using Avalonia.Controls;
using Avalonia.Media;

namespace Cloris.Aion2Flow.Assets.Icons;

internal sealed class IconGeometries : ResourceDictionary
{
    private const string Bbox = "M0 0M24 24";
    internal static StreamGeometry Play { get; } = Geometry("M6 3l14 9-14 9V3z");
    internal static StreamGeometry Pencil { get; } = Geometry("M21.174 6.812a1 1 0 0 0-3.986-3.987L3.842 16.174a2 2 0 0 0-.5.83l-1.321 4.352a.5.5 0 0 0 .623.622l4.353-1.32a2 2 0 0 0 .83-.497zM15 5l4 4");
    internal static StreamGeometry Download { get; } = Geometry("M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M7 10l5 5 5-5M12 15V3");
    internal static StreamGeometry Trash { get; } = Geometry("M3 6h18M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2M10 11v6M14 11v6");

    public IconGeometries()
    {
        Add("rotate-cw", Geometry($"M21 12a9 9 0 1 1-9-9c2.52 0 4.93 1 6.74 2.74L21 8M21 3v5h-5"));
        Add("minus", Geometry("M5 12h14"));
        Add("plus", Geometry("M12 5v14M5 12h14"));
        Add("x", Geometry("M18 6 6 18M6 6l12 12"));
        Add("wifi", Geometry("M12 20h.01M2 8.82a15 15 0 0 1 20 0M5 12.859a10 10 0 0 1 14 0M8.5 16.429a5 5 0 0 1 7 0"));
        Add("radio", Geometry("M4.9 19.1C1 15.2 1 8.8 4.9 4.9M7.8 16.2c-2.3-2.3-2.3-6.1 0-8.4M16.2 7.8c2.3 2.3 2.3 6.1 0 8.4M19.1 4.9C23 8.8 23 15.1 19.1 19M14 12a2 2 0 1 1-4 0 2 2 0 0 1 4 0z"));
        Add("trash", Trash);
        // A snapshot's note in the twin library (Lucide's notebook-pen).
        Add("notebook-pen", Geometry("M13.4 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-7.4M2 6h4M2 10h4M2 14h4M2 18h4M21.378 5.626a1 1 0 1 0-3.004-3.004l-5.01 5.012a2 2 0 0 0-.506.854l-.837 2.87a.5.5 0 0 0 .62.62l2.87-.837a2 2 0 0 0 .854-.506z"));
        Add("heart", Geometry("M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z"));
        // A person with a refresh arrow over the head: look the player up again.
        Add("user-refresh", Geometry("M15.2 12.5a3.2 3.2 0 1 1-6.4 0 3.2 3.2 0 0 1 6.4 0zM5.5 22v-1a4.5 4.5 0 0 1 4.5-4.5h4a4.5 4.5 0 0 1 4.5 4.5v1M7.5 6.5a5 5 0 0 1 9-1.5M17 2v3h-3"));
        Add("users", Geometry("M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M13 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0zM22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75"));
        Add("history", Geometry("M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8M3 3v5h5M12 7v5l4 2"));
        // A frame's eye (FrameChrome): open while it shows, shut while it's minimized.
        Add("eye", Geometry("M2.062 12.348a1 1 0 0 1 0-.696 10.75 10.75 0 0 1 19.876 0 1 1 0 0 1 0 .696 10.75 10.75 0 0 1-19.876 0M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0z"));
        Add("eye-off", Geometry("M10.733 5.076a10.744 10.744 0 0 1 11.205 6.575 1 1 0 0 1 0 .696 10.747 10.747 0 0 1-1.444 2.49M14.084 14.158a3 3 0 0 1-4.242-4.242M17.479 17.499a10.75 10.75 0 0 1-15.417-5.151 1 1 0 0 1 0-.696 10.75 10.75 0 0 1 4.446-5.143M2 2l20 20"));
        // The meter's navigation: the group frame, the skill monitor, the fight graphs and the Hidden Cubes.
        Add("heart-pulse", Geometry("M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7ZM3.22 12H9.5l.5-1 2 4.5 2-7 1.5 3.5h5.27"));
        Add("timer", Geometry("M10 2h4M12 14l3-3M20 14a8 8 0 1 1-16 0 8 8 0 0 1 16 0z"));
        Add("chart-line", Geometry("M3 3v16a2 2 0 0 0 2 2h16M19 9l-5 5-4-4-3 3"));
        Add("box", Geometry("M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16ZM3.3 7 12 12l8.7-5M12 22V12"));
        Add("pin", Geometry("M12 17v5M5 17h14M15 3.5a4 4 0 0 0-6 0C7.6 4.4 7 6 7 8c0 2-1 3-2 4h14c-1-1-2-2-2-4 0-2-.6-3.6-2-4.5"));
        Add("globe", Geometry("M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20zM2 12h20M12 2a15.3 15.3 0 0 1 0 20M12 2a15.3 15.3 0 0 0 0 20"));
        Add("settings", Geometry("M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1zM15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0z"));
        Add("check", Geometry("M20 6 9 17l-5-5"));
        Add("play", Play);
        Add("pause", Geometry("M6 4h4v16H6zM14 4h4v16h-4z"));
        Add("square", Geometry("M5 5h14v14H5z"));
        Add("chevron-left", Geometry("M15 18l-6-6 6-6"));
        Add("chevron-right", Geometry("M9 18l6-6-6-6"));
        Add("chevron-down", Geometry("M6 9l6 6 6-6"));
        Add("chevron-up", Geometry("M18 15l-6-6-6 6"));
        Add("skip-back", Geometry("M19 20 9 12l10-8v16zM5 19V5"));
        Add("skip-forward", Geometry("M5 4l10 8-10 8V4zM19 5v14"));
    }

    private static StreamGeometry Geometry(string data) => StreamGeometry.Parse(Bbox + data);
}
