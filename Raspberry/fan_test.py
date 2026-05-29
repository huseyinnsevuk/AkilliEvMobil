import time
import os
import sys

# ==========================================
# 🧪 DONANIM SİMÜLASYONU (PC vs RASPBERRY)
# ==========================================
try:
    import RPi.GPIO as GPIO
    IS_RASPBERRY_PI = True
except ImportError:
    IS_RASPBERRY_PI = False
    
    class MockGPIO:
        BCM = "BCM"
        OUT = "OUT"
        IN = "IN"
        HIGH = 1
        LOW = 0
        
        def setmode(self, mode): pass
        def setup(self, pin, mode, pull_up_down=None): pass
        def output(self, pin, state):
            state_str = "HIGH (1)" if state == 1 else "LOW (0)"
            print(f"   [SİMÜLASYON] Pin {pin} -> {state_str}")
        def cleanup(self): 
            print("   [SİMÜLASYON] GPIO Temizlendi (Cleanup).")
        
        class PWM:
            def __init__(self, pin, freq): 
                self.pin = pin
                self.freq = freq
                self.duty = 0
            def start(self, duty): 
                self.duty = duty
                print(f"   [SİMÜLASYON] PWM Pin {self.pin} Başlatıldı. Frekans: {self.freq}Hz, Duty: %{duty}")
            def ChangeDutyCycle(self, duty): 
                self.duty = duty
                print(f"   [SİMÜLASYON] PWM Pin {self.pin} Hız (Duty Cycle) Değiştirildi -> %{duty}")
                
    GPIO = MockGPIO()

# ==========================================
# ⚙️ FAN PIN TANIMLARI (BCM formatında)
# ==========================================
# L298N Sürücü - Kanal A Bağlantıları
PIN_FAN_PWM = 12  # ENA (Hız Kontrolü - PWM)
PIN_FAN_IN1 = 5   # IN1 (Yön Pin 1)
PIN_FAN_IN2 = 6   # IN2 (Yön Pin 2)

def setup_gpio():
    """GPIO Ayarlarını Yapar ve Başlatır"""
    GPIO.setmode(GPIO.BCM)
    GPIO.setup(PIN_FAN_PWM, GPIO.OUT)
    GPIO.setup(PIN_FAN_IN1, GPIO.OUT)
    GPIO.setup(PIN_FAN_IN2, GPIO.OUT)
    
    # Başlangıçta Yön Ayarı: İleri (Forward)
    GPIO.output(PIN_FAN_IN1, GPIO.HIGH)
    GPIO.output(PIN_FAN_IN2, GPIO.LOW)
    
    # 100Hz frekansta PWM başlat, başlangıç hızı %0
    fan_pwm = GPIO.PWM(PIN_FAN_PWM, 100)
    fan_pwm.start(0)
    return fan_pwm

def print_banner():
    """Kullanıcı bilgilendirme arayüzü"""
    os.system('cls' if os.name == 'nt' else 'clear')
    print("=" * 60)
    print("      🌀 L298N DRIVER - RASPBERRY PI FAN TEST SCRIPT 🌀")
    print("=" * 60)
    print(f"📍 BAĞLANTI ŞEMASI (BCM):")
    print(f"   👉 ENA  (Hız / PWM)   ---->  Raspberry Pi GPIO {PIN_FAN_PWM}")
    print(f"   👉 IN1  (Yön Kontrol) ---->  Raspberry Pi GPIO {PIN_FAN_IN1}")
    print(f"   👉 IN2  (Yön Kontrol) ---->  Raspberry Pi GPIO {PIN_FAN_IN2}")
    print(f"   👉 GND  (Toprak)      ---->  Raspberry Pi GND (Ortak Şasi)")
    print("-" * 60)
    if IS_RASPBERRY_PI:
        print("✅ Raspberry Pi donanımı algılandı. Gerçek GPIO aktif.")
    else:
        print("⚠️  RASPBERRY PI BULUNAMADI! Simülasyon modunda çalışıyor.")
    print("=" * 60)

def main():
    print_banner()
    fan_pwm = setup_gpio()
    
    current_direction = "İLERİ" # İleri: IN1=1, IN2=0
    current_speed = 0
    
    try:
        while True:
            print(f"\n📊 MEVCUT DURUM: Fan Hızı: %{current_speed} | Yön: {current_direction}")
            print("-" * 40)
            print("1 - Hız Ayarla (%0 - %100)")
            print("2 - Yön Değiştir (İleri / Geri)")
            print("3 - Otomatik Rampa Testi Çalıştır (Hızlan/Yavaşla)")
            print("4 - Fanı Tamamen Durdur")
            print("5 - Çıkış")
            print("-" * 40)
            
            secim = input("Lütfen bir seçenek girin (1-5): ").strip()
            
            if secim == "1":
                try:
                    hiz_input = input("Hız Değeri girin (%0 - %100): ").strip()
                    hiz = int(hiz_input)
                    if 0 <= hiz <= 100:
                        current_speed = hiz
                        fan_pwm.ChangeDutyCycle(current_speed)
                        print(f"🌀 Fan hızı %{current_speed} olarak ayarlandı.")
                    else:
                        print("❌ Hata: Lütfen 0 ile 100 arasında bir değer girin!")
                except ValueError:
                    print("❌ Hata: Geçersiz sayısal değer!")
                    
            elif secim == "2":
                if current_direction == "İLERİ":
                    # Geri yöne al
                    GPIO.output(PIN_FAN_IN1, GPIO.LOW)
                    GPIO.output(PIN_FAN_IN2, GPIO.HIGH)
                    current_direction = "GERİ"
                    print("🔄 Yön GERİ olarak değiştirildi (IN1: LOW, IN2: HIGH).")
                else:
                    # İleri yöne al
                    GPIO.output(PIN_FAN_IN1, GPIO.HIGH)
                    GPIO.output(PIN_FAN_IN2, GPIO.LOW)
                    current_direction = "İLERİ"
                    print("🔄 Yön İLERİ olarak değiştirildi (IN1: HIGH, IN2: LOW).")
                    
            elif secim == "3":
                print("\n🚀 Otomatik Rampa Testi Başlatılıyor...")
                print("➡️  1. Adım: İleri Yön - Hız %0'dan %100'e çıkarılıyor...")
                GPIO.output(PIN_FAN_IN1, GPIO.HIGH)
                GPIO.output(PIN_FAN_IN2, GPIO.LOW)
                current_direction = "İLERİ"
                
                for s in range(0, 101, 10):
                    fan_pwm.ChangeDutyCycle(s)
                    current_speed = s
                    print(f"📈 Hız: %{s}")
                    time.sleep(0.4)
                
                print("⏳ %100 hızda 2 saniye bekleniyor...")
                time.sleep(2)
                
                print("📉 2. Adım: İleri Yön - Hız %100'den %0'a düşürülüyor...")
                for s in range(100, -1, -10):
                    fan_pwm.ChangeDutyCycle(s)
                    current_speed = s
                    print(f"📉 Hız: %{s}")
                    time.sleep(0.4)
                
                time.sleep(1)
                
                print("🔄 3. Adım: Geri Yön - Hız %0'dan %100'e çıkarılıyor...")
                GPIO.output(PIN_FAN_IN1, GPIO.LOW)
                GPIO.output(PIN_FAN_IN2, GPIO.HIGH)
                current_direction = "GERİ"
                
                for s in range(0, 101, 10):
                    fan_pwm.ChangeDutyCycle(s)
                    current_speed = s
                    print(f"📈 Hız (Geri): %{s}")
                    time.sleep(0.4)
                
                print("⏳ %100 hızda 2 saniye bekleniyor...")
                time.sleep(2)
                
                print("📉 4. Adım: Geri Yön - Hız %100'den %0'a düşürülüyor...")
                for s in range(100, -1, -10):
                    fan_pwm.ChangeDutyCycle(s)
                    current_speed = s
                    print(f"📉 Hız (Geri): %{s}")
                    time.sleep(0.4)
                
                print("✅ Otomatik test başarıyla tamamlandı!")
                
            elif secim == "4":
                current_speed = 0
                fan_pwm.ChangeDutyCycle(0)
                print("🛑 Fan durduruldu (Hız %0).")
                
            elif secim == "5":
                print("👋 Çıkış yapılıyor...")
                break
            else:
                print("❌ Geçersiz seçim! Lütfen 1-5 arasında bir rakam girin.")
                
    except KeyboardInterrupt:
        print("\n🛑 Program kullanıcı tarafından durduruldu.")
    finally:
        fan_pwm.ChangeDutyCycle(0)
        GPIO.cleanup()
        print("🧹 GPIO kaynakları temizlendi ve program sonlandırıldı.")

if __name__ == "__main__":
    main()
