using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;

namespace EagleEyes.App;

/// <summary>WPF tooltips have a separate popup HWND and do not inherit the owner's display affinity.</summary>
internal static class CaptureSafeToolTip
{
    static readonly Lazy<Style> hintStyle = new(() =>
        (Style)VisualIdentity.CreateResources()["EagleEyes.ToolTip.Hint"]);

    internal static void SetHint(Button button, string text)
    {
        var tip = (ToolTip)button.ToolTip;
        tip.Style = hintStyle.Value;
        tip.Content = text;
        AutomationProperties.SetName(tip, text);
        AutomationProperties.SetHelpText(button, text);
    }

    internal static ToolTip Create(string text, bool excludeFromCapture)
    {
        var tooltip = new ToolTip
        {
            Content = text,
            HasDropShadow = false,
            // Do not paint the first popup frame before its own HWND has been excluded.
            Opacity = excludeFromCapture ? 0 : 1
        };
        AutomationProperties.SetName(tooltip, text);
        AutomationProperties.SetAutomationId(tooltip, "CaptureSafeToolTip");
        tooltip.Opened += (_, _) =>
        {
            try
            {
                if (excludeFromCapture)
                {
                    var source = PresentationSource.FromVisual(tooltip) as HwndSource;
                    if (source == null || !Native.SetWindowDisplayAffinity(source.Handle, 0x11))
                        AppLog.Write("tooltip-capture-exclusion", details: $"Popup affinity unavailable; HWND={source?.Handle}; Win32={Marshal.GetLastWin32Error()}");
                }
            }
            catch (Exception error) { AppLog.Write("tooltip-capture-exclusion", error); }
            finally { tooltip.Opacity = 1; }
        };
        tooltip.Closed += (_, _) => { if (excludeFromCapture) tooltip.Opacity = 0; };
        return tooltip;
    }
}