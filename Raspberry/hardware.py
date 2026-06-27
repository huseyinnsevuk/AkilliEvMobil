import time
import paho.mqtt.client as mqtt
import RPi.GPIO as GPIO
import cv2
import http.server
import socketserver
import threading

# ==========================================
# 🧪 DONANIM SİMÜLASYONU (PC vs RASPBERRY)
# ==========================================
try:
    import smbus
    import board
    import adafruit_dht
    HAS_DHT = True
    IS_PC = False
except ImportError:
    HAS_DHT = False
    IS_PC = True
    class MockSMBus:
        def __init__(self, bus): pass
        def write_byte_data(self, addr, reg, val): pass
        def read_byte_data(self, addr, reg):
            if reg == (0x80 | 0x0C): return 0xA0
            if reg == (0x80 | 0x0D): return 0x0F
            return 0
    smbus = type('smbus', (), {'SMBus': MockSMBus})

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
        def exit(self): pass
    
    board = type('board', (), {'D4': 4})
    adafruit_dht = type('adafruit_dht', (), {'DHT11': MockDHT11})

# pigpio kütüphanesini bağımsız olarak yükle (Eksikse PC modunu tetiklemesin, doğrudan RPi.GPIO fallback kullansın)
try:
    import pigpio
except ImportError:
    class MockPi:
        def __init__(self):
            self.connected = True
        def set_servo_pulsewidth(self, pin, val): pass
        def stop(self): pass
    class MockPigpio:
        def pi(self): return MockPi()
    pigpio = MockPigpio()

# ==========================================
# ⚙️ AYARLAR
# ==========================================
VDS_DOMAIN = "nart3d.com" 
MQTT_PORT = 1883

# MQTT KONULARI
TOPIC_BASE     = "Nest/home"
KONU_YAGMUR    = f"{TOPIC_BASE}/sensor/yagmur"
KONU_TENTE     = f"{TOPIC_BASE}/command/tente"
KONU_AYDINLATMA= f"{TOPIC_BASE}/command/aydinlatma"
KONU_FAN       = f"{TOPIC_BASE}/command/fan"
KONU_HEATER    = f"{TOPIC_BASE}/command/heater"
KONU_ISIK      = f"{TOPIC_BASE}/sensor/isik"
KONU_SICAKLIK  = f"{TOPIC_BASE}/sensor/sicaklik"
KONU_NEM       = f"{TOPIC_BASE}/sensor/nem"
KONU_GAZ       = f"{TOPIC_BASE}/sensor/gaz"

# Pin Ayarları
PIN_YAGMUR = 17
PIN_SERVO  = 18
PIN_GAZ    = 27

# Isıtıcı (Röle)
PIN_HEATER = 16

# Aydınlatma (L298N Kanal B)
PIN_LIGHT_PWM  = 13  # ENB (Parlaklık - Hız)
PIN_LIGHT_IN3  = 20  # IN3 (Yön +) 
PIN_LIGHT_IN4  = 21  # IN4 (Yön -) 

# Fan (L298N Kanal A)
PIN_FAN_PWM    = 12  # ENA (Hız / PWM)
PIN_FAN_IN1    = 5   # IN1 (Yön +)
PIN_FAN_IN2    = 6   # IN2 (Yön -)

GPIO.setmode(GPIO.BCM)
GPIO.setup(PIN_YAGMUR, GPIO.IN)
GPIO.setup(PIN_HEATER, GPIO.OUT)
GPIO.setup(PIN_GAZ, GPIO.IN)

# L298N Aydınlatma Pin Kurulumları
GPIO.setup(PIN_LIGHT_PWM, GPIO.OUT)
GPIO.setup(PIN_LIGHT_IN3, GPIO.OUT)
GPIO.setup(PIN_LIGHT_IN4, GPIO.OUT)

# L298N Fan Pin Kurulumları
GPIO.setup(PIN_FAN_PWM, GPIO.OUT)
GPIO.setup(PIN_FAN_IN1, GPIO.OUT)
GPIO.setup(PIN_FAN_IN2, GPIO.OUT)

# Aydınlatma Yönünü Ayarla (Akım IN3'ten IN4'e akacak)
GPIO.output(PIN_LIGHT_IN3, GPIO.HIGH)
GPIO.output(PIN_LIGHT_IN4, GPIO.LOW)

# Fan Yönünü Ayarla (Varsayılan: İleri)
GPIO.output(PIN_FAN_IN1, GPIO.HIGH)
GPIO.output(PIN_FAN_IN2, GPIO.LOW)

# Isıtıcı Başlangıç Durumu (Kapalı)
GPIO.output(PIN_HEATER, GPIO.LOW)

# Aydınlatma için PWM (L298N üzerinden parlaklık)
pwm_aydinlatma = GPIO.PWM(PIN_LIGHT_PWM, 1000) # Kırpışmayı önlemek için 1000 Hz
pwm_aydinlatma.start(0) # Başlangıçta %0 duty cycle (kapalı)

# Fan için PWM (L298N üzerinden hız)
pwm_fan = GPIO.PWM(PIN_FAN_PWM, 100) # 100 Hz
pwm_fan.start(0) # Başlangıçta %0 duty cycle (kapalı)

# pigpio daemon bağlantısı ve Servo Donanımsal PWM Ayarı (Geriye Dönük Uyumluluklu)
pi = pigpio.pi()
use_pigpio = False
pwm_servo = None

if pi.connected:
    use_pigpio = True
    print("✅ pigpio daemon bağlantısı başarılı! Donanımsal PWM aktif.")
    pi.set_servo_pulsewidth(PIN_SERVO, 0)
else:
    print("⚠️ UYARI: pigpio daemon (pigpiod) çalışmıyor!")
    print("👉 Yazılımsal PWM (RPi.GPIO) moduna otomatik geçiş yapılıyor.")
    print("👉 Not: Donanımsal PWM için sunucuda 'sudo pigpiod' komutunu çalıştırabilirsiniz.")
    GPIO.setup(PIN_SERVO, GPIO.OUT)
    pwm_servo = GPIO.PWM(PIN_SERVO, 50)
    pwm_servo.start(0)

# TSL2561 Işık Sensörü Başlatma
TSL2561_ADDR = 0x39 
try: 
    bus = smbus.SMBus(1)
    # TSL2561 Güç Verme (0x80 | 0x00 adresine 0x03 yazılır)
    bus.write_byte_data(TSL2561_ADDR, 0x80 | 0x00, 0x03)
    print("✅ TSL2561 Işık Sensörü Başlatıldı.")
except Exception as e:
    print(f"⚠️ Işık Sensörü Başlatılamadı: {e}")
    bus = None

def read_lux():
    """TSL2561 Sensöründen Ortam Aydınlığını Lux Cinsinden Okur"""
    if bus is None:
        return 250 # Okunamadıysa varsayılan iç mekan aydınlığı (Lux)
    try:
        if IS_PC:
            import random
            return int(random.uniform(280, 320))
        # TSL2561 Kanal 0 Broad-Spectrum (Kızılötesi + Görünür Işık) okuması
        low = bus.read_byte_data(TSL2561_ADDR, 0x80 | 0x0C)
        high = bus.read_byte_data(TSL2561_ADDR, 0x80 | 0x0D)
        val = high * 256 + low
        
        # 1 Lux yaklaşık 0.15 counts olarak ölçeklenir
        lux = int(val * 0.15)
        return max(0, lux)
    except Exception as e:
        print(f"⚠️ Işık sensörü okunamadı: {e}")
        return 250

# DHT11 Sıcaklık ve Nem Sensörü Başlatma (GPIO 4 / board.D4)
try:
    dht_device = adafruit_dht.DHT11(board.D4)
    print("✅ DHT11 Sıcaklık ve Nem Sensörü Başlatıldı.")
except Exception as e:
    print(f"⚠️ DHT11 Sıcaklık ve Nem Sensörü Başlatılamadı: {e}")
    dht_device = None

def read_dht():
    """DHT11 Sensöründen Sıcaklık ve Nem Okur, Hataları Graceful Ele Alır"""
    if dht_device is None:
        return None, None
    try:
        temp = dht_device.temperature
        hum = dht_device.humidity
        if temp is not None and hum is not None:
            return temp, hum
    except RuntimeError as error:
        # Adafruit DHT okuma hataları timing sebepli çok yaygındır, sessizce geçilir
        pass
    except Exception as e:
        print(f"⚠️ DHT11 Okuma Hatası: {e}")
    return None, None

# --- BAŞLANGIÇ TESTİ (Debug İçin) ---
print("🧪 DONANIM TESTİ BAŞLIYOR... (Lamba, Fan ve Isıtıcı 2 saniye çalışmalı)")
try:
    # Lamba Testi
    GPIO.output(PIN_LIGHT_IN3, GPIO.HIGH) 
    pwm_aydinlatma.ChangeDutyCycle(100) 
    
    # Fan Testi
    GPIO.output(PIN_FAN_IN1, GPIO.HIGH)
    GPIO.output(PIN_FAN_IN2, GPIO.LOW)
    pwm_fan.ChangeDutyCycle(100)

    # Isıtıcı Testi
    GPIO.output(PIN_HEATER, GPIO.HIGH)
    
    time.sleep(2)
    
    # Lamba Kapat
    pwm_aydinlatma.ChangeDutyCycle(0)   
    GPIO.output(PIN_LIGHT_IN3, GPIO.LOW)  # Kaçak voltajı engelle
    
    # Fan Kapat
    pwm_fan.ChangeDutyCycle(0)
    GPIO.output(PIN_FAN_IN1, GPIO.LOW)  # Kaçak voltajı engelle

    # Isıtıcı Kapat
    GPIO.output(PIN_HEATER, GPIO.LOW)
    
    print("✅ Donanım testi tamamlandı. Sistem dinlemeye geçiyor.")
except Exception as e:
    print(f"❌ Test sırasında hata: {e}")

def set_servo_angle(angle):
    duty = angle / 18 + 2.5
    GPIO.output(PIN_SERVO, True)
    pwm.ChangeDutyCycle(duty)
    time.sleep(0.5)
    GPIO.output(PIN_SERVO, False)
    pwm.ChangeDutyCycle(0)

# ==========================================
# 📹 KAMERA YAKALAMA YAPISI (GStreamer)
# ==========================================
# 📹 KAMERA YAKALAMA VE BULUT YÜKLEME YAPISI
# ==========================================
class CameraCapture:
    def __init__(self):
        self.width = 640
        self.height = 480
        self.fps = 30
        
        # GStreamer pipeline tanımı (Ters montaj için 180 derece dönüş OpenCV'de uygulanacak)
        self.pipeline = (
            "libcamerasrc ! "
            f"video/x-raw, width={self.width}, height={self.height}, framerate={self.fps}/1 ! "
            "videoconvert ! "
            "video/x-raw, format=BGR ! "
            "appsink drop=true sync=false"
        )
        
        print("📷 RPi Kamera pipeline (GStreamer) başlatılıyor...")
        self.video = cv2.VideoCapture(self.pipeline, cv2.CAP_GSTREAMER)
        
        if not self.video.isOpened():
            print("⚠️ GStreamer pipeline başarısız oldu. Standart VideoCapture(0) deneniyor...")
            self.video = cv2.VideoCapture(0)
            
        if self.video.isOpened():
            print("✅ Kamera donanımı başarıyla açıldı!")
        else:
            print("❌ HATA: Kamera donanımı açılamadı! Başka bir script kamerayı kilitliyor olabilir.")
            
        time.sleep(1.0)
        
        # Arka planda frame okuma ve yükleme döngüsünü başlat
        self.running = True
        self.thread = threading.Thread(target=self._capture_and_upload_loop, daemon=True)
        self.thread.start()

    def _capture_and_upload_loop(self):
        print("🏃 Arka planda kamera okuma ve VDS sunucusuna yükleme döngüsü başladı...")
        import requests
        session = requests.Session() # Keep-Alive sayesinde tünel kadar hızlı bağlantı
        first_frame = True
        frame_count = 0
        
        while self.running:
            if self.video.isOpened():
                ret, frame = self.video.read()
                if ret:
                    if first_frame:
                        print("🎉 GStreamer üzerinden ilk kare başarıyla okundu ve VDS sunucusuna yükleniyor!")
                        first_frame = False
                    
                    # 180 Derece Döndürme (Ters montaj için)
                    frame = cv2.rotate(frame, cv2.ROTATE_180)
                    
                    # Kareyi yüksek kaliteli/düşük boyutlu JPEG'e sıkıştır (%40 kalite idealdir)
                    ret_enc, jpeg = cv2.imencode('.jpg', frame, [int(cv2.IMWRITE_JPEG_QUALITY), 40])
                    if ret_enc:
                        jpeg_bytes = jpeg.tobytes()
                        try:
                            # VDS sunucusuna binary POST ile aktarım
                            session.post(
                                f"http://{VDS_DOMAIN}:3000/api/camera/upload", 
                                data=jpeg_bytes, 
                                headers={"Content-Type": "image/jpeg"},
                                timeout=0.8
                            )
                            
                            frame_count += 1
                            if frame_count % 90 == 0:
                                print(f"📡 VDS bulut sunucusuna 90 kare başarıyla yüklendi (Toplam: {frame_count})")
                        except Exception as e:
                            # Ağ kopması vb. durumlarda kısa bir uyku
                            time.sleep(0.1)
                            
                    time.sleep(0.01)
                else:
                    time.sleep(0.03)
            else:
                time.sleep(0.1)

    def close(self):
        self.running = False
        print("🔌 Kamera kaynakları serbest bırakılıyor...")
        if self.video.isOpened():
            self.video.release()

camera = None

def baslat_kamera_yayini():
    global camera
    try:
        camera = CameraCapture()
        while True:
            time.sleep(1)
    except Exception as e:
        print(f"❌ Kamera sunucusu hatası: {e}")

# ==========================================
# 📡 MQTT KURULUMU
# ==========================================
def on_connect(client, userdata, flags, rc):
    if rc == 0:
        print("✅ MQTT Broker'a Bağlandı!")
        client.subscribe("Nest/#")
        print(f"📡 Nest/# kanalları dinleniyor...")
    else:
        print(f"❌ Bağlantı Hatası: {rc}")

current_angle = 0

def set_servo_angle_with_speed(target_angle, speed_percent):
    global current_angle
    
    # 0 ile 180 derece sınırlarını koru
    target_angle = max(0, min(180, target_angle))
    
    angle_diff = abs(target_angle - current_angle)
    if angle_diff < 1:
        return
        
    if use_pigpio:
        print(f"🔄 pigpio Donanımsal Yumuşak Hareket: {current_angle} -> {target_angle}")
        pulse_min = 500
        pulse_max = 2500
        
        def angle_to_pulse(angle):
            return int(pulse_min + (angle / 180.0) * (pulse_max - pulse_min))

        # Yumuşak Geçiş (Sweep): 2 derecelik adımlarla 18ms bekleyerek hedefe git
        step_size = 2
        step = step_size if target_angle > current_angle else -step_size
        
        temp_angle = current_angle
        steps_list = []
        if step > 0:
            while temp_angle < target_angle:
                steps_list.append(temp_angle)
                temp_angle += step_size
        else:
            while temp_angle > target_angle:
                steps_list.append(temp_angle)
                temp_angle -= step_size
                
        steps_list.append(target_angle)
        
        for ang in steps_list:
            pw = angle_to_pulse(ang)
            pi.set_servo_pulsewidth(PIN_SERVO, pw)
            time.sleep(0.018) # 18ms gecikme
            
        time.sleep(0.15)
        pi.set_servo_pulsewidth(PIN_SERVO, 0)
    else:
        # RPi.GPIO Yazılımsal PWM Modu Fallback (pigpiod çalışmadığında devreye girer)
        print(f"🔄 RPi.GPIO Yazılımsal Güvenli Hareket: {current_angle} -> {target_angle} (Sweep Modu)")
        
        if pwm_servo:
            # Kullanıcının eski kodundaki gibi 2.0 duty (0 deg) ile ~12.0 duty (180 deg) aralığını kullanıyoruz
            target_duty = 2.0 + (target_angle / 180.0) * 10.0
            current_duty = 2.0 + (current_angle / 180.0) * 10.0
            
            # Hıza göre adımı belirliyoruz (Kullanıcının Slow, Medium, Fast mantığı)
            if speed_percent <= 30:
                adim = 0.02
            elif speed_percent <= 70:
                adim = 0.06
            else:
                adim = 0.20 # %100 hız (Fast)
                
            cur = current_duty
            yon = 1 if target_duty > cur else -1
            
            while True:
                if abs(cur - target_duty) < adim:
                    cur = target_duty
                    break
                cur += (adim * yon)
                pwm_servo.ChangeDutyCycle(cur)
                time.sleep(0.02) # Kullanıcının sweep bekleme süresi
                
            # Tam hedefe yerleşip sinyali kesiyoruz
            pwm_servo.ChangeDutyCycle(target_duty)
            time.sleep(0.1)
            pwm_servo.ChangeDutyCycle(0)
            
    current_angle = target_angle
    print("✅ Hedefe ulaşıldı, sinyal kesildi.")

def on_message(client, userdata, msg):
    try:
        import json
        payload_str = msg.payload.decode().strip()
        print(f"📩 Komut Geldi: {msg.topic} -> {payload_str}")
        
        if msg.topic == KONU_TENTE:
            data = json.loads(payload_str)
            opening_percent = int(data.get("position", 0))
            speed = int(data.get("speed", 50))
            
            target_angle = (opening_percent * 180) / 100
            set_servo_angle_with_speed(target_angle, speed)
            
        elif msg.topic == KONU_AYDINLATMA:
            data = json.loads(payload_str)
            state = data.get("state", "OFF")
            brightness = int(data.get("brightness", 100))
            
            if state == "ON" and brightness > 0:
                GPIO.output(PIN_LIGHT_IN3, GPIO.HIGH) # L298N çıkışına izin ver
                pwm_aydinlatma.ChangeDutyCycle(brightness)
                print(f"💡 Aydınlatma AÇILDI (Parlaklık: %{brightness})")
            else:
                pwm_aydinlatma.ChangeDutyCycle(0)
                GPIO.output(PIN_LIGHT_IN3, GPIO.LOW) # L298N çıkışını kes
                print("💡 Aydınlatma KAPATILDI")
                
        elif msg.topic == KONU_FAN:
            data = json.loads(payload_str)
            state = data.get("state", "OFF")
            speed = int(data.get("speed", 100))
            
            if state == "ON" and speed > 0:
                GPIO.output(PIN_FAN_IN1, GPIO.HIGH) # Fan yönünü ileri ayarla
                GPIO.output(PIN_FAN_IN2, GPIO.LOW)
                pwm_fan.ChangeDutyCycle(speed)
                print(f"🌀 Fan AÇILDI (Hız: %{speed})")
            else:
                pwm_fan.ChangeDutyCycle(0)
                GPIO.output(PIN_FAN_IN1, GPIO.LOW) # Kaçak voltajı engelle
                GPIO.output(PIN_FAN_IN2, GPIO.LOW)
                print("🌀 Fan KAPATILDI")

        elif msg.topic == KONU_HEATER:
            data = json.loads(payload_str)
            state = data.get("state", "OFF")
            
            if state == "ON":
                GPIO.output(PIN_HEATER, GPIO.HIGH)
                print("🔥 Isıtıcı AÇILDI")
            else:
                GPIO.output(PIN_HEATER, GPIO.LOW)
                print("🔥 Isıtıcı KAPATILDI")
            
    except Exception as e:
        print(f"❌ Komut işleme hatası: {e}")
        
    # [LATENCY ÖLÇÜMÜ İÇİN] Her komut işlendikten sonra anında ACK (Onay) gönder
    if msg.topic.startswith("Nest/home/command/"):
        device_type = msg.topic.split("/")[-1]
        client.publish(f"Nest/home/ack/{device_type}", "OK")
        print(f"✅ ACK Gönderildi: Nest/home/ack/{device_type}")

client = mqtt.Client()
client.on_connect = on_connect
client.on_message = on_message

try:
    client.connect(VDS_DOMAIN, MQTT_PORT, 60)
    client.loop_start()
except Exception as e:
    print(f"❌ MQTT Hatası: {e}")

# ==========================================
# 🚀 SUNUCULARI AÇ VE ANA DÖNGÜYE GEÇ
# ==========================================
# Kamera yayın sunucusunu arka plan iş parçacığı (Thread) olarak başlatıyoruz
threading.Thread(target=baslat_kamera_yayini, daemon=True).start()

try:
    print("🚀 SİSTEM ÇİFT YÖNLÜ ÇALIŞIYOR...")
    while True:
        try:
            # Yağmur Sensörü Okuma ve Yayınlama
            durum = GPIO.input(PIN_YAGMUR)
            yagmur_var_mi = "1" if durum == 0 else "0"
            client.publish(KONU_YAGMUR, yagmur_var_mi)
            
            # Gaz Sensörü Okuma ve Yayınlama (MQ-2)
            gaz_durum = GPIO.input(PIN_GAZ)
            # Genellikle MQ-2 dijital çıkışı gaz algılandığında LOW (0) olur. 
            # Durum 0 ise gaz var ("1"), 1 ise temiz hava ("0") yayınlanır.
            gaz_var_mi = "1" if gaz_durum == 0 else "0"
            client.publish(KONU_GAZ, gaz_var_mi)
            if gaz_durum == 0:
                print("🚨 DIKKAT: Gaz sizintisi tespit edildi!")
            
            # LDR Işık Sensörü Okuma ve Yayınlama (Lux)
            lux_degeri = read_lux()
            client.publish(KONU_ISIK, str(lux_degeri))

            # DHT11 Sıcaklık ve Nem Sensörü Okuma ve Yayınlama
            temp, hum = read_dht()
            if temp is not None:
                client.publish(KONU_SICAKLIK, str(temp))
                print(f"🌡️ Sıcaklık Okundu: {temp}°C")
            if hum is not None:
                client.publish(KONU_NEM, str(hum))
                print(f"💧 Nem Okundu: %{hum}")
        except Exception as e:
            print(f"❌ Hata: {e}")
        time.sleep(3)
except KeyboardInterrupt:
    print("\n🛑 Kullanıcı tarafından durduruldu.")
finally:
    print("🧹 Sistem temizliği başlatılıyor...")
    
    if dht_device is not None:
        try:
            dht_device.exit()
            print("✅ DHT11 sensör bağlantısı sonlandırıldı.")
        except Exception as e:
            print(f"⚠️ DHT11 kapatılamadı: {e}")
            
    if camera is not None:
        try:
            camera.close()
            print("✅ RPi Kamera bağlantısı sonlandırıldı.")
        except Exception as e:
            print(f"⚠️ Kamera kapatılamadı: {e}")
            
    try:
        pwm.stop()
        pwm_aydinlatma.stop()
        pwm_fan.stop()
        GPIO.cleanup()
        print("✅ GPIO pin temizliği tamamlandı.")
    except Exception as e:
        print(f"⚠️ GPIO temizlenirken hata: {e}")
