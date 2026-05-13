using Microsoft.Maui.Graphics;

namespace AkilliEvMobil.Views.Controls;

public partial class StripedSlider : ContentView, IDrawable
{
    public static readonly BindableProperty StartColorProperty = BindableProperty.Create(
        nameof(StartColor), typeof(Color), typeof(StripedSlider), Color.FromArgb("#FFD54F"));

    public static readonly BindableProperty EndColorProperty = BindableProperty.Create(
        nameof(EndColor), typeof(Color), typeof(StripedSlider), Color.FromArgb("#FFB300"));

    public Color StartColor
    {
        get => (Color)GetValue(StartColorProperty);
        set => SetValue(StartColorProperty, value);
    }

    public Color EndColor
    {
        get => (Color)GetValue(EndColorProperty);
        set => SetValue(EndColorProperty, value);
    }

    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value), typeof(double), typeof(StripedSlider), 0.0, BindingMode.TwoWay,
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            var control = (StripedSlider)bindable;
            control.UpdateUI((double)newValue);
        });

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public StripedSlider()
    {
        InitializeComponent();
        StripesGraphicsView.Drawable = this;
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        UpdateUI(Value);
    }

    private void OnSliderValueChanged(object sender, ValueChangedEventArgs e)
    {
        Value = e.NewValue;
    }

    private void UpdateUI(double value)
    {
        if (MainSlider == null || FillBorder == null || ThumbBorder == null || this.Width <= 0) return;

        MainSlider.Value = value;

        double totalWidth = this.Width;
        double thumbWidth = ThumbBorder.Width;
        
        // Thumb moves between 0 and (totalWidth - thumbWidth)
        double availableWidthForThumb = totalWidth - thumbWidth;
        double thumbPos = (value / 100.0) * availableWidthForThumb;
        
        ThumbBorder.TranslationX = thumbPos;

        // Fill width follows the percentage of the total width
        // At 0%, width is 0. At 100%, width is totalWidth.
        // This ensures it perfectly matches the thumb's progress.
        FillBorder.WidthRequest = (value / 100.0) * totalWidth;
        
        StripesGraphicsView.Invalidate();
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState();
        
        float stripeWidth = 2;
        float spacing = 8;
        
        canvas.StrokeColor = Colors.Black.WithAlpha(0.1f);
        canvas.StrokeSize = stripeWidth;

        // Draw vertical lines across the track
        for (float x = 20; x < dirtyRect.Width - 20; x += spacing)
        {
            canvas.DrawLine(x, 10, x, dirtyRect.Height - 10);
        }

        canvas.RestoreState();
    }
}
