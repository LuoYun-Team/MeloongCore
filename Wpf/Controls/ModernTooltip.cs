using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
namespace MeloongCore.Wpf;

public static class ModernToolTipService {
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(ModernToolTipService), new PropertyMetadata(true));

    public static readonly DependencyProperty FollowMouseProperty = DependencyProperty.RegisterAttached(
        "FollowMouse", typeof(bool), typeof(ModernToolTipService), new PropertyMetadata(true));

    private static readonly TimeSpan animationTime = TimeSpan.FromMilliseconds(70);
    private static readonly int shadowRadius = 18;
    private static readonly float shadowOpacity = 0.15f;
    private static readonly Thickness contentMargin = new(12, 11, 12, 8.5);
    private static readonly Brush backgroundBrush = new SolidColorBrush(Colors.White);
    private static readonly Brush borderBrush = new SolidColorBrush(Color.FromRgb(0xD6, 0xD6, 0xD6));
    private static readonly Brush foregroundBrush = new SolidColorBrush(Color.FromRgb(0x52, 0x52, 0x52));
    private static readonly DispatcherTimer openTimer = new();

    private static bool initialized;
    private static int animationToken;
    private static Point lastMousePoint;
    private static Popup? popup;
    private static Border? popupCard;
    private static ScaleTransform? popupScale;
    private static FrameworkElement? currentOwner;
    private static ToolTip? borrowedToolTip;
    private static object? borrowedContent;

    static ModernToolTipService() {
        openTimer.Tick += (_, _) => {
            openTimer.Stop();
            if (currentOwner is not null) Show(currentOwner, lastMousePoint);
        };
    }

    /// <summary>
    /// 全局启用现代 Tooltip。启用后会接管所有 <see cref="FrameworkElement.ToolTip"/>。
    /// </summary>
    public static void Initialize() {
        if (initialized) return;
        initialized = true;

        EventManager.RegisterClassHandler(typeof(FrameworkElement), UIElement.MouseEnterEvent, new MouseEventHandler(OnMouseEnter), true);
        EventManager.RegisterClassHandler(typeof(FrameworkElement), UIElement.MouseMoveEvent, new MouseEventHandler(OnMouseMove), true);
        EventManager.RegisterClassHandler(typeof(FrameworkElement), UIElement.MouseLeaveEvent, new MouseEventHandler(OnMouseLeave), true);
        EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.UnloadedEvent, new RoutedEventHandler(OnUnloaded), true);
        EventManager.RegisterClassHandler(typeof(FrameworkElement), ToolTipService.ToolTipOpeningEvent, new ToolTipEventHandler(OnNativeToolTipOpening), true);
    }

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element) => (bool) element.GetValue(IsEnabledProperty);

    public static void SetFollowMouse(DependencyObject element, bool value) => element.SetValue(FollowMouseProperty, value);

    public static bool GetFollowMouse(DependencyObject element) => (bool) element.GetValue(FollowMouseProperty);

    private static void OnMouseEnter(object sender, MouseEventArgs e) {
        if (sender is not FrameworkElement owner || !GetIsEnabled(owner) || GetToolTipContent(owner) is null) return;
        if (ReferenceEquals(currentOwner, owner) && popup is { IsOpen: true }) return;

        if (!ReferenceEquals(currentOwner, owner)) HideImmediately();

        currentOwner = owner;
        lastMousePoint = e.GetPosition(owner);
        openTimer.Stop();

        var delay = Math.Max(0, ToolTipService.GetInitialShowDelay(owner));
        if (delay == 0) {
            Show(owner, lastMousePoint);
        } else {
            openTimer.Interval = TimeSpan.FromMilliseconds(delay);
            openTimer.Start();
        }
    }

    private static void OnMouseMove(object sender, MouseEventArgs e) {
        if (sender is not FrameworkElement owner || !ReferenceEquals(owner, currentOwner)) return;

        lastMousePoint = e.GetPosition(owner);
        if (GetFollowMouse(owner) && popup is { IsOpen: true }) UpdatePosition(owner, lastMousePoint);
    }

    private static void OnMouseLeave(object sender, MouseEventArgs e) {
        if (sender is FrameworkElement owner && ReferenceEquals(owner, currentOwner)) Close(true);
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e) {
        if (sender is FrameworkElement owner && ReferenceEquals(owner, currentOwner)) Close(false);
    }

    private static void OnNativeToolTipOpening(object sender, ToolTipEventArgs e) {
        if (sender is FrameworkElement owner && GetIsEnabled(owner) && GetToolTipContent(owner) is not null) e.Handled = true;
    }

    private static object? GetToolTipContent(FrameworkElement owner) {
        var raw = owner.ToolTip;
        var content = raw is ToolTip toolTip ? toolTip.Content : raw;
        return content is string text && text.Length == 0 ? null : content;
    }

    private static void Show(FrameworkElement owner, Point point) {
        if (!ReferenceEquals(owner, currentOwner)) return;

        var raw = owner.ToolTip;
        var sourceToolTip = raw as ToolTip;
        var content = sourceToolTip?.Content ?? raw;
        if (content is null) return;
        if (content is string text && text.Length == 0) return;

        EnsurePopup();

        popup!.PlacementTarget = owner;
        popupCard!.DataContext = sourceToolTip?.DataContext ?? owner.DataContext;
        popupCard.FlowDirection = owner.FlowDirection;
        UpdatePosition(owner, point);

        popupCard.Child = null;
        RestoreBorrowedContent();

        if (sourceToolTip is not null && content is DependencyObject contentObject && ReferenceEquals(LogicalTreeHelper.GetParent(contentObject), sourceToolTip)) {
            borrowedToolTip = sourceToolTip;
            borrowedContent = content;
            sourceToolTip.Content = null;
        }

        popupCard.Child = content is string stringContent && sourceToolTip?.ContentTemplate is null && sourceToolTip?.ContentTemplateSelector is null
            ? new TextBlock {
                Text = stringContent,
                TextWrapping = TextWrapping.Wrap,
                Foreground = foregroundBrush,
                Margin = contentMargin,
                FontSize = 12.5,
                LineHeight = 17,
                MaxWidth = 676
            }
            : new ContentPresenter {
                Content = content,
                ContentTemplate = sourceToolTip?.ContentTemplate,
                ContentTemplateSelector = sourceToolTip?.ContentTemplateSelector,
                ContentStringFormat = sourceToolTip?.ContentStringFormat,
                Margin = contentMargin,
                MaxWidth = 676
            };

        animationToken++;
        popupCard.Opacity = 0;
        popupScale!.ScaleX = 0.97;
        popupScale.ScaleY = 0.97;
        popup.IsOpen = true;
        Animate(1, 1, null);
    }

    private static void Close(bool animated) {
        openTimer.Stop();
        currentOwner = null;

        if (popup is null || popupCard is null) return;
        if (!popup.IsOpen || !animated) {
            HideImmediately();
            return;
        }

        var token = ++animationToken;
        Animate(0, 0.97, (_, _) => {
            if (token != animationToken) return;
            HideImmediately();
        });
    }

    private static void HideImmediately() {
        animationToken++;

        if (popup is not null) popup.IsOpen = false;
        if (popupCard is not null) {
            popupCard.BeginAnimation(UIElement.OpacityProperty, null);
            popupCard.Child = null;
        }

        if (popupScale is not null) {
            popupScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            popupScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        }

        RestoreBorrowedContent();
    }

    private static void RestoreBorrowedContent() {
        if (borrowedToolTip is null) return;

        borrowedToolTip.Content = borrowedContent;
        borrowedToolTip = null;
        borrowedContent = null;
    }

    private static void EnsurePopup() {
        if (popup is not null) return;

        popupScale = new ScaleTransform(0.97, 0.97);
        popupCard = new Border {
            Background = backgroundBrush,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            MaxWidth = 700,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
            RenderTransform = popupScale,
            RenderTransformOrigin = new Point(0, 0),
            Effect = new DropShadowEffect {
                Opacity = shadowOpacity,
                BlurRadius = shadowRadius,
                ShadowDepth = 0,
                Color = Colors.Black
            }
        };

        var root = new Grid {
            Margin = new Thickness(shadowRadius + 1),
            SnapsToDevicePixels = true,
            UseLayoutRounding = true
        };
        root.Children.Add(popupCard);

        popup = new Popup {
            AllowsTransparency = true,
            IsHitTestVisible = false,
            StaysOpen = true,
            PopupAnimation = PopupAnimation.None,
            Placement = PlacementMode.Relative,
            Child = root
        };
    }

    private static void UpdatePosition(FrameworkElement owner, Point point) {
        popup!.PlacementTarget = owner;
        popup.HorizontalOffset = Math.Round(point.X + 25);
        popup.VerticalOffset = Math.Round(point.Y + 20);
    }

    private static void Animate(double opacity, double scale, EventHandler? completed) {
        var opacityAnimation = new DoubleAnimation(opacity, new Duration(animationTime)) {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        if (completed is not null) opacityAnimation.Completed += completed;

        var scaleAnimation = new DoubleAnimation(scale, new Duration(animationTime)) {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        popupCard!.BeginAnimation(UIElement.OpacityProperty, opacityAnimation, HandoffBehavior.SnapshotAndReplace);
        popupScale!.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation, HandoffBehavior.SnapshotAndReplace);
        popupScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation, HandoffBehavior.SnapshotAndReplace);
    }
}