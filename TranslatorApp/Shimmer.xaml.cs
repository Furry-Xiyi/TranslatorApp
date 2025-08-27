using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Animations.Expressions;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Numerics;
using Windows.UI; // for Color

namespace TranslatorApp;

[TemplatePart(Name = PART_Shape, Type = typeof(Border))]
public partial class Shimmer : ContentControl
{
    private const float InitialStartPointX = -7.92f;
    private const string PART_Shape = "Shape";

    private Vector2Node? _sizeAnimation;
    private Vector2KeyFrameAnimation? _gradientStartPointAnimation;
    private Vector2KeyFrameAnimation? _gradientEndPointAnimation;
    private CompositionColorGradientStop? _gradientStop1;
    private CompositionColorGradientStop? _gradientStop2;
    private CompositionColorGradientStop? _gradientStop3;
    private CompositionColorGradientStop? _gradientStop4;
    private CompositionRoundedRectangleGeometry? _rectangleGeometry;
    private ShapeVisual? _shapeVisual;
    private CompositionLinearGradientBrush? _shimmerMaskGradient;
    private Border? _shape;

    private bool _initialized;
    private bool _animationStarted;

    // Track base Control.CornerRadius callback token
    private long _cornerRadiusCallbackToken;

    public Shimmer()
    {
        DefaultStyleKey = typeof(Shimmer);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_shape != null)
        {
            _shape.SizeChanged -= OnShapeSizeChanged;
        }

        _shape = GetTemplateChild(PART_Shape) as Border;
        if (_shape != null)
        {
            _shape.SizeChanged += OnShapeSizeChanged;
        }

        // Re-hook CornerRadius callback (template may reapply)
        EnsureCornerRadiusCallback();

        if (!_initialized && TryInitializationResource() && IsActive)
        {
            TryStartAnimation();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        EnsureCornerRadiusCallback();

        if (!_initialized && TryInitializationResource() && IsActive)
        {
            TryStartAnimation();
        }

        ActualThemeChanged += OnActualThemeChanged;
        UpdateVisualSize(); // 首次确保尺寸同步
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ActualThemeChanged -= OnActualThemeChanged;

        if (_shape != null)
        {
            _shape.SizeChanged -= OnShapeSizeChanged;
        }

        // Unhook CornerRadius callback
        if (_cornerRadiusCallbackToken != 0)
        {
            try { UnregisterPropertyChangedCallback(Control.CornerRadiusProperty, _cornerRadiusCallbackToken); }
            catch { }
            _cornerRadiusCallbackToken = 0;
        }

        StopAnimation();

        if (_initialized && _shape != null)
        {
            ElementCompositionPreview.SetElementChildVisual(_shape, null);

            _rectangleGeometry!.Dispose();
            _shapeVisual!.Dispose();
            _shimmerMaskGradient!.Dispose();
            _gradientStop1!.Dispose();
            _gradientStop2!.Dispose();
            _gradientStop3!.Dispose();
            _gradientStop4!.Dispose();

            _initialized = false;
        }
    }

    private void EnsureCornerRadiusCallback()
    {
        if (_cornerRadiusCallbackToken == 0)
        {
            _cornerRadiusCallbackToken = RegisterPropertyChangedCallback(
                Control.CornerRadiusProperty,
                OnCornerRadiusDpChanged);
        }
    }

    private void OnCornerRadiusDpChanged(DependencyObject sender, DependencyProperty dp)
    {
        if (!_initialized || _rectangleGeometry is null) return;
        _rectangleGeometry.CornerRadius = new Vector2((float)CornerRadius.TopLeft);
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        if (!_initialized) return;
        SetGradientStopColorsByTheme();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateVisualSize();
    }

    private void OnShapeSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateVisualSize();
    }

    private bool TryInitializationResource()
    {
        if (_initialized) return true;
        if (_shape is null || !IsLoaded) return false;

        try
        {
            var compositor = _shape.GetVisual().Compositor;

            _rectangleGeometry = compositor.CreateRoundedRectangleGeometry();
            _shapeVisual = compositor.CreateShapeVisual();
            _shapeVisual.Offset = Vector3.Zero;
            _shapeVisual.RelativeSizeAdjustment = Vector2.One; // 自动跟随宿主尺寸

            _shimmerMaskGradient = compositor.CreateLinearGradientBrush();
            _gradientStop1 = compositor.CreateColorGradientStop();
            _gradientStop2 = compositor.CreateColorGradientStop();
            _gradientStop3 = compositor.CreateColorGradientStop();
            _gradientStop4 = compositor.CreateColorGradientStop();

            SetGradientAndStops();
            SetGradientStopColorsByTheme();

            // 初始圆角来自基类 Control.CornerRadius
            _rectangleGeometry.CornerRadius = new Vector2((float)CornerRadius.TopLeft);

            var spriteShape = compositor.CreateSpriteShape(_rectangleGeometry);
            spriteShape.FillBrush = _shimmerMaskGradient;
            _shapeVisual.Shapes.Add(spriteShape);

            ElementCompositionPreview.SetElementChildVisual(_shape, _shapeVisual);

            _initialized = true;
            UpdateVisualSize(); // 初始化后立刻同步一次尺寸
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Shimmer] 初始化 Composition 资源失败: {ex}");
            _initialized = false;
            return false;
        }
    }

    private void SetGradientAndStops()
    {
        // 横向穿越，纵向填满
        _shimmerMaskGradient!.StartPoint = new Vector2(InitialStartPointX, 0.0f);
        _shimmerMaskGradient.EndPoint = new Vector2(0.0f, 1.0f);

        // 更宽的亮带（更柔和）：0.35 ~ 0.65
        _gradientStop1!.Offset = 0.20f; // 左侧弱
        _gradientStop2!.Offset = 0.35f; // 亮带起
        _gradientStop3!.Offset = 0.65f; // 亮带止
        _gradientStop4!.Offset = 0.80f; // 右侧弱

        _shimmerMaskGradient.ColorStops.Add(_gradientStop1);
        _shimmerMaskGradient.ColorStops.Add(_gradientStop2);
        _shimmerMaskGradient.ColorStops.Add(_gradientStop3);
        _shimmerMaskGradient.ColorStops.Add(_gradientStop4);
    }

    private void SetGradientStopColorsByTheme()
    {
        byte aSide, aCenter;
        byte r, g, b;

        if (ActualTheme == ElementTheme.Light)
        {
            // 浅色主题：亮灰
            r = g = b = 255;
            aSide = (byte)(255 * 0.25);   // 两侧 25%
            aCenter = (byte)(255 * 0.70); // 中间 70%
        }
        else
        {
            // 深色主题：亮白
            r = g = b = 255;
            aSide = (byte)(255 * 0.20);   // 两侧 20%
            aCenter = (byte)(255 * 0.50); // 中间 65%
        }

        _gradientStop1!.Color = Color.FromArgb(aSide, r, g, b);
        _gradientStop2!.Color = Color.FromArgb(aCenter, r, g, b);
        _gradientStop3!.Color = Color.FromArgb(aCenter, r, g, b);
        _gradientStop4!.Color = Color.FromArgb(aSide, r, g, b);
    }

    private void TryStartAnimation()
    {
        if (_animationStarted || !_initialized || _shape is null || _shapeVisual is null || _rectangleGeometry is null)
            return;

        try
        {
            var rootVisual = _shape.GetVisual();

            // 尺寸动画：绑定到宿主 Size，避免首次错位
            _sizeAnimation = rootVisual.GetReference().Size;
            _shapeVisual.StartAnimation(nameof(ShapeVisual.Size), _sizeAnimation);
            _rectangleGeometry.StartAnimation(nameof(CompositionRoundedRectangleGeometry.Size), _sizeAnimation);

            // 同步圆角
            _rectangleGeometry.CornerRadius = new Vector2((float)CornerRadius.TopLeft);

            // 渐变扫动
            _gradientStartPointAnimation = rootVisual.Compositor.CreateVector2KeyFrameAnimation();
            _gradientStartPointAnimation.Duration = Duration;
            _gradientStartPointAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
            _gradientStartPointAnimation.InsertKeyFrame(0.0f, new Vector2(InitialStartPointX, 0.0f));
            _gradientStartPointAnimation.InsertKeyFrame(1.0f, Vector2.Zero);
            _shimmerMaskGradient!.StartAnimation(nameof(CompositionLinearGradientBrush.StartPoint), _gradientStartPointAnimation);

            _gradientEndPointAnimation = rootVisual.Compositor.CreateVector2KeyFrameAnimation();
            _gradientEndPointAnimation.Duration = Duration;
            _gradientEndPointAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
            _gradientEndPointAnimation.InsertKeyFrame(0.0f, new Vector2(1.0f, 0.0f));
            _gradientEndPointAnimation.InsertKeyFrame(1.0f, new Vector2(-InitialStartPointX, 1.0f));
            _shimmerMaskGradient.StartAnimation(nameof(CompositionLinearGradientBrush.EndPoint), _gradientEndPointAnimation);

            _animationStarted = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Shimmer] 启动动画失败: {ex}");
            _animationStarted = false;
        }
    }

    private void UpdateVisualSize()
    {
        if (!_initialized || _shapeVisual is null || _rectangleGeometry is null || _shape is null)
            return;

        float w = (float)(_shape.ActualWidth > 0 ? _shape.ActualWidth : ActualWidth);
        float h = (float)(_shape.ActualHeight > 0 ? _shape.ActualHeight : ActualHeight);

        if (w <= 0 || h <= 0) return;

        var size = new Vector2(w, h);

        // 立即对齐一次，避免首帧错位
        _shapeVisual.Size = size;
        _rectangleGeometry.Size = size;

        // 圆角保持一致
        _rectangleGeometry.CornerRadius = new Vector2((float)CornerRadius.TopLeft);
    }

    private void StopAnimation()
    {
        if (!_animationStarted) return;

        try
        {
            _shapeVisual?.StopAnimation(nameof(ShapeVisual.Size));
            _rectangleGeometry?.StopAnimation(nameof(CompositionRoundedRectangleGeometry.Size));
            _shimmerMaskGradient?.StopAnimation(nameof(CompositionLinearGradientBrush.StartPoint));
            _shimmerMaskGradient?.StopAnimation(nameof(CompositionLinearGradientBrush.EndPoint));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Shimmer] 停止动画失败: {ex}");
        }

        _sizeAnimation?.Dispose();
        _sizeAnimation = null;

        _gradientStartPointAnimation?.Dispose();
        _gradientStartPointAnimation = null;

        _gradientEndPointAnimation?.Dispose();
        _gradientEndPointAnimation = null;

        _animationStarted = false;
    }

    // 动画时长依赖属性
    public static readonly DependencyProperty DurationProperty =
        DependencyProperty.Register(
            nameof(Duration),
            typeof(TimeSpan),
            typeof(Shimmer),
            new PropertyMetadata(TimeSpan.FromMilliseconds(1800), OnPropertyChanged));

    // 是否播放动画依赖属性
    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(
            nameof(IsActive),
            typeof(bool),
            typeof(Shimmer),
            new PropertyMetadata(true, OnPropertyChanged));

    public TimeSpan Duration
    {
        get => (TimeSpan)GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (Shimmer)d;
        if (self.IsActive)
        {
            self.StopAnimation();
            self.TryStartAnimation();
        }
        else
        {
            self.StopAnimation();
        }
    }
}