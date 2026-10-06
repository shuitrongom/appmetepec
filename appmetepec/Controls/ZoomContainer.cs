namespace appmetepec.Controls;

// Contenedor con zoom por pellizco y doble toque; al estar ampliado permite arrastrar la imagen.
// El arrastre (Pan) solo se registra mientras hay zoom para no bloquear el deslizamiento del
// CarouselView que lo contiene (ver GaleriaEvidenciasPage).
public sealed class ZoomContainer : ContentView
{
    private const double MaxScale = 4;
    private const double DoubleTapScale = 2.5;

    private readonly PanGestureRecognizer _pan = new();
    private double _currentScale = 1;
    private double _startScale = 1;
    private double _xOffset;
    private double _yOffset;
    private double _panStartX;
    private double _panStartY;
    private bool _isZoomed;

    // true = la imagen quedo ampliada; false = regreso a su tamaño normal.
    public event EventHandler<bool>? ZoomChanged;

    public ZoomContainer()
    {
        var pinch = new PinchGestureRecognizer();
        pinch.PinchUpdated += OnPinchUpdated;
        GestureRecognizers.Add(pinch);

        var doubleTap = new TapGestureRecognizer { NumberOfTapsRequired = 2 };
        doubleTap.Tapped += OnDoubleTapped;
        GestureRecognizers.Add(doubleTap);

        _pan.PanUpdated += OnPanUpdated;
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        // El CarouselView recicla las vistas: cada imagen nueva empieza sin zoom.
        Reset(animate: false);
    }

    public void Reset(bool animate = true)
    {
        _currentScale = 1;
        _xOffset = _yOffset = 0;
        if (Content is not null)
        {
            if (animate)
            {
                _ = Content.ScaleTo(1, 200, Easing.CubicOut);
                _ = Content.TranslateTo(0, 0, 200, Easing.CubicOut);
            }
            else
            {
                Content.Scale = 1;
                Content.TranslationX = Content.TranslationY = 0;
            }
        }
        SetZoomed(false);
    }

    private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        if (Content is null) return;

        switch (e.Status)
        {
            case GestureStatus.Started:
                _startScale = Content.Scale;
                Content.AnchorX = 0;
                Content.AnchorY = 0;
                break;

            case GestureStatus.Running:
                _currentScale += (e.Scale - 1) * _startScale;
                _currentScale = Math.Clamp(_currentScale, 1, MaxScale);

                // Mantiene fijo el punto entre los dedos mientras se amplia.
                var renderedX = Content.X + _xOffset;
                var deltaX = renderedX / Width;
                var deltaWidth = Width / (Content.Width * _startScale);
                var originX = (e.ScaleOrigin.X - deltaX) * deltaWidth;

                var renderedY = Content.Y + _yOffset;
                var deltaY = renderedY / Height;
                var deltaHeight = Height / (Content.Height * _startScale);
                var originY = (e.ScaleOrigin.Y - deltaY) * deltaHeight;

                var targetX = _xOffset - (originX * Content.Width) * (_currentScale - _startScale);
                var targetY = _yOffset - (originY * Content.Height) * (_currentScale - _startScale);

                Content.TranslationX = ClampX(targetX, _currentScale);
                Content.TranslationY = ClampY(targetY, _currentScale);
                Content.Scale = _currentScale;
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _xOffset = Content.TranslationX;
                _yOffset = Content.TranslationY;
                if (_currentScale <= 1.05) Reset();
                else SetZoomed(true);
                break;
        }
    }

    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (Content is null || !_isZoomed) return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _panStartX = Content.TranslationX;
                _panStartY = Content.TranslationY;
                break;

            case GestureStatus.Running:
                Content.TranslationX = ClampX(_panStartX + e.TotalX, Content.Scale);
                Content.TranslationY = ClampY(_panStartY + e.TotalY, Content.Scale);
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _xOffset = Content.TranslationX;
                _yOffset = Content.TranslationY;
                break;
        }
    }

    private async void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (Content is null) return;

        if (_isZoomed)
        {
            Reset();
            return;
        }

        // Amplia hacia el punto tocado (o al centro si no se conoce).
        var punto = e.GetPosition(this) ?? new Point(Width / 2, Height / 2);
        Content.AnchorX = 0;
        Content.AnchorY = 0;
        _currentScale = DoubleTapScale;
        var targetX = ClampX(-(punto.X * (DoubleTapScale - 1)), DoubleTapScale);
        var targetY = ClampY(-(punto.Y * (DoubleTapScale - 1)), DoubleTapScale);
        SetZoomed(true);

        await Task.WhenAll(
            Content.ScaleTo(DoubleTapScale, 220, Easing.CubicOut),
            Content.TranslateTo(targetX, targetY, 220, Easing.CubicOut));
        _xOffset = Content.TranslationX;
        _yOffset = Content.TranslationY;
    }

    private double ClampX(double x, double scale) => Content is null ? 0 : Math.Clamp(x, -Content.Width * (scale - 1), 0);
    private double ClampY(double y, double scale) => Content is null ? 0 : Math.Clamp(y, -Content.Height * (scale - 1), 0);

    private void SetZoomed(bool zoomed)
    {
        if (_isZoomed == zoomed) return;
        _isZoomed = zoomed;

        if (zoomed) GestureRecognizers.Add(_pan);
        else GestureRecognizers.Remove(_pan);

        ZoomChanged?.Invoke(this, zoomed);
    }
}
