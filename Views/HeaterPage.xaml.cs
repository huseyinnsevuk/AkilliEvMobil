using Microsoft.Maui.Controls.Shapes;

namespace AkilliEvMobil.Views;

public partial class HeaterPage : ContentPage, IDrawable
{
    private bool _isOn = false;
    private float _phase = 0;
    private bool _isAnimating = false;

    public HeaterPage()
    {
        InitializeComponent();
        WaveView.Drawable = this;
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        _isAnimating = false;
        await Shell.Current.GoToAsync("..");
    }

    private async void OnPowerTapped(object sender, EventArgs e)
    {
        _isOn = !_isOn;

        await PowerButtonBorder.ScaleTo(0.95, 100);
        await PowerButtonBorder.ScaleTo(1.0, 100);

        if (_isOn)
        {
            StatusLabel.Text = "Şu an Çalışıyor";
            StatusLabel.TextColor = Color.FromArgb("#FF7043");
            PowerLabel.Text = "DURDUR";
            
            _isAnimating = true;
            await WaveView.FadeTo(1, 500);
            StartAnimationLoop();
        }
        else
        {
            StatusLabel.Text = "Şu an Kapalı";
            StatusLabel.TextColor = Color.FromArgb("#A0AABF");
            PowerLabel.Text = "BAŞLAT";

            _isAnimating = false;
            await WaveView.FadeTo(0, 300);
        }
    }

    private async void StartAnimationLoop()
    {
        while (_isAnimating)
        {
            _phase += 0.06f; // Slowed down from 0.15f
            WaveView.Invalidate();
            await Task.Delay(16); // ~60fps
        }
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (!_isAnimating) return;

        float centerX = dirtyRect.Width / 2;
        float centerY = dirtyRect.Height / 2;
        float baseRadius = 78;

        canvas.SaveState();
        canvas.Antialias = true;

        // Draw 3 layers of "breathing" filled gradient waves
        // Use _phase to modulate amplitude instead of rotating
        DrawWaveLayer(canvas, centerX, centerY, baseRadius + 15, _phase * 1.5f, Color.FromArgb("#33FFAB00"), 18, 8); // Outer
        DrawWaveLayer(canvas, centerX, centerY, baseRadius + 8, _phase * 1.2f, Color.FromArgb("#66FF6D00"), 12, 6);  // Mid
        DrawWaveLayer(canvas, centerX, centerY, baseRadius, _phase, Color.FromArgb("#AAFF3D00"), 10, 5);           // Inner fire

        canvas.RestoreState();
    }

    private void DrawWaveLayer(ICanvas canvas, float cx, float cy, float radius, float phase, Color color, float baseAmplitude, int frequency)
    {
        var path = new PathF();
        int points = 120;

        // Amplitude modulation: breathes between 50% and 100% of baseAmplitude
        float pulse = (float)(Math.Sin(phase) * 0.25 + 0.75);
        float currentAmplitude = baseAmplitude * pulse;

        for (int i = 0; i <= points; i++)
        {
            float angle = (float)(i * 2 * Math.PI / points);
            // Angle remains fixed (no phase), amplitude pulses
            float rOffset = (float)(Math.Sin(angle * frequency) * currentAmplitude);
            float r = radius + rOffset;

            float x = cx + (float)(Math.Cos(angle) * r);
            float y = cy + (float)(Math.Sin(angle) * r);

            if (i == 0) path.MoveTo(x, y);
            else path.LineTo(x, y);
        }
        path.Close();

        var gradient = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.5),
            Radius = 0.5,
            GradientStops = new GradientStopCollection
            {
                new GradientStop { Color = color, Offset = 0.5f },
                new GradientStop { Color = Colors.Transparent, Offset = 1.0f }
            }
        };

        canvas.SetFillPaint(gradient, new RectF(cx - radius - baseAmplitude, cy - radius - baseAmplitude, (radius + baseAmplitude) * 2, (radius + baseAmplitude) * 2));
        canvas.FillPath(path);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _isAnimating = false;
    }
}
