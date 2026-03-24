using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LinkingMountains.ImageZoom
{
    /// <summary>
    /// A control that allows the user to zoom and pan an image.
    /// </summary>
    [TemplatePart(Name = PartImageContainerName, Type = typeof(Grid))]
    [TemplatePart(Name = PartImageName, Type = typeof(Image))]
    [TemplatePart(Name = PartScaleTransformName, Type = typeof(ScaleTransform))]
    [TemplatePart(Name = PartTranslateTransformName, Type = typeof(TranslateTransform))]
    [TemplatePart(Name = PartScaleTextBorderName, Type = typeof(Border))]
    public class ImageZoom : Control
    {
        private const string PartImageContainerName = "PART_ImageContainer";
        private const string PartImageName = "PART_Image";
        private const string PartScaleTransformName = "PART_ScaleTransform";
        private const string PartTranslateTransformName = "PART_TranslateTransform";
        private const string PartScaleTextBorderName = "PART_ScaleTextBorder";

        private const double ZoomFactorDefault = 1.2;
        private const double MinZoomRatioDefault = 0.01;
        private const double MaxZoomRatioDefault = 50;
        private const double ZoomRatioDefault = 1;
        private const double TranslateXDefault = 0;
        private const double TranslateYDefault = 0;
        private const double TranslateOriginXDefault = 0.5;
        private const double TranslateOriginYDefault = 0.5;
        private const int AnimationDurationMs = 200;
        private const int ZoomValueHintAnimationDurationMs = 1000;

        private Grid _imageContainer;
        private Image _image;
        private ScaleTransform _scaleTransform;
        private TranslateTransform _translateTransform;
        private Border _scaleTextBorder;

        private Size _imageSize;
        private Point _lastMousePosition;
        private bool _isDragging;
        private double _translateXCurrent = TranslateXDefault;
        private double _translateYCurrent = TranslateYDefault;
        private bool _useAnimationOnSetZoomRatio = true;
        private bool _suppressZoomRatioChanged;


        static ImageZoom()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ImageZoom), new FrameworkPropertyMetadata(typeof(ImageZoom)));
        }

        #region Source
        /// <summary>
        /// Gets or sets the <see cref="ImageSource"/> for the image.
        /// </summary>
        public ImageSource Source
        {
            get => (ImageSource)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }
        /// <summary>
        /// See <see cref="ImageZoom.Source"/> property.
        /// </summary>
        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(ImageSource), typeof(ImageZoom), new FrameworkPropertyMetadata(null, OnSourceChanged));
        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ImageZoom z = (ImageZoom)d;
            if (e.NewValue is ImageSource newSource)
            {
                z._imageSize.Width = newSource.Width;
                z._imageSize.Height = newSource.Height;
            }
            else
            {
                z._imageSize.Width = 0;
                z._imageSize.Height = 0;
            }
            z.Reset();
        }
        #endregion

        #region ZoomFactor
        /// <summary>
        /// Gets or sets the zoom factor.
        /// </summary>
        public double ZoomFactor
        {
            get { return (double)GetValue(ZoomFactorProperty); }
            set { SetValue(ZoomFactorProperty, value); }
        }
        /// <summary>
        /// See <see cref="ImageZoom.ZoomFactor"/> property.
        /// </summary>
        public static readonly DependencyProperty ZoomFactorProperty = DependencyProperty.Register(nameof(ZoomFactor), typeof(double), typeof(ImageZoom), new FrameworkPropertyMetadata(ZoomFactorDefault, null, OnCoerceZoomFactor));
        private static object OnCoerceZoomFactor(DependencyObject d, object baseValue)
        {
            ImageZoom z = (ImageZoom)d;
            double oldZoomFactor = z.ZoomFactor;

            double newZoomFactor = (double)baseValue;
            if (double.IsNaN(newZoomFactor) || double.IsInfinity(newZoomFactor))
                return oldZoomFactor;
            return Math.Max(newZoomFactor, 1);
        }
        #endregion

        #region ZoomRatio
        /// <summary>
        /// Gets or sets the zoom ratio.
        /// </summary>
        public double ZoomRatio
        {
            get { return (double)GetValue(ZoomRatioProperty); }
            set { SetValue(ZoomRatioProperty, value); }
        }
        /// <summary>
        /// See <see cref="ImageZoom.ZoomRatio"/> property.
        /// </summary>
        public static readonly DependencyProperty ZoomRatioProperty = DependencyProperty.Register(nameof(ZoomRatio), typeof(double), typeof(ImageZoom), new FrameworkPropertyMetadata(ZoomRatioDefault, OnZoomRatioChanged, OnCoerceZoomRatio));
        private static void OnZoomRatioChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ImageZoom z = (ImageZoom)d;
            if (z._suppressZoomRatioChanged)
                return;

            double newZoomRatio = (double)e.NewValue;
            z.AnimateTransform(newZoomRatio, newZoomRatio, z._translateXCurrent, z._translateYCurrent, z._useAnimationOnSetZoomRatio, true);
        }
        private static object OnCoerceZoomRatio(DependencyObject d, object baseValue)
        {
            ImageZoom z = (ImageZoom)d;
            double oldZoomRatio = z.ZoomRatio;

            double newZoomRatio = (double)baseValue;
            double result = newZoomRatio;
            if (double.IsNaN(newZoomRatio) || double.IsInfinity(newZoomRatio))
                result = oldZoomRatio;
            result = Clamp(result, z.MinZoomRatio, z.MaxZoomRatio);
            return result;
        }
        #endregion

        #region MinZoomRatio
        /// <summary>
        /// Gets or sets the minimum zoom ratio.
        /// </summary>
        public double MinZoomRatio
        {
            get { return (double)GetValue(MinZoomRatioProperty); }
            set { SetValue(MinZoomRatioProperty, value); }
        }
        /// <summary>
        /// See <see cref="ImageZoom.MinZoomRatio"/> property.
        /// </summary>
        public static readonly DependencyProperty MinZoomRatioProperty = DependencyProperty.Register(nameof(MinZoomRatio), typeof(double), typeof(ImageZoom), new FrameworkPropertyMetadata(MinZoomRatioDefault, OnMinZoomRatioChanged, OnCoerceMinZoomRatio));
        private static void OnMinZoomRatioChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ImageZoom z = (ImageZoom)d;
            double newMinZoomRatio = (double)e.NewValue;
            if (z.ZoomRatio < newMinZoomRatio)
                z.ZoomTo(newMinZoomRatio);
        }
        private static object OnCoerceMinZoomRatio(DependencyObject d, object baseValue)
        {
            ImageZoom z = (ImageZoom)d;
            double oldMinZoomRatio = z.MinZoomRatio;

            double newMinZoomRatio = (double)baseValue;
            if (double.IsNaN(newMinZoomRatio) || double.IsInfinity(newMinZoomRatio) || newMinZoomRatio < 0)
                return oldMinZoomRatio;
            return Math.Min(newMinZoomRatio, z.MaxZoomRatio);
        }
        #endregion

        #region MaxZoomRatio
        /// <summary>
        /// Gets or sets the maximum zoom ratio.
        /// </summary>
        public double MaxZoomRatio
        {
            get { return (double)GetValue(MaxZoomRatioProperty); }
            set { SetValue(MaxZoomRatioProperty, value); }
        }
        /// <summary>
        /// See <see cref="ImageZoom.MaxZoomRatio"/> property.
        /// </summary>
        public static readonly DependencyProperty MaxZoomRatioProperty = DependencyProperty.Register(nameof(MaxZoomRatio), typeof(double), typeof(ImageZoom), new FrameworkPropertyMetadata(MaxZoomRatioDefault, OnMaxZoomRatioChanged, OnCoerceMaxZoomRatio));
        private static void OnMaxZoomRatioChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ImageZoom z = (ImageZoom)d;
            double newMaxZoomRatio = (double)e.NewValue;
            if (z.ZoomRatio > newMaxZoomRatio)
                z.ZoomTo(newMaxZoomRatio);
        }
        private static object OnCoerceMaxZoomRatio(DependencyObject d, object baseValue)
        {
            ImageZoom z = (ImageZoom)d;
            double oldMaxZoomRatio = z.MaxZoomRatio;

            double newMaxZoomRatio = (double)baseValue;
            if (double.IsNaN(newMaxZoomRatio) || double.IsInfinity(newMaxZoomRatio) || newMaxZoomRatio < 0)
                return oldMaxZoomRatio;
            return Math.Max(newMaxZoomRatio, z.MinZoomRatio);
        }
        #endregion

        #region AlwaysHideZoomValueHint
        /// <summary>
        /// Gets or sets a value indicating whether the zoom value hint should always be hidden.
        /// </summary>
        public bool AlwaysHideZoomValueHint
        {
            get { return (bool)GetValue(AlwaysHideZoomValueHintProperty); }
            set { SetValue(AlwaysHideZoomValueHintProperty, value); }
        }
        /// <summary>
        /// See <see cref="ImageZoom.AlwaysHideZoomValueHint"/> property.
        /// </summary>
        public static readonly DependencyProperty AlwaysHideZoomValueHintProperty = DependencyProperty.Register(nameof(AlwaysHideZoomValueHint), typeof(bool), typeof(ImageZoom), new FrameworkPropertyMetadata(false, OnAlwaysHideZoomValueHintChanged));
        private static void OnAlwaysHideZoomValueHintChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ImageZoom z = (ImageZoom)d;
            bool hide = (bool)e.NewValue;
            if (z._scaleTextBorder != null)
                z._scaleTextBorder.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
        }
        #endregion

        #region DisableAnimation
        /// <summary>
        /// Gets or sets a value indicating whether the animation should be disabled.
        /// </summary>
        public bool DisableAnimation
        {
            get { return (bool)GetValue(DisableAnimationProperty); }
            set { SetValue(DisableAnimationProperty, value); }
        }
        /// <summary>
        /// See <see cref="ImageZoom.DisableAnimation"/> property.
        /// </summary>
        public static readonly DependencyProperty DisableAnimationProperty = DependencyProperty.Register(nameof(DisableAnimation), typeof(bool), typeof(ImageZoom), new FrameworkPropertyMetadata(false));
        #endregion

        #region DisableDoubleClickReset
        /// <summary>
        /// Gets or sets a value indicating whether the double-click reset should be disabled.
        /// </summary>
        public bool DisableDoubleClickReset
        {
            get { return (bool)GetValue(DisableDoubleClickResetProperty); }
            set { SetValue(DisableDoubleClickResetProperty, value); }
        }
        /// <summary>
        /// See <see cref="ImageZoom.DisableDoubleClickReset"/> property.
        /// </summary>
        public static readonly DependencyProperty DisableDoubleClickResetProperty = DependencyProperty.Register(nameof(DisableDoubleClickReset), typeof(bool), typeof(ImageZoom), new FrameworkPropertyMetadata(false));
        #endregion

        #region MoveCursor
        /// <summary>
        /// Gets or sets the cursor to use when the user is moving the image.
        /// </summary>
        public Cursor MoveCursor
        {
            get { return (Cursor)GetValue(MoveCursorProperty); }
            set { SetValue(MoveCursorProperty, value); }
        }
        /// <summary>
        /// See <see cref="ImageZoom.MoveCursor"/> property.
        /// </summary>
        public static readonly DependencyProperty MoveCursorProperty = DependencyProperty.Register(nameof(MoveCursor), typeof(Cursor), typeof(ImageZoom), new FrameworkPropertyMetadata(Cursors.ScrollAll));
        #endregion

        #region PanX
        /// <summary>
        /// Gets or sets the horizontal pan offset.
        /// </summary>
        public double PanX
        {
            get { return (double)GetValue(PanXProperty); }
            set { SetValue(PanXProperty, value); }
        }

        /// <summary>
        /// See <see cref="ImageZoom.PanX"/> property.
        /// </summary>
        public static readonly DependencyProperty PanXProperty =
            DependencyProperty.Register(
                nameof(PanX),
                typeof(double),
                typeof(ImageZoom),
                new FrameworkPropertyMetadata(
                    TranslateXDefault,
                    OnPanXChanged,
                    OnCoercePanX));

        private static void OnPanXChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ImageZoom z = (ImageZoom)d;
            double newX = (double)e.NewValue;

            // 使用当前 ZoomRatio，纵向平移采用当前 PanY
            double ratio = z.ZoomRatio;
            double y = z.PanY;

            z._translateXCurrent = newX;
            z.AnimateTransform(ratio, ratio, newX, y, !z.DisableAnimation, false);
        }

        private static object OnCoercePanX(DependencyObject d, object baseValue)
        {
            double value = (double)baseValue;
            if (double.IsNaN(value) || double.IsInfinity(value))
                return TranslateXDefault;
            return value;
        }
        #endregion

        #region PanY
        /// <summary>
        /// Gets or sets the vertical pan offset.
        /// </summary>
        public double PanY
        {
            get { return (double)GetValue(PanYProperty); }
            set { SetValue(PanYProperty, value); }
        }

        /// <summary>
        /// See <see cref="ImageZoom.PanY"/> property.
        /// </summary>
        public static readonly DependencyProperty PanYProperty =
            DependencyProperty.Register(
                nameof(PanY),
                typeof(double),
                typeof(ImageZoom),
                new FrameworkPropertyMetadata(
                    TranslateYDefault,
                    OnPanYChanged,
                    OnCoercePanY));

        private static void OnPanYChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ImageZoom z = (ImageZoom)d;
            double newY = (double)e.NewValue;

            double ratio = z.ZoomRatio;
            double x = z.PanX;

            z._translateYCurrent = newY;
            z.AnimateTransform(ratio, ratio, x, newY, !z.DisableAnimation, false);
        }

        private static object OnCoercePanY(DependencyObject d, object baseValue)
        {
            double value = (double)baseValue;
            if (double.IsNaN(value) || double.IsInfinity(value))
                return TranslateYDefault;
            return value;
        }
        #endregion

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (_imageContainer != null)
            {
                _imageContainer.MouseDown -= ImageContainer_MouseDown;
                _imageContainer.MouseMove -= ImageContainer_MouseMove;
                _imageContainer.MouseUp -= ImageContainer_MouseUp;
                _imageContainer.LostMouseCapture -= ImageContainer_LostMouseCapture;
                _imageContainer.MouseWheel -= ImageContainer_MouseWheel;
                _imageContainer.ManipulationStarting -= ImageContainer_ManipulationStarting;
                _imageContainer.ManipulationDelta -= ImageContainer_ManipulationDelta; ;
            }

            _imageContainer = GetTemplateChild(PartImageContainerName) as Grid;
            _image = GetTemplateChild(PartImageName) as Image;
            _scaleTransform = GetTemplateChild(PartScaleTransformName) as ScaleTransform;
            _translateTransform = GetTemplateChild(PartTranslateTransformName) as TranslateTransform;
            _scaleTextBorder = GetTemplateChild(PartScaleTextBorderName) as Border;

            if (_imageContainer != null)
            {
                _imageContainer.MouseDown += ImageContainer_MouseDown;
                _imageContainer.MouseMove += ImageContainer_MouseMove;
                _imageContainer.MouseUp += ImageContainer_MouseUp;
                _imageContainer.LostMouseCapture += ImageContainer_LostMouseCapture; ;
                _imageContainer.MouseWheel += ImageContainer_MouseWheel;
                _imageContainer.ManipulationStarting += ImageContainer_ManipulationStarting;
                _imageContainer.ManipulationDelta += ImageContainer_ManipulationDelta; ;
            }

            Reset();
        }

        private void ImageContainer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsPrimaryButton(e))
                return;

            if (!DisableDoubleClickReset && e.ClickCount is 2)
            {
                Reset();
                e.Handled = true;
                return;
            }

            _lastMousePosition = e.GetPosition(_imageContainer);
            _imageContainer.CaptureMouse();
            _imageContainer.Cursor = MoveCursor;
            _isDragging = true;
            e.Handled = true;
        }

        private void ImageContainer_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging)
                return;

            Point currentPosition = e.GetPosition(_imageContainer);
            Vector delta = currentPosition - _lastMousePosition;
            _lastMousePosition = currentPosition;

            MoveInternal(delta.X, delta.Y, false);
            e.Handled = true;
        }

        private void ImageContainer_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!IsPrimaryButton(e))
                return;

            if (_isDragging)
            {
                _isDragging = false;
                _imageContainer.ReleaseMouseCapture();
                _imageContainer.Cursor = null;
                e.Handled = true;
            }
        }

        private void ImageContainer_LostMouseCapture(object sender, MouseEventArgs e)
        {
            _isDragging = false;
            _imageContainer.ReleaseMouseCapture();
            _imageContainer.Cursor = null;
            e.Handled = true;
        }

        private void ImageContainer_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            bool isZoomIn = e.Delta > 0;
            bool needZoom = true;

            if (_imageSize.Width > 0 && _imageSize.Height > 0 && _image?.ActualWidth > 0 && _image?.ActualHeight > 0
                && _imageContainer?.ActualWidth > 0 && _imageContainer?.ActualHeight > 0 && _scaleTransform != null && _translateTransform != null)
            {
                double realTimeZoomRatio = _scaleTransform.ScaleX;
                Rect imageRectOnContainer = new Rect(_image.TranslatePoint(new Point(0, 0), _imageContainer),
                    new Size(_image.ActualWidth * realTimeZoomRatio, _image.ActualHeight * realTimeZoomRatio));
                Point mousePositionOnContainer = e.GetPosition(_imageContainer);
                bool isMouseOverImage = imageRectOnContainer.Contains(mousePositionOnContainer);
                if (isMouseOverImage)
                {
                    Vector relativeMousePos = mousePositionOnContainer - imageRectOnContainer.TopLeft;
                    double percentX = relativeMousePos.X / imageRectOnContainer.Width;
                    double percentY = relativeMousePos.Y / imageRectOnContainer.Height;
                    Point mousePositionPercentOnImage = new Point(percentX, percentY);

                    double newZoomRatio = isZoomIn ? PeekZoomInRatio() : PeekZoomOutRatio();
                    newZoomRatio = Clamp(newZoomRatio, MinZoomRatio, MaxZoomRatio);
                    Point renderTransOrigin = _image.RenderTransformOrigin;
                    double widthChange = Math.Abs(_image.ActualWidth * newZoomRatio - imageRectOnContainer.Width);
                    double heightChange = Math.Abs(_image.ActualHeight * newZoomRatio - imageRectOnContainer.Height);
                    double deltaX = widthChange * (mousePositionPercentOnImage.X - renderTransOrigin.X);
                    double deltaY = heightChange * (mousePositionPercentOnImage.Y - renderTransOrigin.Y);

                    double realTimeTranslateX = _translateTransform.X;
                    double realTimeTranslateY = _translateTransform.Y;
                    double translateX = realTimeTranslateX + (isZoomIn ? -deltaX : deltaX);
                    double translateY = realTimeTranslateY + (isZoomIn ? -deltaY : deltaY);

                    bool useAnimation = true;
                    _useAnimationOnSetZoomRatio = useAnimation;
                    bool hasScaleChanged = newZoomRatio != realTimeZoomRatio;
                    try
                    {
                        _suppressZoomRatioChanged = true;
                        this.SetCurrentValue(ImageZoom.ZoomRatioProperty, newZoomRatio);
                    }
                    finally
                    {
                        _suppressZoomRatioChanged = false;
                    }

                    // 同步逻辑平移值到依赖属性，保持与实际 Transform 一致
                    SetCurrentValue(PanXProperty, translateX);
                    SetCurrentValue(PanYProperty, translateY);

                    AnimateTransform(newZoomRatio, newZoomRatio, translateX, translateY, useAnimation, hasScaleChanged);
                    needZoom = false;
                }
            }

            if (needZoom)
            {
                if (isZoomIn)
                    ZoomIn();
                else
                    ZoomOut();
            }
            e.Handled = true;
        }

        private void ImageContainer_ManipulationStarting(object sender, ManipulationStartingEventArgs e)
        {
            //e.ManipulationContainer = _imageContainer;
            //e.Mode = ManipulationModes.Scale | ManipulationModes.Translate;
        }

        private void ImageContainer_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
            //Vector translation = e.DeltaManipulation.Translation;
            //MoveInternal(translation.X, translation.Y, false);

            //double scaleFactor = e.DeltaManipulation.Scale.X;
            //double newZoomRatio = ZoomRatio * scaleFactor;
            //double centerX = e.ManipulationOrigin.X;
            //double centerY = e.ManipulationOrigin.Y;
            //ZoomTo(newZoomRatio, false);

            //e.Handled = true;
        }


        private static bool IsPrimaryButton(MouseButtonEventArgs e)
        {
            return e.ChangedButton is MouseButton.Left;
        }

        private static double Clamp(double value, double min, double max)
        {
            double result = value;
            if (result < min)
                result = min;
            if (result > max)
                result = max;
            return result;
        }

        private void AnimateTransform(double scaleX, double scaleY, double x, double y, bool useAnimation, bool hasScaleChanged)
        {
            _translateXCurrent = x;
            _translateYCurrent = y;

            AnimateProperty(_translateTransform, TranslateTransform.XProperty, x, useAnimation);
            AnimateProperty(_translateTransform, TranslateTransform.YProperty, y, useAnimation);
            AnimateProperty(_scaleTransform, ScaleTransform.ScaleXProperty, scaleX, useAnimation);
            AnimateProperty(_scaleTransform, ScaleTransform.ScaleYProperty, scaleY, useAnimation);

            if (hasScaleChanged && _scaleTextBorder != null)
            {
                TimeSpan duration = TimeSpan.FromMilliseconds(ZoomValueHintAnimationDurationMs);
                const double OpacityFrom = 1;
                const double OpacityTo = 0;
                var fadeOutAnimation = new DoubleAnimation(OpacityFrom, OpacityTo, duration)
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                _scaleTextBorder.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation, HandoffBehavior.SnapshotAndReplace);
            }
        }

        private void AnimateProperty(Transform transform, DependencyProperty property, double targetValue, bool useAnimation)
        {
            if (transform is null || property is null)
                return;

            TimeSpan duration = useAnimation && !DisableAnimation ? TimeSpan.FromMilliseconds(AnimationDurationMs) : TimeSpan.Zero;
            var animation = new DoubleAnimation(targetValue, duration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            transform.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
        }


        private void ZoomTo(double ratio, bool useAnimation = true)
        {
            _useAnimationOnSetZoomRatio = useAnimation;
            this.SetCurrentValue(ImageZoom.ZoomRatioProperty, ratio);
        }

        private void MoveToInternal(double offsetX, double offsetY, bool useAnimation = true)
        {
            double ratio = ZoomRatio;
            if (double.IsNaN(offsetX) || double.IsInfinity(offsetX))
                offsetX = _translateXCurrent;
            else if (double.IsNaN(offsetY) || double.IsInfinity(offsetY))
                offsetY = _translateYCurrent;

            _translateXCurrent = offsetX;
            _translateYCurrent = offsetY;

            // 更新依赖属性，保证外部可以获取到最新的平移值
            SetCurrentValue(PanXProperty, offsetX);
            SetCurrentValue(PanYProperty, offsetY);

            AnimateTransform(ratio, ratio, offsetX, offsetY, useAnimation, false);
        }

        private void MoveInternal(double deltaX, double deltaY, bool useAnimation = true)
        {
            double translateX = _translateXCurrent + deltaX;
            double translateY = _translateYCurrent + deltaY;
            MoveToInternal(translateX, translateY, useAnimation);
        }

        private double PeekZoomInRatio()
        {
            return ZoomRatio * ZoomFactor;
        }

        private double PeekZoomOutRatio()
        {
            return ZoomRatio / ZoomFactor;
        }

        /// <summary>
        /// Zooms in the image by a factor of <see cref="ZoomFactor"/>.
        /// </summary>
        public void ZoomIn()
        {
            double newRatio = PeekZoomInRatio();
            ZoomTo(newRatio);
        }

        /// <summary>
        /// Zooms out the image by a factor of <see cref="ZoomFactor"/>.
        /// </summary>
        public void ZoomOut()
        {
            double newRatio = PeekZoomOutRatio();
            ZoomTo(newRatio);
        }

        /// <summary>
        /// Moves the image to the specified position.
        /// </summary>
        /// <param name="offsetX"></param>
        /// <param name="offsetY"></param>
        public void MoveTo(double offsetX, double offsetY)
        {
            MoveToInternal(offsetX, offsetY);
        }

        /// <summary>
        /// Moves the image to the left by a distance of <paramref name="delta"/>.
        /// </summary>
        /// <param name="delta"></param>
        public void Left(double delta)
        {
            MoveInternal(-delta, 0);
        }

        /// <summary>
        /// Moves the image to the right by a distance of <paramref name="delta"/>.
        /// </summary>
        /// <param name="delta"></param>
        public void Right(double delta)
        {
            MoveInternal(delta, 0);
        }

        /// <summary>
        /// Moves the image up by a distance of <paramref name="delta"/>.
        /// </summary>
        /// <param name="delta"></param>
        public void Up(double delta)
        {
            MoveInternal(0, -delta);
        }

        /// <summary>
        /// Moves the image down by a distance of <paramref name="delta"/>.
        /// </summary>
        /// <param name="delta"></param>
        public void Down(double delta)
        {
            MoveInternal(0, delta);
        }

        /// <summary>
        /// Resets the zoom and translation of the image to their default values.
        /// </summary>
        public void Reset()
        {
            if (_image != null)
                _image.RenderTransformOrigin = new Point(TranslateOriginXDefault, TranslateOriginYDefault);
            ZoomTo(ZoomRatioDefault);
            MoveTo(TranslateXDefault, TranslateYDefault);
        }
    }
}
