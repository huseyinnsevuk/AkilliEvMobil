using System;
using Microsoft.Maui.Controls;

namespace AkilliEvMobil.Views.Controls
{
    public partial class CustomToggle : ContentView
    {
        public static readonly BindableProperty IsToggledProperty = BindableProperty.Create(
            nameof(IsToggled), typeof(bool), typeof(CustomToggle), false, propertyChanged: OnIsToggledChanged);

        public bool IsToggled
        {
            get => (bool)GetValue(IsToggledProperty);
            set => SetValue(IsToggledProperty, value);
        }

        public event EventHandler<ToggledEventArgs> Toggled;

        public CustomToggle()
        {
            InitializeComponent();
            UpdateVisualState(false);
        }

        private static void OnIsToggledChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var control = (CustomToggle)bindable;
            control.UpdateVisualState(true);
        }

        private async void OnToggled(object sender, EventArgs e)
        {
            IsToggled = !IsToggled;
            Toggled?.Invoke(this, new ToggledEventArgs(IsToggled));
        }

        private async void UpdateVisualState(bool animate)
        {
            if (Thumb == null || BackgroundBorder == null) return;

            double targetX = IsToggled ? 26 : 0; // 54 total width - 22 thumb width - 3 margin*2 = 26 travel distance
            
            if (animate)
            {
                await Thumb.TranslateTo(targetX, 0, 200, Easing.CubicInOut);
            }
            else
            {
                Thumb.TranslationX = targetX;
            }

            if (IsToggled)
            {
                // Active State: Soft light blue background with a vibrant blue-violet gradient thumb
                BackgroundBorder.Background = new SolidColorBrush(Color.FromArgb("#EFF6FF"));
                BackgroundBorder.Stroke = Color.FromArgb("#DBEAFE");
                BackgroundBorder.StrokeThickness = 1;

                var onGradient = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1)
                };
                onGradient.GradientStops.Add(new GradientStop(Color.FromArgb("#60A5FA"), 0.0f)); // Soft light blue
                onGradient.GradientStops.Add(new GradientStop(Color.FromArgb("#1D4ED8"), 1.0f)); // Deep premium blue
                Thumb.Background = onGradient;
            }
            else
            {
                // Inactive State: Gray background and neutral white-gray gradient thumb
                BackgroundBorder.Background = new SolidColorBrush(Color.FromArgb("#E2E8F0"));
                BackgroundBorder.Stroke = Colors.Transparent;
                BackgroundBorder.StrokeThickness = 0;

                var offGradient = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1)
                };
                offGradient.GradientStops.Add(new GradientStop(Color.FromArgb("#FFFFFF"), 0.0f));
                offGradient.GradientStops.Add(new GradientStop(Color.FromArgb("#CBD5E1"), 1.0f));
                Thumb.Background = offGradient;
            }
        }
    }
}
