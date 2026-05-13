using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace AkilliEvMobil.Views.Controls
{
    [ContentProperty(nameof(InnerContent))]
    public partial class ConcaveCard : ContentView
    {
        private readonly ConcaveCardDrawable _drawable;

        public static readonly BindableProperty CardColorProperty = BindableProperty.Create(
            nameof(CardColor), typeof(Color), typeof(ConcaveCard), Colors.White, propertyChanged: OnCardColorChanged);

        public Color CardColor
        {
            get => (Color)GetValue(CardColorProperty);
            set => SetValue(CardColorProperty, value);
        }

        public static readonly BindableProperty InnerContentProperty = BindableProperty.Create(
            nameof(InnerContent), typeof(View), typeof(ConcaveCard), null, propertyChanged: OnInnerContentChanged);

        public View InnerContent
        {
            get => (View)GetValue(InnerContentProperty);
            set => SetValue(InnerContentProperty, value);
        }

        public ConcaveCard()
        {
            InitializeComponent();
            _drawable = new ConcaveCardDrawable();
            CardGraphicsView.Drawable = _drawable;
        }

        private static void OnCardColorChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is ConcaveCard card && newValue is Color color)
            {
                card._drawable.CardColor = color;
                card.CardGraphicsView.Invalidate();
            }
        }

        private static void OnInnerContentChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is ConcaveCard card && newValue is View view)
            {
                card.CardContent.Content = view;
            }
        }
    }
}
