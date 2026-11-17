using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
namespace EagleEyes.App;

internal static class WindowDragBehavior
{
    internal static void Attach(Window window, Action? afterDrag = null)
    {
        Point? origin = null; bool dragging = false;
        window.PreviewMouseLeftButtonDown += (_, e) =>
        {
            origin = IsInteractive(e.OriginalSource as DependencyObject) ? null : e.GetPosition(window);
        };
        window.PreviewMouseLeftButtonUp += (_, _) => origin = null;
        window.PreviewMouseMove += (_, e) =>
        {
            if (dragging || origin == null || e.LeftButton != MouseButtonState.Pressed) return;
            var current = e.GetPosition(window);
            if (Math.Abs(current.X - origin.Value.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(current.Y - origin.Value.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            origin = null; dragging = true;
            try { window.DragMove(); Native.ClampToWorkArea(window); afterDrag?.Invoke(); }
            catch (InvalidOperationException) { }
            finally { dragging = false; }
        };
        window.Deactivated += (_, _) => origin = null;
    }
    static bool IsInteractive(DependencyObject? element)
    {
        while (element != null)
        {
            if (element is ButtonBase or TextBoxBase or PasswordBox or Selector or Slider or ScrollBar or MenuBase or MenuItem or Hyperlink) return true;
            if (element is Visual or Visual3D) element = VisualTreeHelper.GetParent(element);
            else element = LogicalTreeHelper.GetParent(element);
        }
        return false;
    }
}
