import RPi.GPIO as GPIO
import time

# Pin Ayarları (Kanal B)
PIN_LIGHT_PWM = 13  # ENB (Parlaklık)
PIN_LIGHT_IN3 = 20  # IN3 (Yön +)
PIN_LIGHT_IN4 = 21  # IN4 (Yön -)

# Temizlik ve Kurulum
GPIO.setwarnings(False)
GPIO.setmode(GPIO.BCM)
GPIO.setup(PIN_LIGHT_PWM, GPIO.OUT)
GPIO.setup(PIN_LIGHT_IN3, GPIO.OUT)
GPIO.setup(PIN_LIGHT_IN4, GPIO.OUT)

# L298N Yönünü Ayarla (Akım geçişine izin ver)
GPIO.output(PIN_LIGHT_IN3, GPIO.HIGH)
GPIO.output(PIN_LIGHT_IN4, GPIO.LOW)

# PWM Ayarını tekrar ENB pini üzerinden yap, frekansı 1000 Hz'e çıkar
pwm = GPIO.PWM(PIN_LIGHT_PWM, 1000) # 1000 Hz frekans (Kırpışmayı gözün göremeyeceği seviyeye çeker)
pwm.start(0)

try:
    print("=" * 40)
    print("💡 AYDINLATMA DONANIM TESTİ BAŞLIYOR")
    print("=" * 40)
    
    # Adım 1: %100 Parlaklık
    print("▶️ AŞAMA 1: %100 Parlaklık (10 Saniye)")
    pwm.ChangeDutyCycle(100)
    time.sleep(10)
    
    # Adım 2: %50 Parlaklık
    print("▶️ AŞAMA 2: %50 Parlaklık (10 Saniye)")
    pwm.ChangeDutyCycle(50)
    time.sleep(10)
    
    # Test Sonu
    print("▶️ TEST BİTTİ: Lamba Kapatılıyor...")
    pwm.ChangeDutyCycle(0)
    
    # Tam güvenlik için yön pinini kapat
    GPIO.output(PIN_LIGHT_IN3, GPIO.LOW)
    print("✅ Sistem başarıyla kapatıldı.")

except KeyboardInterrupt:
    print("\n⚠️ Test kullanıcı tarafından iptal edildi.")

finally:
    # GPIO pinlerini temizle (Başka bir script çalıştırıldığında sorun çıkmasın)
    pwm.stop()
    GPIO.cleanup()
