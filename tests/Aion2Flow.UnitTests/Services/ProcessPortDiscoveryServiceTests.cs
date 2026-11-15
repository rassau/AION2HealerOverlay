using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Cloris.Aion2Flow.Services;
using Windows.Win32.Foundation;

namespace Cloris.Aion2Flow.Tests.Services;

public sealed class ProcessPortDiscoveryServiceTests
{
    [Fact]
    public async Task SynchronizeProcessPorts_ReplacesStaleCrossServerConnectionPort()
    {
        await using var service = new ProcessPortDiscoveryService();
        var discovered = new List<ushort>();
        var removed = new List<ushort>();
        service.Discovered += (_, port) => discovered.Add(port);
        service.Removed += (_, port) => removed.Add(port);

        service.SynchronizeProcessPorts(42, [new(31_001, 21_001)]);
        service.SynchronizeProcessPorts(42, [new(31_777, 21_777)]);

        Assert.Equal([31_777], service.AllPorts);
        Assert.Equal([31_001, 31_777], discovered);
        Assert.Equal([31_001], removed);
    }

    [Fact]
    public async Task SynchronizeProcessPorts_KeepsLocalPortUntilAllRemotePairsAreGone()
    {
        await using var service = new ProcessPortDiscoveryService();
        var discovered = new List<ushort>();
        var removed = new List<ushort>();
        service.Discovered += (_, port) => discovered.Add(port);
        service.Removed += (_, port) => removed.Add(port);

        service.SynchronizeProcessPorts(42, [new(31_001, 21_001), new(31_001, 21_002)]);
        service.SynchronizeProcessPorts(42, [new(31_001, 21_002)]);

        Assert.Equal([31_001], service.AllPorts);
        Assert.Equal([31_001], discovered);
        Assert.Empty(removed);
    }

    // The capture thread rebuilds the snapshot on its next packet after a change. A port the flow or poll thread adds
    // to a set that rebuild has already read must still show on the following read, not stay missing until some
    // unrelated change.
    [Fact]
    public async Task AllPorts_ShowsAPortAddedWhileAnotherThreadRebuildsTheSnapshot()
    {
        await using var service = new ProcessPortDiscoveryService();
        service.SynchronizeProcessPorts(1, [new(31_001, 21_001)]);
        service.SynchronizeProcessPorts(2, [new(32_001, 22_001)]);
        var sets = PortSets(service).ToArray(); // in the order a rebuild reads them

        Exception? failure = null;
        var reader = new Thread(() => { try { _ = service.AllPorts; } catch (Exception ex) { failure = ex; } });
        // Holding the second set's lock stops the rebuild after it has read the first set.
        lock (sets[1].Value)
        {
            reader.Start();
            WaitUntilBlockedOrDone(reader);
            service.SynchronizeProcessPorts(sets[0].Key, [.. sets[0].Value, new(31_777, 21_777)]);
        }
        reader.Join();

        Assert.Null(failure);
        Assert.Contains((ushort)31_777, service.AllPorts);
    }

    // When the game exits, the poll loop drops its pid. A rebuild running at that moment on the capture thread must not
    // throw: an exception there ends the capture loop.
    [Fact]
    public async Task AllPorts_DoesNotThrowWhenAProcessIsDroppedDuringARebuild()
    {
        await using var service = new ProcessPortDiscoveryService();
        service.SynchronizeProcessPorts(1, [new(31_001, 21_001)]);
        service.SynchronizeProcessPorts(2, [new(32_001, 22_001)]);
        service.SynchronizeProcessPorts(3, [new(33_001, 23_001)]);
        var portSets = PortSets(service);
        var sets = portSets.ToArray();

        Exception? failure = null;
        var reader = new Thread(() => { try { _ = service.AllPorts; } catch (Exception ex) { failure = ex; } });
        lock (sets[1].Value)
        {
            reader.Start();
            WaitUntilBlockedOrDone(reader);
            // The poll loop's vanished-pid branch, for the process the rebuild has not reached yet.
            Assert.True(portSets.TryRemove(sets[2].Key, out _));
        }
        reader.Join();

        Assert.Null(failure);
    }

    // The capture thread and the UI thread can both rebuild. A rebuild that started before a change must not publish its
    // older snapshot after one that started after the change: the change would stay missing until some unrelated change.
    [Fact]
    public async Task AllPorts_KeepsTheNewerSnapshotWhenTwoThreadsRebuildAtOnce()
    {
        await using var service = new ProcessPortDiscoveryService();
        service.SynchronizeProcessPorts(1, [new(31_001, 21_001)]);
        service.SynchronizeProcessPorts(2, [new(32_001, 22_001)]);
        var portSets = PortSets(service);
        var sets = portSets.ToArray();

        Exception? failure = null;
        void Read() { try { _ = service.AllPorts; } catch (Exception ex) { failure = ex; } }
        var older = new Thread(Read);
        var newer = new Thread(Read);
        lock (sets[1].Value)
        {
            // The older rebuild has read the first set and waits for the second.
            older.Start();
            WaitUntilBlockedOrDone(older);
            // The second process goes, and a port is added to the first. The newer rebuild no longer needs the second
            // set, so nothing but the snapshot lock stops it from publishing before the older one.
            Assert.True(portSets.TryRemove(sets[1].Key, out _));
            service.SynchronizeProcessPorts(sets[0].Key, [.. sets[0].Value, new(31_777, 21_777)]);
            newer.Start();
            WaitUntilBlockedOrDone(newer);
        }
        older.Join();
        newer.Join();

        Assert.Null(failure);
        Assert.Equal(sets[0].Value.Select(pair => pair.LocalPort).Order(), service.AllPorts);
    }

    // The TCP table is read in one call into the kept buffer, which grows the first time.
    [Fact]
    public void TcpPorts_ListTheLoopbackConnectionOfThisProcess()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var client = new TcpClient();
        client.Connect((IPEndPoint)listener.LocalEndpoint);
        using var accepted = listener.AcceptTcpClient();
        var connection = new ProcessPortDiscoveryService.PortPair(
            (ushort)((IPEndPoint)client.Client.LocalEndPoint!).Port, (ushort)((IPEndPoint)listener.LocalEndpoint).Port);

        var service = new ProcessPortDiscoveryService();
        var ports = new HashSet<ProcessPortDiscoveryService.PortPair>();
        Assert.True(service.TryGetTcpPortsForPid((uint)Environment.ProcessId, ports));
        Assert.Contains(connection, ports);
        Assert.True(service.TryGetTcpPortsForPid((uint)Environment.ProcessId, ports));
        Assert.Contains(connection, ports);
    }

    // Windows 11 24H2 and later list the processes without their threads. The game's pids come from that list by the same
    // names as from the toolhelp snapshot; the list counts only if it holds this process under its own name, and a name
    // that points outside the list is never read.
    [Fact]
    public unsafe void ProcessScan_TakesTheGameProcessesFromTheBasicProcessList()
    {
        var self = ((uint)Environment.ProcessId, Path.GetFileName(Environment.ProcessPath)!);
        var pids = new HashSet<uint>();
        Assert.True(ProcessPortDiscoveryService.TryCollectPids(
            BasicProcessList((0, ""), (4, "System"), (1_204, "AION2.exe"), (1_208, "aion2"), (1_212, "Aion2.exe.bak"), (1_216, "NotAion2.exe"), self),
            pids));
        Assert.Equal([1_204u, 1_208u], pids.Order());

        Assert.False(ProcessPortDiscoveryService.TryCollectPids(BasicProcessList((1_204, "Aion2.exe")), []));

        var outside = BasicProcessList((1_204, "Aion2.exe"), self);
        fixed (byte* start = outside)
            ((ProcessPortDiscoveryService.SystemBasicProcessInformationEntry*)start)->ImageName = (char*)(start + outside.Length - 2);
        Assert.False(ProcessPortDiscoveryService.TryCollectPids(outside, []));
    }

    // The list's class is undocumented. Should a Windows build answer with a length out of reason, or with a list that
    // outgrows the buffer again and again, the query gives up (the toolhelp walk runs) instead of throwing or asking forever.
    [Fact]
    public unsafe void ProcessScan_GivesUpOnAnAnswerOutOfReason()
    {
        var success = NTSTATUS.STATUS_SUCCESS;
        var mismatch = NTSTATUS.STATUS_INFO_LENGTH_MISMATCH;
        Assert.False(Query((success, 48))); // a list longer than the buffer, which is still empty
        Assert.False(Query((mismatch, (16 << 20) + 1), (success, 48))); // asks for more than 16 MB
        Assert.False(Query((mismatch, 0), (success, 48))); // asks for no more than the buffer
        Assert.True(Query((mismatch, 100), (mismatch, 20_000), (success, 48))); // three tries
        Assert.False(Query((mismatch, 100), (mismatch, 20_000), (mismatch, 40_000), (success, 48))); // not four

        static bool Query(params (NTSTATUS Status, uint Length)[] answers)
        {
            s_answers = new(answers);
            return new ProcessPortDiscoveryService().TryQueryBasicProcessInformation(&Answer, out _);
        }
    }

    // On the real OS the list passes its self-check and gives the same game pids as the toolhelp snapshot. A copy of ping
    // under the game's file name stands in for the game. Another Aion2.exe (the game, or the copy of this test in another
    // run) can start or end during the reads, so the list is held against a snapshot taken before it and one after it.
    [Fact]
    public void ProcessScan_FindsTheSameGameProcessesInTheBasicListAsInTheToolhelpSnapshot()
    {
        var service = new ProcessPortDiscoveryService();
        Assert.SkipUnless(service.TryQueryBasicProcessInformation(out _), "Windows lists processes without their threads since 11 24H2.");

        var folder = Directory.CreateTempSubdirectory();
        System.Diagnostics.Process? game = null;
        try
        {
            var path = Path.Combine(folder.FullName, "Aion2.exe");
            File.Copy(Path.Combine(Environment.SystemDirectory, "PING.EXE"), path);
            game = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path, "-n 60 127.0.0.1") { CreateNoWindow = true, RedirectStandardOutput = true })!;
            var copy = (uint)game.Id;

            var before = new HashSet<uint>();
            var fromList = new HashSet<uint>();
            var after = new HashSet<uint>();
            // The first snapshot waits until it lists the copy, so the copy is up for all three reads. Its output can't
            // tell: ping's piped output arrives only when it ends.
            Assert.True(SpinWait.SpinUntil(
                () => { before.Clear(); return ProcessPortDiscoveryService.TryGetPidsFromToolhelpSnapshot(before) && before.Contains(copy); },
                TimeSpan.FromSeconds(10)));
            Assert.True(service.TryQueryBasicProcessInformation(out var information));
            Assert.True(ProcessPortDiscoveryService.TryCollectPids(information, fromList));
            Assert.True(ProcessPortDiscoveryService.TryGetPidsFromToolhelpSnapshot(after));

            // The list holds every game process that both snapshots hold, the copy among them, and none that neither holds.
            var inBoth = before.Intersect(after).ToHashSet();
            Assert.Contains(copy, inBoth);
            Assert.Superset(inBoth, fromList);
            Assert.Subset(before.Union(after).ToHashSet(), fromList);
        }
        finally
        {
            game?.Kill();
            game?.WaitForExit();
            game?.Dispose();
            try { folder.Delete(recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    [ThreadStatic]
    private static Queue<(NTSTATUS Status, uint Length)>? s_answers;

    private static unsafe NTSTATUS Answer(byte* buffer, uint length, uint* returnLength)
    {
        var (status, answer) = s_answers!.Dequeue();
        *returnLength = answer;
        return status;
    }

    // A list as Windows returns it: each entry followed by its name, the name's address pointing into the same (pinned) list.
    private static unsafe byte[] BasicProcessList(params (uint Pid, string Name)[] processes)
    {
        var entrySize = sizeof(ProcessPortDiscoveryService.SystemBasicProcessInformationEntry);
        var sizes = processes.Select(process => (entrySize + process.Name.Length * sizeof(char) + 7) & ~7).ToArray();
        var list = GC.AllocateArray<byte>(sizes.Sum(), pinned: true);
        fixed (byte* start = list)
        {
            var at = start;
            for (var i = 0; i < processes.Length; i++)
            {
                var (pid, name) = processes[i];
                var entry = (ProcessPortDiscoveryService.SystemBasicProcessInformationEntry*)at;
                entry->NextEntryOffset = i == processes.Length - 1 ? 0 : (uint)sizes[i];
                entry->UniqueProcessId = (nint)pid;
                entry->ImageNameLength = entry->ImageNameMaximumLength = (ushort)(name.Length * sizeof(char));
                entry->ImageName = name.Length == 0 ? null : (char*)(at + entrySize);
                name.CopyTo(new Span<char>(at + entrySize, name.Length));
                at += sizes[i];
            }
        }

        return list;
    }

    private static ConcurrentDictionary<uint, HashSet<ProcessPortDiscoveryService.PortPair>> PortSets(ProcessPortDiscoveryService service)
        => (ConcurrentDictionary<uint, HashSet<ProcessPortDiscoveryService.PortPair>>)typeof(ProcessPortDiscoveryService)
            .GetField("_processPorts", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(service)!;

    private static void WaitUntilBlockedOrDone(Thread thread)
        => Assert.True(SpinWait.SpinUntil(
            () => !thread.IsAlive || (thread.ThreadState & ThreadState.WaitSleepJoin) != 0,
            TimeSpan.FromSeconds(10)));
}
