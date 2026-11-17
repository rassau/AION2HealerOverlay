using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Cloris.Aion2Flow.Services.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Cloris.Aion2Flow.Services.Overlay;

internal static class NativeOverlayWindowStyles
{
    private const int WsExTopmost = 0x00000008;
    private const int WsExLayered = 0x00080000;
    private const int WsExTransparent = 0x00000020;
    private const int InputTransparentStyles = WsExLayered | WsExTransparent;
    private static readonly HWND TopmostBand = new(-1);
    private static readonly HWND NonTopmostBand = new(-2);
    private static readonly ConditionalWeakTable<TopLevel, InputTransparencyState> InputTransparencyStates = new();

    public static bool SetInputTransparent(TopLevel window, bool enabled)
    {
        if (!TryGetExtendedStyles(window, out var handle, out var hwnd, out var current))
        {
            return false;
        }

        var state = InputTransparencyStates.GetValue(window, static _ => new InputTransparencyState());
        if (state.Handle != handle)
        {
            state.Reset(handle);
        }

        var preserveLayered = state.PreserveLayered;
        if (enabled && !state.IsEnabled)
        {
            preserveLayered = (current & WsExLayered) != 0;
        }

        var updated = enabled
            ? current | InputTransparentStyles
            : current & ~WsExTransparent;
        if (!enabled && state.IsEnabled && !preserveLayered)
        {
            updated &= ~WsExLayered;
        }

        if (!TryApplyWindowStyles(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, current, updated, InputTransparentStyles, out var applied))
        {
            return false;
        }

        var isTransparent = (applied & WsExTransparent) != 0;
        var isLayered = (applied & WsExLayered) != 0;
        if (enabled ? !isTransparent || !isLayered : isTransparent || (state.IsEnabled && isLayered != preserveLayered))
        {
            return false;
        }

        state.IsEnabled = enabled;
        state.PreserveLayered = enabled && preserveLayered;
        return true;
    }

    // Any of the overlay's windows, a popup's too (the ping graph's).
    public static bool SetTopmostBand(TopLevel window, bool enabled)
    {
        if (!TryGetExtendedStyles(window, out _, out var hwnd, out _))
        {
            return false;
        }

        Marshal.SetLastPInvokeError(0);
        if (!PInvoke.SetWindowPos(
            hwnd,
            enabled ? TopmostBand : NonTopmostBand, 0, 0, 0, 0, SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER))
        {
            var error = Marshal.GetLastPInvokeError();
            AppLog.Write(AppLogLevel.Warning, $"Failed to update overlay topmost band: Win32 error {error}");
            return false;
        }

        var styles = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        return ((styles & WsExTopmost) != 0) == enabled;
    }

    public static bool TryGetCursorPosition(out PixelPoint position)
    {
        if (!PInvoke.GetCursorPos(out var cursor))
        {
            position = default;
            return false;
        }

        position = new PixelPoint(cursor.X, cursor.Y);
        return true;
    }

    public static bool IsCurrentProcessForeground()
    {
        var foreground = PInvoke.GetForegroundWindow();
        return foreground != HWND.Null &&
               PInvoke.GetWindowThreadProcessId(foreground, out var processId) != 0 &&
               processId == (uint)Environment.ProcessId;
    }

    public static bool TryGetInputTransparentStyles(Window window, out bool isLayered, out bool isTransparent)
    {
        if (!TryGetExtendedStyles(window, out _, out _, out var styles))
        {
            isLayered = false;
            isTransparent = false;
            return false;
        }

        isLayered = (styles & WsExLayered) != 0;
        isTransparent = (styles & WsExTransparent) != 0;
        return true;
    }

    public static bool TryGetTopmostBand(Window window, out bool isTopmost)
    {
        if (!TryGetExtendedStyles(window, out _, out _, out var styles))
        {
            isTopmost = false;
            return false;
        }

        isTopmost = (styles & WsExTopmost) != 0;
        return true;
    }

    private static bool TryGetExtendedStyles(TopLevel window, out nint handle, out HWND hwnd, out int styles)
    {
        handle = window.TryGetPlatformHandle()?.Handle ?? 0;
        if (handle == 0)
        {
            hwnd = HWND.Null;
            styles = 0;
            return false;
        }

        hwnd = new HWND(handle);
        styles = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        return true;
    }

    private static bool TryApplyWindowStyles(HWND hwnd, WINDOW_LONG_PTR_INDEX index, int current, int updated, int changedMask, out int applied)
    {
        applied = current;
        if (updated == current)
        {
            return true;
        }

        Marshal.SetLastPInvokeError(0);
        var previous = PInvoke.SetWindowLong(hwnd, index, updated);
        var error = Marshal.GetLastPInvokeError();
        if (previous == 0 && error != 0)
        {
            AppLog.Write(AppLogLevel.Warning, $"Failed to update overlay window styles 0x{(uint)changedMask:X8}: Win32 error {error}");
            return false;
        }

        Marshal.SetLastPInvokeError(0);
        if (!PInvoke.SetWindowPos(
            hwnd,
            HWND.Null,
            0,
            0,
            0,
            0,
            SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED |
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE |
            SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
            SET_WINDOW_POS_FLAGS.SWP_NOZORDER |
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE))
        {
            error = Marshal.GetLastPInvokeError();
            AppLog.Write(AppLogLevel.Warning, $"Failed to refresh overlay window styles 0x{(uint)changedMask:X8}: Win32 error {error}");
            RollbackWindowStyles(hwnd, index, current, changedMask);
            applied = PInvoke.GetWindowLong(hwnd, index);
            return false;
        }

        applied = PInvoke.GetWindowLong(hwnd, index);
        return true;
    }

    private static void RollbackWindowStyles(HWND hwnd, WINDOW_LONG_PTR_INDEX index, int previous, int changedMask)
    {
        Marshal.SetLastPInvokeError(0);
        var replaced = PInvoke.SetWindowLong(hwnd, index, previous);
        var error = Marshal.GetLastPInvokeError();
        if (replaced == 0 && error != 0)
        {
            AppLog.Write(AppLogLevel.Error, $"Failed to roll back overlay window styles 0x{(uint)changedMask:X8}: Win32 error {error}");
            return;
        }

        Marshal.SetLastPInvokeError(0);
        if (!PInvoke.SetWindowPos(
            hwnd,
            HWND.Null,
            0,
            0,
            0,
            0,
            SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED |
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE |
            SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
            SET_WINDOW_POS_FLAGS.SWP_NOZORDER |
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE))
        {
            error = Marshal.GetLastPInvokeError();
            AppLog.Write(AppLogLevel.Error, $"Failed to refresh rolled-back overlay window styles 0x{(uint)changedMask:X8}: Win32 error {error}");
        }
    }

    private sealed class InputTransparencyState
    {
        public nint Handle { get; private set; }

        public bool IsEnabled { get; set; }

        public bool PreserveLayered { get; set; }

        public void Reset(nint handle)
        {
            Handle = handle;
            IsEnabled = false;
            PreserveLayered = false;
        }
    }
}
