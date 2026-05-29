import time
import os
import sys

# ==========================================
# 🧪 DONANIM SİMÜLASYONU (PC vs RASPBERRY)
# ==========================================
try:
    import board
    import adafruit_dht
    IS_RASPBERRY_PI = True
except ImportError:
    IS_RASPBERRY_PI = False
    
    class MockDHT11:
        def __init__(self, pin):
            self.pin = pin
        @property
        def temperature(self):
            import random
            return int(random.uniform(22, 26))
        @property
        def humidity(self):
            import random
            return int(random.uniform(40, 50))
        def exit(self):
            print("   [SİMÜLASYON] DHT11 Kapatıldı.")
            
    board = type('board', (), {'D4': 4})
    adafruit_dht = type('adafruit_dht', (), {'DHT11': MockDHT11})

# ==========================================
# ⚙️ DHT11 PIN TANIMI (BCM)
# ==========================================
# DHT11 veri pini GPIO 4 (Raspberry Pi Fiziksel Pin 7) olarak ayarlanmıştır.
DHT_PIN = board.D4 

def print_banner():
    """Kullanıcı bilgilendirme arayüzü"""
    os.system('cls' if os.name == 'nt' else 'clear')
    print("=" * 60)
    print("      🌡️  DHT11 TEMPERATURE & HUMIDITY TEST SCRIPT 🌡️")
    print("=" * 60)
    print(f"📍 BAĞLANTI ŞEMASI:")
    print(f"   👉 VCC  (Besleme)     ---->  Raspberry Pi 3.3V veya 5V Pin")
    print(f"   👉 GND  (Toprak)      ---->  Raspberry Pi GND Pin")
    print(f"   👉 DATA (Veri Pini)   ---->  Raspberry Pi GPIO 4 (BCM) / Fiziksel Pin 7")
    print("-" * 60)
    if IS_RASPBERRY_PI:
        print("✅ Raspberry Pi donanımı algılandı. Gerçek GPIO aktif.")
    else:
        print("⚠️  RASPBERRY PI BULUNAMADI! Simülasyon modunda çalışıyor.")
    print("📝 NOT: DHT11 donanımsal limitleri gereği okumalar arasında")
    print("        en az 2 saniye beklenmelidir. Okuma döngüsü 2.5 sn olacaktır.")
    print("=" * 60)
    print("\n🚀 Test başlıyor... Çıkmak için Ctrl+C tuşlarına basın.\n")

def main():
    print_banner()
    
    try:
        dht_device = adafruit_dht.DHT11(DHT_PIN)
    except Exception as e:
        print(f"❌ HATA: DHT11 sensörü başlatılamadı: {e}")
        print("Lütfen kablolamayı ve 'libgpiod' kütüphanesini kontrol edin.")
        return

    success_count = 0
    fail_count = 0
    
    try:
        while True:
            try:
                # Sıcaklık ve Nem değerlerini oku
                temp = dht_device.temperature
                hum = dht_device.humidity
                
                if temp is not None and hum is not None:
                    success_count += 1
                    total = success_count + fail_count
                    success_rate = (success_count / total) * 100
                    
                    print(f"✅ OKUMA BAŞARILI [Toplam: {total} | Başarı: %{success_rate:.1f}]")
                    print(f"   🌡️  Sıcaklık : {temp}°C")
                    print(f"   💧  Nem      : %{hum}")
                    print("-" * 40)
            except RuntimeError as error:
                # DHT sensörlerinde zamanlama hataları (checksum/timing) çok sık olur.
                # Bu normaldir, döngü devam etmelidir.
                fail_count += 1
                total = success_count + fail_count
                success_rate = (success_count / total) * 100 if total > 0 else 0
                print(f"⚠️  Okuma Hatası (Timing/Checksum): {error.args[0]}")
                print(f"   📊 Başarı Oranı: %{success_rate:.1f}")
                print("-" * 40)
            except Exception as e:
                dht_device.exit()
                raise e
            
            # DHT11 en az 2 saniye dinlendirilmelidir
            time.sleep(2.5)
            
    except KeyboardInterrupt:
        print("\n🛑 Test sonlandırılıyor...")
    finally:
        dht_device.exit()
        print("🧹 Sensör kaynakları temizlendi ve çıkış yapıldı.")

if __name__ == "__main__":
    main()
