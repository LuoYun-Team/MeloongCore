// 此文件主体由 AI 生成，应作为独立模块，尽量减少与其他内容的耦合。

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace MeloongCore.Wpf;
public static class ModernTooltipService {

    // 控制是否在控件上启用现代化 Tooltip；默认为 true。
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
    public static bool GetIsEnabled(DependencyObject element) => (bool) element.GetValue(IsEnabledProperty);

    // 控制 Tooltip 是否跟随鼠标移动；默认为 true。
    public static void SetFollowMouse(DependencyObject element, bool value) => element.SetValue(FollowMouseProperty, value);
    public static bool GetFollowMouse(DependencyObject element) => (bool) element.GetValue(FollowMouseProperty);

    // 样式配置
    private static readonly TimeSpan animationTime = TimeSpan.FromMilliseconds(70);
    private static readonly int shadowRadius = 18;
    private static readonly float shadowOpacity = 0.15f;
    private static readonly Thickness contentMargin = new(12, 11, 12, 8.5);
    private static readonly Brush backgroundBrush = new SolidColorBrush(Colors.White);
    private static readonly Brush borderBrush = new SolidColorBrush(Color.FromRgb(0xD6, 0xD6, 0xD6));
    private static readonly Brush foregroundBrush = new SolidColorBrush(Color.FromRgb(0x52, 0x52, 0x52));

    /// <summary>
    /// 全局启用现代 Tooltip。启用后会接管所有 <see cref="FrameworkElement.ToolTip"/>。
    /// </summary>
    internal static void Init() {
        if (initialized) return;
        initialized = true;

        EventManager.RegisterClassHandler(typeof(FrameworkElement), UIElement.MouseEnterEvent, new MouseEventHandler(OnMouseEnter), true);
        EventManager.RegisterClassHandler(typeof(FrameworkElement), UIElement.MouseMoveEvent, new MouseEventHandler(OnMouseMove), true);
        EventManager.RegisterClassHandler(typeof(FrameworkElement), UIElement.MouseLeaveEvent, new MouseEventHandler(OnMouseLeave), true);

        EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.UnloadedEvent, new RoutedEventHandler(static (sender, _) => {
            if (sender is FrameworkElement owner && ReferenceEquals(owner, currentOwner)) Close(false);
        }), true);

        EventManager.RegisterClassHandler(typeof(FrameworkElement), ToolTipService.ToolTipOpeningEvent, new ToolTipEventHandler(static (sender, e) => {
            if (sender is FrameworkElement owner && GetIsEnabled(owner) && TryGetToolTip(owner, out _, out _)) e.Handled = true;
        }), true);

        // Loaded 负责常规路径；PreviewMouseDown 兜底处理 Init 晚于控件 Loaded 的情况。
        EventManager.RegisterClassHandler(typeof(ComboBox), FrameworkElement.LoadedEvent, new RoutedEventHandler(static (sender, _) => HookComboBoxDropDown(sender as ComboBox)), true);
        EventManager.RegisterClassHandler(typeof(ComboBox), UIElement.PreviewMouseDownEvent, new MouseButtonEventHandler(static (sender, _) => HookComboBoxDropDown(sender as ComboBox)), true);
    }

    #region 附加属性与运行状态

    // 附加属性
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(ModernTooltipService), new PropertyMetadata(true));
    public static readonly DependencyProperty FollowMouseProperty = DependencyProperty.RegisterAttached(
        "FollowMouse", typeof(bool), typeof(ModernTooltipService), new PropertyMetadata(true));
    private static readonly DependencyProperty IsComboBoxDropDownHookedProperty = DependencyProperty.RegisterAttached(
        "IsComboBoxDropDownHooked", typeof(bool), typeof(ModernTooltipService), new PropertyMetadata(false));

    private static readonly DispatcherTimer openTimer = new();
    private static readonly DispatcherTimer leaveWatchTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };

    private static bool initialized;
    private static int animationToken;
    private static Point lastMousePoint;
    private static Popup? popup;
    private static Border? popupCard;
    private static ScaleTransform? popupScale;
    private static FrameworkElement? currentOwner;
    private static ToolTip? borrowedToolTip;
    private static object? borrowedContent;

    static ModernTooltipService() {
        openTimer.Tick += (_, _) => {
            openTimer.Stop();
            if (currentOwner is not null) Show(currentOwner, lastMousePoint);
        };

        leaveWatchTimer.Tick += (_, _) => {
            if (currentOwner is null) {
                leaveWatchTimer.Stop();
                return;
            }

            if (TryGetCursorPoint(currentOwner, out var point)) {
                lastMousePoint = point;
                if (GetFollowMouse(currentOwner) && popup is { IsOpen: true }) UpdatePosition(currentOwner, point);
                if (currentOwner.IsMouseOver) leaveWatchTimer.Stop();
                return;
            }

            Close(true);
        };
    }

    #endregion

    #region 全局鼠标事件

    private static void OnMouseEnter(object sender, MouseEventArgs e) {
        if (sender is not FrameworkElement owner || !GetIsEnabled(owner) || !TryGetToolTip(owner, out _, out _)) return;
        if (ReferenceEquals(currentOwner, owner) && popup is { IsOpen: true }) {
            leaveWatchTimer.Stop();
            return;
        }

        if (owner is ComboBox comboBox) HookComboBoxDropDown(comboBox);
        if (!ReferenceEquals(currentOwner, owner)) HideImmediately();

        currentOwner = owner;
        lastMousePoint = e.GetPosition(owner);
        openTimer.Stop();
        leaveWatchTimer.Stop();

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
        if (sender is not FrameworkElement owner || !ReferenceEquals(owner, currentOwner)) return;

        // TextBox 点击时，内部元素可能短暂捕获鼠标并触发一次假的 MouseLeave。
        // 若确认鼠标还在控件内，则短暂轮询；避免 WPF 不再补发真正 MouseLeave 时 Tooltip 残留。
        owner.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => {
            if (!ReferenceEquals(owner, currentOwner)) return;

            if (TryGetCursorPoint(owner, out _)) {
                leaveWatchTimer.Start();
                return;
            }

            Close(true);
        }));
    }

    // 确认鼠标位置
    private static bool TryGetCursorPoint(FrameworkElement owner, out Point point) {
        point = default;
        if (!owner.IsLoaded || !owner.IsVisible || !GetCursorPos(out var cursor)) return false;

        point = owner.PointFromScreen(new Point(cursor.X, cursor.Y));
        return point.X >= 0 && point.Y >= 0 && point.X <= owner.ActualWidth && point.Y <= owner.ActualHeight;
    }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out CursorPoint point);
    [StructLayout(LayoutKind.Sequential)]
    private struct CursorPoint {
        public int X;
        public int Y;
    }

    #endregion

    #region Tooltip 生命周期

    private static bool TryGetToolTip(FrameworkElement owner, out object? content, out ToolTip? sourceToolTip) {
        var raw = owner.ToolTip;
        sourceToolTip = raw as ToolTip;
        content = sourceToolTip?.Content ?? raw;
        return content is not null && (content is not string text || text.Length > 0);
    }

    private static void Show(FrameworkElement owner, Point point) {
        if (!ReferenceEquals(owner, currentOwner) || !TryGetToolTip(owner, out var content, out var sourceToolTip)) return;

        EnsurePopup();

        popup!.PlacementTarget = owner;
        popupCard!.DataContext = sourceToolTip?.DataContext ?? owner.DataContext;
        popupCard.FlowDirection = owner.FlowDirection;
        UpdatePosition(owner, point);

        popupCard.Child = null;
        RestoreBorrowedContent();

        // 如果原 ToolTip.Content 是 Visual/FrameworkElement，必须先从原 ToolTip 借出，
        // 否则同一个元素会同时拥有两个逻辑父级，导致 WPF 抛异常。
        if (sourceToolTip is not null && content is DependencyObject contentObject && ReferenceEquals(LogicalTreeHelper.GetParent(contentObject), sourceToolTip)) {
            borrowedToolTip = sourceToolTip;
            borrowedContent = content;
            sourceToolTip.Content = null;
        }

        popupCard.Child = content is string stringContent && sourceToolTip?.ContentTemplate is null && sourceToolTip?.ContentTemplateSelector is null
            ? new TextBlock { 
                Text = stringContent, TextWrapping = TextWrapping.Wrap, Foreground = foregroundBrush, Margin = contentMargin, 
                FontSize = 12.5, LineHeight = 17, MaxWidth = 676 }
            : new ContentPresenter { 
                Content = content, ContentTemplate = sourceToolTip?.ContentTemplate, ContentTemplateSelector = sourceToolTip?.ContentTemplateSelector,
                ContentStringFormat = sourceToolTip?.ContentStringFormat, Margin = contentMargin, MaxWidth = 676 };

        animationToken++;
        popupCard.Opacity = 0;
        popupScale!.ScaleX = 0.97; popupScale.ScaleY = 0.97;
        popup.IsOpen = true;
        Animate(1, 1, null);
    }

    private static void Close(bool animated) {
        openTimer.Stop();
        leaveWatchTimer.Stop();
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
        openTimer.Stop();
        leaveWatchTimer.Stop();
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

    #endregion

    #region Popup 创建、定位与动画

    private static void EnsurePopup() {
        if (popup is not null) return;

        popupScale = new ScaleTransform(0.97, 0.97);
        popupCard = new Border { 
            Background = backgroundBrush, BorderBrush = borderBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), 
            MaxWidth = 700, SnapsToDevicePixels = true, UseLayoutRounding = true, 
            RenderTransform = popupScale, RenderTransformOrigin = new Point(0, 0), 
            Effect = new DropShadowEffect { Opacity = shadowOpacity, BlurRadius = shadowRadius, ShadowDepth = 0, Color = Colors.Black } };

        var root = new Grid { Margin = new Thickness(shadowRadius + 1), SnapsToDevicePixels = true, UseLayoutRounding = true };
        root.Children.Add(popupCard);

        popup = new Popup { 
            AllowsTransparency = true, IsHitTestVisible = false, StaysOpen = true, PopupAnimation = PopupAnimation.None, 
            Placement = PlacementMode.Relative, Child = root };
    }

    private static void UpdatePosition(FrameworkElement owner, Point point) {
        popup!.PlacementTarget = owner;
        popup.HorizontalOffset = Math.Round(point.X + 15);
        popup.VerticalOffset = Math.Round(point.Y + 25);
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

    #endregion

    // WPF 原生 Tooltip 会在 ComboBox 下拉弹出时关闭；这里保持同样行为。
    private static void HookComboBoxDropDown(ComboBox? comboBox) {
        if (comboBox is null || (bool) comboBox.GetValue(IsComboBoxDropDownHookedProperty)) return;
        comboBox.SetValue(IsComboBoxDropDownHookedProperty, true);
        comboBox.DropDownOpened += static (_, _) => { if (currentOwner is not null) Close(true); };
    }

}
