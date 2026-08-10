using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DmC.Qa.Tester;

internal static class UiMotion
{
    public static void FadeIn(FrameworkElement element, double fromY = 12)
    {
        var transform = element.RenderTransform as TranslateTransform ?? new TranslateTransform();
        element.RenderTransform = transform;
        element.Opacity = 0;
        transform.Y = fromY;

        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = easing });
        transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(fromY, 0, TimeSpan.FromMilliseconds(320)) { EasingFunction = easing });
    }

    public static void Pulse(FrameworkElement element)
    {
        var transform = element.RenderTransform as ScaleTransform ?? new ScaleTransform(1, 1);
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.RenderTransform = transform;
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var animation = new DoubleAnimation(0.985, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = easing };
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }
}
