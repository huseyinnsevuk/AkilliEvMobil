using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace AkilliEvMobil.Views.Controls
{
    public class ConcaveCardDrawable : IDrawable
    {
        public Color CardColor { get; set; } = Color.FromArgb("#E8ECF1");

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.SaveState();
            canvas.Antialias = true;

            float w = dirtyRect.Width;
            float h = dirtyRect.Height;
            float cornerR = 24f;    // Kartın normal köşe yuvarlaması

            // Daire parametreleri (sağ üst köşedeki bağımsız daire)
            float circleR = 22f;    // Dairenin yarıçapı
            float gap = 6f;         // Daire ile kart arasındaki boşluk
            float biteR = circleR + gap; // Oyuğun toplam yarıçapı

            // Dairenin merkezi: kartın sağ üst köşesine yakın
            float cx = w - 4f;     // Daire merkezi X (kartın sağ kenarına çok yakın)
            float cy = 4f;          // Daire merkezi Y (kartın üst kenarına çok yakın)

            // Oyuğun başlangıç ve bitiş açıları
            // Kartın üst kenarından gelen çizgi, oyuğa giriyor
            // Kartın sağ kenarından çıkış yapıyor
            
            // Oyuğun üst kenardaki giriş noktası
            float enterX = cx - biteR;
            float enterY = 0f;
            
            // Oyuğun sağ kenardaki çıkış noktası
            float exitX = w;
            float exitY = cy + biteR;

            PathF path = new PathF();

            // 1. Sol Üst Köşe'den başla
            path.MoveTo(cornerR, 0);

            // 2. Üst Kenar → oyuğun giriş noktasına kadar
            path.LineTo(enterX, 0);

            // 3. İçbükey (concave) oyuk — dairenin etrafını saran kavis
            // Üst kenardan başlayıp, dairenin etrafından geçerek sağ kenara inen
            // Bezier kontrol noktaları ile yumuşak bir içbükey kavis
            float k = 0.55228f; // Dairesel kavis için Bezier katsayısı

            // Giriş noktasından (enterX, 0) → alt nokta (cx, cy + biteR)'a bezier
            // Sonra alt noktadan → çıkış noktasına (w, exitY) bezier
            
            // İlk bezier: üst kenardan aşağı doğru kıvrılma
            path.CurveTo(
                enterX + biteR * k, 0,       // kontrol 1: yatay devam
                cx - biteR, cy + biteR * (1 - k),  // kontrol 2
                cx - biteR, cy               // orta nokta (sol taraf)
            );

            // İkinci bezier: dairenin alt kısmını sarma
            path.CurveTo(
                cx - biteR, cy + biteR * k,   // kontrol 1
                cx - biteR * k, cy + biteR,   // kontrol 2
                cx, cy + biteR                // alt orta nokta
            );
            
            // Üçüncü bezier: sağ kenara çıkış
            path.CurveTo(
                cx + biteR * k, cy + biteR,   // kontrol 1
                w, exitY - biteR * k,         // kontrol 2
                w, exitY                      // çıkış noktası (sağ kenar)
            );

            // 4. Sağ Kenar (aşağı)
            path.LineTo(w, h - cornerR);

            // 5. Sağ Alt Köşe
            path.CurveTo(w, h - cornerR + cornerR * k, w - cornerR + cornerR * k, h, w - cornerR, h);

            // 6. Alt Kenar
            path.LineTo(cornerR, h);

            // 7. Sol Alt Köşe
            path.CurveTo(cornerR - cornerR * k, h, 0, h - cornerR + cornerR * k, 0, h - cornerR);

            // 8. Sol Kenar (yukarı)
            path.LineTo(0, cornerR);

            // 9. Sol Üst Köşe
            path.CurveTo(0, cornerR - cornerR * k, cornerR - cornerR * k, 0, cornerR, 0);

            path.Close();

            // Kartı boya (gölge yok, temiz flat tasarım)
            canvas.FillColor = CardColor;
            canvas.FillPath(path);

            canvas.RestoreState();
        }
    }
}
