using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace AkilliEvMobil.Views.Controls
{
    public class ConcaveCardDrawable : IDrawable
    {
        public Color CardColor { get; set; } = Colors.White;

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.SaveState();
            canvas.Antialias = true;

            float w = dirtyRect.Width;
            float h = dirtyRect.Height;
            float r = 20f; // Klasik köşe yuvarlaması
            float b = 35f; // Oyuk (bite) derinliği/yarıçapı
            float s = 15f; // Pürüzsüz geçiş (smoothing) yarıçapı

            // Bezier katsayısı (Dairesel yumuşatma için ~0.55)
            float k = 0.55228f;
            float sk = s * k; // Smoothing kontrol noktası mesafesi
            float bk = b * k; // Bite kontrol noktası mesafesi

            PathF path = new PathF();
            
            // 1. Sol Üst Köşe'den başla (kavis bittikten sonraki nokta)
            path.MoveTo(r, 0);
            
            // 2. Üst Kenar (Oyuğun başladığı yere kadar)
            path.LineTo(w - b - s, 0);
            
            // 3. İçbükey oyuğa pürüzsüz giriş (Konveks pah)
            // (w - b - s, 0) noktasından (w - b, s) noktasına
            path.CurveTo(
                w - b - s + sk, 0, 
                w - b, s - sk, 
                w - b, s);
                
            // 4. İçbükey (Concave) Oyuk (Isırık)
            // (w - b, s) noktasından (w - s, b) noktasına, içe doğru kavis
            path.CurveTo(
                w - b, s + bk, 
                w - s - bk, b, 
                w - s, b);
                
            // 5. Oyuktan sağ kenara pürüzsüz çıkış (Konveks pah)
            // (w - s, b) noktasından (w, b + s) noktasına
            path.CurveTo(
                w - s + sk, b, 
                w, b + s - sk, 
                w, b + s);

            // 6. Sağ Kenar
            path.LineTo(w, h - r);
            
            // 7. Sağ Alt Köşe
            path.AddArc(w - r * 2, h - r * 2, r * 2, r * 2, 0, 90, true);
            
            // 8. Alt Kenar
            path.LineTo(r, h);
            
            // 9. Sol Alt Köşe
            path.AddArc(0, h - r * 2, r * 2, r * 2, 90, 180, true);
            
            // 10. Sol Kenar
            path.LineTo(0, r);
            
            // 11. Sol Üst Köşe
            path.AddArc(0, 0, r * 2, r * 2, 180, 270, true);
            
            path.Close();

            // Gölge ekle (Orijinal koddaki shadow)
            canvas.SetShadow(new SizeF(0, 8), 15, Color.FromArgb("#40CBD5E1")); // %25 Opaklık
            
            // Kartı boya
            canvas.FillColor = CardColor;
            canvas.FillPath(path);
            
            canvas.RestoreState();
        }
    }
}
