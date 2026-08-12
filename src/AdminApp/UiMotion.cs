using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DmC.Qa.Admin;

internal static class UiMotion
{
    public static void Reveal(FrameworkElement element, double fromX = 12)
    {
        var transform = element.RenderTransform as TranslateTransform ?? new TranslateTransform();
        element.RenderTransform = transform;
        element.Opacity = 0;
        transform.X = fromX;
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(230)) { EasingFunction = easing });
        transform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(fromX, 0, TimeSpan.FromMilliseconds(300)) { EasingFunction = easing });
    }

    public static void Pulse(FrameworkElement element)
    {
        var transform = element.RenderTransform as ScaleTransform ?? new ScaleTransform(1, 1);
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.RenderTransform = transform;
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var animation = new DoubleAnimation(0.99, 1, TimeSpan.FromMilliseconds(190)) { EasingFunction = easing };
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }
}
