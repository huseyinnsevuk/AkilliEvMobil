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
            if (Thumb == null) return;

            double targetX = IsToggled ? 26 : 0; // 54 total width - 22 thumb width - 3 margin*2 = 26 travel distance
            
            if (animate)
            {
                await Thumb.TranslateTo(targetX, 0, 200, Easing.CubicInOut);
            }
            else
            {
                Thumb.TranslationX = targetX;
            }

            // Optional: Background color change if needed, but the gradient looks good as is
            // BackgroundBorder.Opacity = IsToggled ? 1.0 : 0.6;
        }
    }
}
