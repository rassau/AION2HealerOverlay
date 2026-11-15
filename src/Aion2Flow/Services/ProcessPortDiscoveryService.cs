using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Net;
using Cloris.Aion2Flow.Services.Logging;
using Cloris.Aion2Flow.WinDivert;
using Windows.Wdk.System.SystemInformation;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Networking.WinSock;
using Windows.Win32.NetworkManagement.IpHelper;
using Windows.Win32.System.Diagnostics.ToolHelp;
using WdkPInvoke = Windows.Wdk.PInvoke;


namespace Cloris.Aion2Flow.Services;

public sealed class ProcessPortDiscoveryService : IAsyncDisposable
{
    private enum PortEventType { Add, Remove }
    internal readonly record struct PortPair(ushort LocalPort, ushort RemotePort);

    private const int SearchPollInterval = 1000;
    private const int KnownProcessPollInterval = 1000;
    private const int KnownProcessRefreshInterval = 5000;

    private const SYSTEM_INFORMATION_CLASS SystemBasicProcessInformation = (SYSTEM_INFORMATION_CLASS)252;
    // Far beyond any real list (700 processes take 55 KB), and a buffer that size can still be made.
    private const int MaxProcessInformationLength = 16 << 20;
    private static readonly uint OwnProcessId = (uint)Environment.ProcessId;
    private static readonly string? OwnImageName = Path.GetFileName(Environment.ProcessPath);

    private readonly ConcurrentDictionary<uint, HashSet<PortPair>> _processPorts = new();
    private readonly Lock _snapshotLock = new();

    private volatile bool _snapshotDirty = true;
    private ImmutableArray<ushort> _allPortsSnapshot = [];

    public ImmutableArray<ushort> AllPorts
    {
        get
        {
            if (_snapshotDirty) RebuildAllPortsSnapshot();
            return _allPortsSnapshot;
        }
    }

    private CancellationTokenSource? _cts;
    private Task? _pollTask;
    private Task? _divertTask;
    private WinDivertSession? _divert;
    private byte[] _tcpTableBuffer = [];
    private byte[] _processInfoBuffer = [];

    public bool IsMonitoring { get; private set; }

    public event Action<uint, ushort>? Discovered;
    public event Action<uint, ushort>? Removed;

    public Task StartAsync()
    {
        if (IsMonitoring) return Task.CompletedTask;

        _cts = new CancellationTokenSource();
        _divertTask = StartDivertPortCaptureLoop(_cts.Token);
        _pollTask = StartProcessPollLoop(_cts.Token);

        IsMonitoring = true;
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_cts is null) return;

        _cts.Cancel();
        _divert?.ShutdownReceive();

        try
        {
            if (_pollTask is not null && _divertTask is not null)
                await Task.WhenAll(_pollTask, _divertTask).ConfigureAwait(false);
            else if (_pollTask is not null)
                await _pollTask.ConfigureAwait(false);
            else if (_divertTask is not null)
                await _divertTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { AppLog.Write(AppLogLevel.Warning, $"Process port discovery stop error: {ex}"); }

        _divert?.Dispose();
        _divert = null;
        _processPorts.Clear();
        _snapshotDirty = true;
        _cts.Dispose();
        _cts = null;
        IsMonitoring = false;
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    private Task StartDivertPortCaptureLoop(CancellationToken token)
    {
        _divert = new WinDivertSession("tcp", WinDivertLayer.Flow, WinDivertFlags.Sniff | WinDivertFlags.ReceiveOnly);

        return Task.Factory.StartNew(() =>
        {
            var address = new WinDivertAddress();
            var buffer = Span<byte>.Empty;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    _divert.Receive(buffer, ref address);
                    var addr = address;

                    if (!address.TryGetFlowData(out var flow) || flow.ProcessId == 0)
                        continue;

                    var eventType = addr.Event == WinDivertEvent.FlowEstablished ? PortEventType.Add :
                                    addr.Event == WinDivertEvent.FlowDeleted ? PortEventType.Remove : (PortEventType?)null;

                    if (eventType == null) continue;

                    if (_processPorts.ContainsKey(flow.ProcessId))
                    {
                        UpdatePortState(flow.ProcessId, new(flow.LocalPort, flow.RemotePort), eventType.Value);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    AppLog.Write(AppLogLevel.Error, $"WinDivert flow session stopped: {ex}");
                    break;
                }
            }
        }, TaskCreationOptions.LongRunning);
    }

    private Task StartProcessPollLoop(CancellationToken token)
    {
        return Task.Factory.StartNew(async () =>
        {
            var knownPids = new HashSet<uint>();
            var currentPids = new HashSet<uint>();
            var vanishedPids = new List<uint>();
            var currentConnections = new HashSet<PortPair>();
            var sw = Stopwatch.StartNew();
            var nextProcessRefreshAt = 0L;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var now = sw.ElapsedMilliseconds;
                    if (knownPids.Count == 0 || now >= nextProcessRefreshAt)
                    {
                        currentPids.Clear();
                        if (TryGetPidsByProcessName(currentPids))
                        {
                            nextProcessRefreshAt = now + (currentPids.Count == 0 ? SearchPollInterval : KnownProcessRefreshInterval);

                            foreach (var pid in currentPids)
                            {
                                knownPids.Add(pid);
                                EnsureProcessTracked(pid);
                            }

                            vanishedPids.Clear();
                            foreach (var pid in knownPids)
                            {
                                if (!currentPids.Contains(pid))
                                    vanishedPids.Add(pid);
                            }

                            if (vanishedPids.Count != 0)
                            {
                                foreach (var pid in vanishedPids)
                                {
                                    knownPids.Remove(pid);
                                    if (_processPorts.TryRemove(pid, out var portSet))
                                    {
                                        lock (portSet)
                                        {
                                            var uniqueLocals = new HashSet<ushort>();
                                            foreach (var (LocalPort, _) in portSet) uniqueLocals.Add(LocalPort);
                                            foreach (var lp in uniqueLocals) Removed?.Invoke(pid, lp);
                                        }
                                        _snapshotDirty = true;
                                    }
                                }
                            }
                        }
                    }

                    foreach (var pid in knownPids)
                    {
                        EnsureProcessTracked(pid);

                        currentConnections.Clear();
                        if (TryGetTcpPortsForPid(pid, currentConnections))
                            SynchronizeProcessPorts(pid, currentConnections);
                    }
                }
                catch (Exception ex)
                {
                    AppLog.Write(AppLogLevel.Warning, $"Process port discovery polling failed: {ex}");
                }

                var delay = knownPids.Count == 0 ? SearchPollInterval : KnownProcessPollInterval;
                await Task.Delay(delay, token).ConfigureAwait(false);
            }
        }, TaskCreationOptions.LongRunning).Unwrap();
    }

    internal void SynchronizeProcessPorts(uint pid, HashSet<PortPair> currentPorts)
    {
        EnsureProcessTracked(pid);

        if (!_processPorts.TryGetValue(pid, out var portSet))
            return;

        List<PortPair>? stalePorts = null;
        List<ushort>? discoveredPorts = null;
        List<ushort>? removedPorts = null;
        var changed = false;

        lock (portSet)
        {
            foreach (var portPair in currentPorts)
            {
                var alreadyHasLocal = HasLocalPort(portSet, portPair.LocalPort);
                if (portSet.Add(portPair))
                {
                    changed = true;
                    if (!alreadyHasLocal)
                    {
                        discoveredPorts ??= [];
                        discoveredPorts.Add(portPair.LocalPort);
                    }
                }
            }

            foreach (var portPair in portSet)
            {
                if (!currentPorts.Contains(portPair))
                {
                    stalePorts ??= [];
                    stalePorts.Add(portPair);
                }
            }

            if (stalePorts is not null)
            {
                foreach (var portPair in stalePorts)
                {
                    if (portSet.Remove(portPair))
                    {
                        changed = true;
                        if (!HasLocalPort(portSet, portPair.LocalPort))
                        {
                            removedPorts ??= [];
                            removedPorts.Add(portPair.LocalPort);
                        }
                    }
                }
            }
        }

        if (!changed)
        {
            return;
        }

        _snapshotDirty = true;

        if (discoveredPorts is not null)
        {
            foreach (var localPort in discoveredPorts)
            {
                Discovered?.Invoke(pid, localPort);
            }
        }

        if (removedPorts is not null)
        {
            foreach (var localPort in removedPorts)
            {
                Removed?.Invoke(pid, localPort);
            }
        }
    }

    private void EnsureProcessTracked(uint pid)
    {
        if (_processPorts.TryAdd(pid, []))
        {
            _snapshotDirty = true;
        }
    }

    private void UpdatePortState(uint pid, PortPair portPair, PortEventType type)
    {
        if (!_processPorts.TryGetValue(pid, out var portSet)) return;

        bool changed = false;
        bool isFirstLocal = false;
        bool isLastLocal = false;

        lock (portSet)
        {
            if (type == PortEventType.Add)
            {
                bool alreadyHasLocal = HasLocalPort(portSet, portPair.LocalPort);
                if (portSet.Add(portPair))
                {
                    changed = true;
                    if (!alreadyHasLocal) isFirstLocal = true;
                }
            }
            else
            {
                if (portSet.Remove(portPair))
                {
                    changed = true;
                    if (!HasLocalPort(portSet, portPair.LocalPort)) isLastLocal = true;
                }
            }
        }

        if (changed)
        {
            _snapshotDirty = true;
            if (isFirstLocal) Discovered?.Invoke(pid, portPair.LocalPort);
            if (isLastLocal) Removed?.Invoke(pid, portPair.LocalPort);
        }
    }

    private static bool HasLocalPort(HashSet<PortPair> portSet, ushort localPort)
    {
        foreach (var pair in portSet)
        {
            if (pair.LocalPort == localPort)
                return true;
        }

        return false;
    }

    // The capture thread and the UI thread both rebuild. One at a time, so an older snapshot is never published over a
    // newer one; and the flag is cleared before the sets are read, so a change that lands during the rebuild sets it
    // again and the next read picks the change up.
    private void RebuildAllPortsSnapshot()
    {
        lock (_snapshotLock)
        {
            if (!_snapshotDirty) return;
            _snapshotDirty = false;
            Interlocked.MemoryBarrier();

            var uniquePorts = new HashSet<ushort>();
            foreach (var kvp in _processPorts)
            {
                lock (kvp.Value)
                {
                    foreach (var (LocalPort, _) in kvp.Value) uniquePorts.Add(LocalPort);
                }
            }

            var sortedPorts = uniquePorts.ToArray();
            Array.Sort(sortedPorts);
            _allPortsSnapshot = [.. sortedPorts];
        }
    }

    internal unsafe bool TryGetTcpPortsForPid(uint targetPid, HashSet<PortPair> ports)
    {
        ports.Clear();

        // One call into the kept buffer. Only when the table no longer fits does it grow, with room to spare, and the
        // table is asked for again.
        var size = (uint)_tcpTableBuffer.Length;
        var res = PInvoke.GetExtendedTcpTable(_tcpTableBuffer, ref size, true, (uint)ADDRESS_FAMILY.AF_INET, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0);
        if (res == (uint)WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER)
        {
            _tcpTableBuffer = GC.AllocateUninitializedArray<byte>((int)size * 2);
            size = (uint)_tcpTableBuffer.Length;
            res = PInvoke.GetExtendedTcpTable(_tcpTableBuffer, ref size, true, (uint)ADDRESS_FAMILY.AF_INET, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0);
        }

        if (res != (uint)WIN32_ERROR.NO_ERROR)
        {
            return false;
        }

        fixed (byte* pBuffer = _tcpTableBuffer)
        {
            uint rowCount = *(uint*)pBuffer;
            var pRow = (MIB_TCPROW_OWNER_PID*)(pBuffer + sizeof(uint));

            for (int i = 0; i < rowCount; i++)
            {
                ref var row = ref pRow[i];
                if (row.dwOwningPid != targetPid) continue;
                if (row.dwState == MIB_TCP_STATE.MIB_TCP_STATE_DELETE_TCB) continue;

                ushort localPort = (ushort)IPAddress.NetworkToHostOrder((short)row.dwLocalPort);
                ushort remotePort = (ushort)IPAddress.NetworkToHostOrder((short)row.dwRemotePort);
                ports.Add(new(localPort, remotePort));
            }
        }

        return true;
    }

    private bool TryGetPidsByProcessName(HashSet<uint> pids)
    {
        if (TryQueryBasicProcessInformation(out var information) && TryCollectPids(information, pids))
            return true;

        // Windows before 11 24H2, or a list that does not look as expected.
        pids.Clear();
        return TryGetPidsFromToolhelpSnapshot(pids);
    }

    // The same process ids and image names as the toolhelp snapshot, without the data of every thread that the snapshot
    // collects first: about 0.06 ms instead of 20 ms with 750 processes. Undocumented, Windows 11 24H2 and later: a length
    // out of reason, or a list that outgrows the buffer three times in a row, counts as no list (the toolhelp walk runs).
    internal unsafe bool TryQueryBasicProcessInformation(out ReadOnlySpan<byte> information)
        => TryQueryBasicProcessInformation(&QueryBasicProcessInformation, out information);

    // The query is a parameter so that a test can answer as a future Windows build might.
    internal unsafe bool TryQueryBasicProcessInformation(delegate*<byte*, uint, uint*, NTSTATUS> query, out ReadOnlySpan<byte> information)
    {
        information = default;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            uint length = 0;
            NTSTATUS status;
            fixed (byte* buffer = _processInfoBuffer)
                status = query(buffer, (uint)_processInfoBuffer.Length, &length);

            if (status == NTSTATUS.STATUS_SUCCESS)
            {
                if (length > _processInfoBuffer.Length)
                    return false;

                information = _processInfoBuffer.AsSpan(0, (int)length);
                return true;
            }

            if (status != NTSTATUS.STATUS_INFO_LENGTH_MISMATCH || length <= _processInfoBuffer.Length || length > MaxProcessInformationLength)
                return false;

            // Pinned, as the names' addresses point into it; with room for processes started before the next call.
            _processInfoBuffer = GC.AllocateUninitializedArray<byte>((int)length + 16 * 1024, pinned: true);
        }

        return false;
    }

    private static unsafe NTSTATUS QueryBasicProcessInformation(byte* buffer, uint length, uint* returnLength)
        => WdkPInvoke.NtQuerySystemInformation(SystemBasicProcessInformation, buffer, length, returnLength);

    // The list counts only if it holds this process under its own file name, which checks that the layout still holds.
    internal static unsafe bool TryCollectPids(ReadOnlySpan<byte> information, HashSet<uint> pids)
    {
        var foundSelf = false;
        fixed (byte* start = information)
        {
            var end = start + information.Length;
            for (var at = start; ;)
            {
                if (at + sizeof(SystemBasicProcessInformationEntry) > end)
                    return false;

                var entry = (SystemBasicProcessInformationEntry*)at;
                var nameOffset = (byte*)entry->ImageName - start;
                if (entry->ImageNameLength != 0 && (nameOffset < 0 || nameOffset > information.Length - entry->ImageNameLength))
                    return false;

                var processName = new ReadOnlySpan<char>(entry->ImageName, entry->ImageNameLength / sizeof(char));
                var pid = (uint)entry->UniqueProcessId;
                if (Aion2ProcessIdentity.MatchesExecutableName(processName))
                    pids.Add(pid);
                if (pid == OwnProcessId)
                    foundSelf = processName.Equals(OwnImageName, StringComparison.OrdinalIgnoreCase);

                if (entry->NextEntryOffset == 0)
                    return foundSelf;
                at += entry->NextEntryOffset;
            }
        }
    }

    // SYSTEM_BASICPROCESS_INFORMATION.
    internal unsafe struct SystemBasicProcessInformationEntry
    {
        public uint NextEntryOffset;
        public nint UniqueProcessId;
        public nint InheritedFromUniqueProcessId;
        public ulong SequenceNumber;
        public ushort ImageNameLength; // ImageName, a UNICODE_STRING: its length in bytes, capacity and text
        public ushort ImageNameMaximumLength;
        public char* ImageName;
    }

    internal static unsafe bool TryGetPidsFromToolhelpSnapshot(HashSet<uint> pids)
    {
        var snapshot = PInvoke.CreateToolhelp32Snapshot_SafeHandle(CREATE_TOOLHELP_SNAPSHOT_FLAGS.TH32CS_SNAPPROCESS, 0);

        if (snapshot.IsInvalid)
        {
            return false;
        }

        try
        {
            PROCESSENTRY32W entry = default;
            entry.dwSize = (uint)sizeof(PROCESSENTRY32W);

            if (PInvoke.Process32FirstW(snapshot, ref entry))
            {
                do
                {
                    var processName = entry.szExeFile.AsReadOnlySpan();

                    if (processName.IndexOf('\0') is int length and not -1)
                        processName = processName[..length];

                    if (Aion2ProcessIdentity.MatchesExecutableName(processName))
                    {
                        pids.Add(entry.th32ProcessID);
                    }
                }
                while (PInvoke.Process32NextW(snapshot, ref entry));
            }
        }
        finally
        {
            snapshot.Close();
        }

        return true;
    }
}
