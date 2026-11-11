using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Cloris.Aion2Flow.Assets.Icons;
using Cloris.Aion2Flow.Controls;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Audio;
using Cloris.Aion2Flow.Services.Hotkeys;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.ViewModels;
using Cloris.Aion2Flow.Views;

namespace Cloris.Aion2Flow.Tests.App;

[Collection(AvaloniaTestCollection.Name)]
public sealed class AlertSoundTests
{
    [Fact]
    public void The_Chime_Is_Short_And_Has_Headroom()
    {
        var chime = AudioClip.Chime();
        var peak = chime.Samples.Max(sample => Math.Abs((int)sample));
        Assert.InRange(chime.Samples.Length / (double)chime.SampleRate, 0.5, 1);
        Assert.InRange(peak, short.MaxValue * 0.3, short.MaxValue * 0.75);
    }

    // The knock and the blip: two short hits each, the second (at 0.13 s and 0.15 s) well above what's left of the first,
    // peaking at 60 % like the chime and fading out instead of ending in a click.
    [Theory]
    [InlineData(AlertSounds.KnockName, 0.13)]
    [InlineData(AlertSounds.BlipName, 0.15)]
    public void Knock_And_Blip_Are_Two_Short_Hits(string name, double second)
    {
        var clip = new AlertSounds(Path.GetTempPath()).Load(name);
        var rate = clip.SampleRate;
        Assert.InRange(clip.Samples.Length / (double)rate, 0.3, 0.6);
        Assert.InRange(clip.Samples.Max(sample => Math.Abs((int)sample)), short.MaxValue * 0.55, short.MaxValue * 0.6);
        Assert.True(Level(second, second + 0.03) > 2 * Level(second - 0.03, second));
        Assert.Equal(0, clip.Samples[^1]);

        double Level(double from, double to) => clip.Samples[(int)(from * rate)..(int)(to * rate)].Average(sample => Math.Abs((double)sample));
    }

    // A cut fades in and out over 5 ms, comes back the same from its WAV, and plays at the volume.
    [Fact]
    public void A_Cut_Fades_Its_Ends_And_Survives_Its_Wav()
    {
        var clip = new AudioClip(Enumerable.Repeat((short)10_000, 4_000).ToArray(), 8_000);
        var cut = clip.Cut(1_000, 3_000);
        Assert.Equal(2_000, cut.Samples.Length);
        Assert.Equal((0, 0, 10_000), (cut.Samples[0], cut.Samples[^1], cut.Samples[1_000]));
        Assert.True(cut.Samples[20] is > 0 and < 10_000);

        var back = AudioClip.ReadWav(cut.ToWav());
        Assert.Equal(cut.SampleRate, back.SampleRate);
        Assert.Equal(cut.Samples, back.Samples);
        Assert.Equal(5_000, AudioClip.ReadWav(cut.ToWav(volumePercent: 50)).Samples[1_000]);
        // Up to 200 % for quiet sounds, capped at full scale instead of wrapping round; 0 % is silence.
        var loud = new AudioClip([1_000, 20_000, -20_000], 8_000);
        Assert.Equal(new short[] { 2_000, short.MaxValue, short.MinValue }, AudioClip.ReadWav(loud.ToWav(volumePercent: 200)).Samples);
        Assert.All(AudioClip.ReadWav(loud.ToWav(volumePercent: 0)).Samples, sample => Assert.Equal(0, sample));
    }

    // Windows' Media Foundation reads a stereo WAV like an mp3 or a video's sound: mixed down to mono at its own rate, and
    // only as far as asked.
    [Fact]
    public void Media_Foundation_Reads_A_File_As_Mono()
    {
        Assert.SkipUnless(NativeLibrary.TryLoad("mfplat.dll", out _) && NativeLibrary.TryLoad("mfreadwrite.dll", out _),
            "Media Foundation isn't installed here (Windows Server without its media feature).");
        var path = Path.Combine(Path.GetTempPath(), $"aion2flow-stereo-{Guid.NewGuid():N}.wav");
        try
        {
            File.WriteAllBytes(path, StereoWav(rate: 22_050, frames: 22_050, left: 8_000, right: 4_000));
            var all = AudioFileDecoder.Decode(path, TimeSpan.FromSeconds(10));
            Assert.Equal(22_050, all.SampleRate);
            Assert.Equal(22_050, all.Samples.Length);
            Assert.All(all.Samples, sample => Assert.Equal(6_000, sample));
            Assert.Equal(11_025, AudioFileDecoder.Decode(path, TimeSpan.FromSeconds(0.5)).Samples.Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // Ogg Opus (Discord's soundboard sounds): Windows has the Opus decoder but no Ogg reader, so the sound goes in as WebM.
    // Here 50 packets of 20 ms (empty ones, which decode as silence): a second at Opus' 48 kHz, and only as far as asked.
    [Fact]
    public void Ogg_Opus_Goes_In_As_Webm()
    {
        Assert.SkipUnless(NativeLibrary.TryLoad("mfplat.dll", out _) && NativeLibrary.TryLoad("mfreadwrite.dll", out _)
            && NativeLibrary.TryLoad("MSOpusDecoder.dll", out _) && NativeLibrary.TryLoad("mfmkvsrcsnk.dll", out _),
            "Windows' Opus decoder or WebM reader isn't installed here.");
        var path = Path.Combine(Path.GetTempPath(), $"aion2flow-opus-{Guid.NewGuid():N}.ogg");
        try
        {
            var head = new byte[19];
            "OpusHead"u8.CopyTo(head);
            head[8] = 1; // version
            head[9] = 1; // channels; no pre-skip
            BinaryPrimitives.WriteInt32LittleEndian(head.AsSpan(12), 48_000);
            byte[] tags = [.. "OpusTags"u8, 0, 0, 0, 0, 0, 0, 0, 0];
            File.WriteAllBytes(path, Ogg([head, tags, .. Enumerable.Repeat(new byte[] { 0xF8 }, 50)]));
            Assert.True(OggOpus.Is(path));
            var all = AudioFileDecoder.Decode(path, TimeSpan.FromSeconds(10));
            Assert.Equal(48_000, all.SampleRate);
            Assert.Equal(48_000, all.Samples.Length);
            Assert.All(all.Samples, sample => Assert.Equal(0, sample));
            Assert.Equal(24_000, AudioFileDecoder.Decode(path, TimeSpan.FromSeconds(0.5)).Samples.Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // An Ogg stream, a page per packet (without checksums: nothing here reads them).
    private static byte[] Ogg(params byte[][] packets)
    {
        var ogg = new List<byte>();
        for (var i = 0; i < packets.Length; i++)
        {
            var page = new byte[27];
            "OggS"u8.CopyTo(page);
            page[5] = (byte)(i == 0 ? 2 : i == packets.Length - 1 ? 4 : 0); // first, last
            BinaryPrimitives.WriteInt32LittleEndian(page.AsSpan(18), i);
            page[26] = (byte)(packets[i].Length / 255 + 1);
            ogg.AddRange(page);
            for (var left = packets[i].Length; left >= 0; left -= 255)
                ogg.Add((byte)Math.Min(left, 255));
            ogg.AddRange(packets[i]);
        }

        return [.. ogg];
    }

    // At most five, the newest first; a sixth takes the oldest's place and its file. Names stay free and fit for a file.
    [Fact]
    public void Five_Sounds_At_Most_The_Oldest_Goes()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"aion2flow-alert-{Guid.NewGuid():N}");
        try
        {
            var library = new AlertSounds(folder);
            var sounds = new List<string>();
            var clip = new AudioClip([1, 2, 3, 4], 8_000);
            for (var i = 0; i < 6; i++)
                library.Add(clip, "Gong", sounds);
            Assert.Equal(new[] { "Gong 6", "Gong 5", "Gong 4", "Gong 3", "Gong 2" }, sounds);
            Assert.False(File.Exists(library.PathOf("Gong")));
            Assert.Equal("Gong", library.Add(clip, "Gong", sounds));
            Assert.False(File.Exists(library.PathOf("Gong 2")));
            Assert.Equal("ab c", library.Add(clip, " a/b c: ", sounds));

            Assert.Equal(clip.Samples, library.Load("Gong").Samples);
            Assert.Equal(AudioClip.Chime().Samples.Length, library.Load(null).Samples.Length);
            Assert.Equal(AudioClip.Knock().Samples.Length, library.Load(AlertSounds.KnockName).Samples.Length);
            Assert.Equal(AudioClip.Blip().Samples.Length, library.Load(AlertSounds.BlipName).Samples.Length);
            Assert.Equal(AudioClip.Chime().Samples.Length, library.Load("gone").Samples.Length);
            // A sound of the user's can't take a built-in one's name.
            Assert.Equal("knock", library.Add(clip, AlertSounds.KnockName, sounds));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    // None within the quiet time after the last alert; with none set (0 s), every switch to you sounds.
    [Fact]
    public void The_Alert_Waits_Out_Its_Quiet_Time()
    {
        var library = new AlertSounds(Path.GetTempPath());
        Assert.Equal(new[] { true, false, true, true },
            new[] { library.TryAlert(1_000, 5_000), library.TryAlert(5_999, 5_000), library.TryAlert(6_000, 5_000), library.TryAlert(6_000, 0) });
    }

    // The selection stays inside the sound and 50 ms to 10 s long (1,000 samples a second here); the other edge follows.
    [Theory]
    [InlineData(0, 5_000, true, 12_000, 30_000, 2_000, 12_000)]
    [InlineData(0, 5_000, false, 6_000, 30_000, 6_000, 6_050)]
    [InlineData(1_000, 5_000, true, -5, 30_000, 0, 50)]
    [InlineData(0, 30, true, 100, 30, 0, 30)]
    public void An_Edge_Keeps_The_Alert_Between_Its_Limits(int start, int end, bool movingEnd, int at, int length, int newStart, int newEnd)
    {
        Assert.Equal((newStart, newEnd), AlertClipView.MoveEdge(start, end, movingEnd, at, length, min: 50, max: 10_000));
    }

    // The frame's settings have a "Boss alert" submenu; its sound list holds the chime, the user's own sounds (each with
    // an export button) and "Add from file…"; a click picks a sound, and the pick is kept with the settings.
    [Fact]
    public void The_Sound_List_Picks_And_Keeps_A_Sound()
    {
        AvaloniaTestHost.Run(() =>
        {
            AlertSoundPlayer.Muted = true;
            var (localization, _) = ViewTestServices.Get();
            var icons = new IconGeometries();
            Application.Current!.Resources.MergedDictionaries.Add(icons);
            var folder = Path.Combine(Path.GetTempPath(), $"aion2flow-alert-{Guid.NewGuid():N}");
            try
            {
                var settingsService = new SettingsService(Path.Combine(folder, "settings.json"));
                var language = new LanguageService();
                using var uiScale = new UiScaleService(settingsService);
                var settings = new SettingsFlyoutViewModel(localization, language, settingsService, new PlayerNameDisplayService(settingsService, localization), uiScale,
                    new AppUpdateService(), new ProcessForegroundWatcher(), new GlobalHotkeyService());
                settings.AddAlertSound(new AudioClip([1, 2, 3], 8_000), "Gong");
                settings.AddAlertSound(new AudioClip([1, 2, 3], 8_000), "Bell");
                Assert.True(File.Exists(Path.Combine(folder, "alert-sounds", "Gong.wav")));
                Assert.Equal("Bell", settings.GroupFrameAlertSound);

                var menu = new MenuItem();
                var groupFrameMenu = new GroupFrameSettingsMenu(settings, menu.Items);
                var alert = (MenuItem)menu.Items[^2]!;
                var list = (MenuItem)alert.Items[1]!;
                var items = list.Items.Cast<MenuItem>().ToList();
                Assert.Equal(new[] { "", AlertSounds.KnockName, AlertSounds.BlipName, "Bell", "Gong", null }, items.Select(item => item.Tag as string));
                Assert.Equal(new[] { false, false, false, true, true, false }, items.Select(item => item.Header is Grid grid && grid.Children.OfType<Button>().Any()));
                Assert.Equal(new[] { false, false, false, true, false, false }, items.Select(item => item.Icon is not null));

                items[4].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Assert.Equal("Gong", settings.GroupFrameAlertSound);
                // As the settings flyout and the frame's gear do on every change.
                groupFrameMenu.Refresh();
                Assert.Equal(new[] { false, false, false, false, true, false }, list.Items.Cast<MenuItem>().Select(item => item.Icon is not null));
                // The knock and the blip ship with the overlay, like the chime: the row shows their name, not their key.
                items[2].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                groupFrameMenu.Refresh();
                Assert.Equal(AlertSounds.BlipName, settings.GroupFrameAlertSound);
                Assert.Equal(localization["Settings_GroupFrame_AlertBlip"], ((Grid)list.Header!).Children.OfType<TextBlock>().Last().Text);
                items[0].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Assert.Null(settings.GroupFrameAlertSound);

                // The quiet time: 0-10 s, 5 s at first.
                var quiet = ((Grid)((MenuItem)alert.Items[3]!).Header!).Children;
                var quietSlider = quiet.OfType<Slider>().Single();
                Assert.Equal((0d, 10d, 5d, "5 s"), (quietSlider.Minimum, quietSlider.Maximum, quietSlider.Value, quiet.OfType<TextBlock>().Last().Text));
                quietSlider.Value = 0;
                // The volume: 0-200 %, 100 % at first.
                var volume = ((Grid)((MenuItem)alert.Items[2]!).Header!).Children.OfType<Slider>().Single();
                Assert.Equal((0d, 200d, 100d), (volume.Minimum, volume.Maximum, volume.Value));
                volume.Value = 200;
                Assert.Equal(0, settings.GroupFrameAlertQuietSeconds);
                settings.SaveOverlayOpacity();

                var saved = new SettingsService(settingsService.FilePath).Current;
                Assert.Equal(new[] { "Bell", "Gong" }, saved.GroupFrameAlertSounds);
                Assert.Null(saved.GroupFrameAlertSound);
                Assert.Equal(0, saved.GroupFrameAlertQuietSeconds);
                Assert.Equal(200, saved.GroupFrameAlertVolumePercent);
            }
            finally
            {
                Application.Current.Resources.MergedDictionaries.Remove(icons);
                AlertSoundPlayer.Muted = false;
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
            }
        });
    }

    private static byte[] StereoWav(int rate, int frames, short left, short right)
    {
        var bytes = new byte[44 + frames * 4];
        var wav = bytes.AsSpan();
        "RIFF"u8.CopyTo(wav);
        BinaryPrimitives.WriteInt32LittleEndian(wav[4..], bytes.Length - 8);
        "WAVEfmt "u8.CopyTo(wav[8..]);
        BinaryPrimitives.WriteInt32LittleEndian(wav[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(wav[20..], 1);
        BinaryPrimitives.WriteInt16LittleEndian(wav[22..], 2);
        BinaryPrimitives.WriteInt32LittleEndian(wav[24..], rate);
        BinaryPrimitives.WriteInt32LittleEndian(wav[28..], rate * 4);
        BinaryPrimitives.WriteInt16LittleEndian(wav[32..], 4);
        BinaryPrimitives.WriteInt16LittleEndian(wav[34..], 16);
        "data"u8.CopyTo(wav[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(wav[40..], frames * 4);
        for (var i = 0; i < frames; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(wav[(44 + i * 4)..], left);
            BinaryPrimitives.WriteInt16LittleEndian(wav[(46 + i * 4)..], right);
        }

        return bytes;
    }
}
