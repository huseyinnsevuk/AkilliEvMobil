import cv2
import threading
import collections
import time
import smtplib
import paho.mqtt.client as mqtt
import requests
import urllib.parse
from flask import Flask, Response
from email.message import EmailMessage
import os
import sys

# --- NGROK KÜTÜPHANESİ ---
try:
    from pyngrok import ngrok
    NGROK_MEVCUT = True
except ImportError:
    NGROK_MEVCUT = False
    print("⚠️ pyngrok yüklü değil. 'pip install pyngrok --break-system-packages' ile yükleyebilirsiniz.")

# ==========================================
# 🧪 DONANIM SİMÜLASYONU (PC vs RASPBERRY)
# ==========================================
try:
    import RPi.GPIO as GPIO
    import smbus
    import adafruit_dht
    import board
    IS_RASPBERRY_PI = True
    print("✅ Raspberry Pi Donanımı Tespit Edildi.")
except ImportError:
    IS_RASPBERRY_PI = False
    print("⚠️ RASPBERRY PI BULUNAMADI: Simülasyon Modu Aktif (PC)")
    
    class MockGPIO:
        BCM = "BCM"
        OUT = "OUT"
        IN = "IN"
        HIGH = 1
        LOW = 0
        PUD_UP = "PUD_UP"
        
        def setmode(self, mode): pass
        def setup(self, pin, mode, pull_up_down=None): pass
        def output(self, pin, state): pass
        def input(self, pin): return 1 
        def cleanup(self): print("   [SİMÜLASYON] GPIO Temizlendi.")
        
        class PWM:
            def __init__(self, pin, freq): self.pin = pin
            def start(self, duty): pass
            def ChangeDutyCycle(self, duty): pass
                
    class MockSMBus:
        def __init__(self, bus): pass
        def write_byte_data(self, addr, reg, val): pass
        def read_byte_data(self, addr, reg): return 0
        
    class MockDHT:
        temperature = 25
        humidity = 50

    GPIO = MockGPIO()
    smbus = type('smbus', (), {'SMBus': MockSMBus})
    board = type('board', (), {'D4': 4, 'SCL': 3, 'SDA': 2})
    adafruit_dht = type('adafruit_dht', (), {'DHT11': lambda x: MockDHT()})

# ==========================================
# ⚙️ AYARLAR
# ==========================================
# BURAYA NGROK TOKEN'INIZI YAPIŞTIRIN
NGROK_AUTH_TOKEN = "36sUmmejf97rWt5rkFC19e35bhg_2FnWJVHq5MLptg9FU1eYq" 

VDS_IP = "192.168.137.1" 
MQTT_PORT = 1883

# MQTT KONULARI
KONU_SICAKLIK = "tr/net/huseyin/sicaklik"
KONU_NEM      = "tr/net/huseyin/nem"
KONU_ISIK     = "tr/net/huseyin/isik"
KONU_YAGMUR   = "tr/net/huseyin/yagmur"
KONU_MESAFE   = "tr/net/huseyin/mesafe"
KONU_GAZ      = "tr/net/huseyin/gaz"
KONU_TENTE    = "tr/net/huseyin/tente/komut"
KONU_CMD_MAIL = "tr/net/huseyin/kamera/cmd/mail"
KONU_CMD_WP   = "tr/net/huseyin/kamera/cmd/wp"

# YENİ EKLENEN KONULAR
KONU_HEATER     = "tr/net/huseyin/heater/durum"
KONU_COOLER     = "tr/net/huseyin/klima/cooler"
KONU_LAMBA      = "tr/net/huseyin/lamba/ayar"

GMAIL_USER = "huseyinnsevuk@gmail.com"
GMAIL_PASS = "mizg kukk gbpw bvov" 

IFTTT_KEY = "dF8XcxGShFhC4arp9I0tJH2xk_zVgo3gbClZSPdYp2X"
IFTTT_EVENT = "gaz_alarmi"

# GREEN API (WhatsApp Video)
GREEN_INSTANCE_ID = "7105411368"
GREEN_API_TOKEN   = "04c359491bde449a8820fc445674cb90d29d3fd0036e4b81a2"

# PIN AYARLARI (BCM)
PIN_SERVO      = 18
PIN_YAGMUR     = 17
PIN_TRIG       = 23
PIN_ECHO       = 24
PIN_GAZ        = 27

# AKTÜATÖR PINLERİ
PIN_HEATER     = 16  # Röle (Isıtıcı)

# --- L298N BAĞLANTILARI ---
# Fan (Kanal A)
PIN_FAN_PWM    = 12  # ENA (Hız)
PIN_FAN_IN1    = 5   # IN1 (Yön +) 
PIN_FAN_IN2    = 6   # IN2 (Yön -) 

# Aydınlatma (Kanal B)
PIN_LIGHT_PWM  = 13  # ENB (Parlaklık - Hız)
PIN_LIGHT_IN3  = 20  # IN3 (Yön +) 
PIN_LIGHT_IN4  = 21  # IN4 (Yön -) 

# KAMERA
GENISLIK = 320
YUKSEKLIK = 240
FPS = 10
MAX_FRAME = FPS * 10 

# ==========================================

app = Flask(__name__)
video_buffer = collections.deque(maxlen=MAX_FRAME)
lock = threading.Lock()

# GPIO Kurulumu
GPIO.setmode(GPIO.BCM)
GPIO.setup(PIN_YAGMUR, GPIO.IN)
GPIO.setup(PIN_TRIG, GPIO.OUT)
GPIO.setup(PIN_ECHO, GPIO.IN)
GPIO.setup(PIN_GAZ, GPIO.IN, pull_up_down=GPIO.PUD_UP) 

# Aktuatör Kurulumları
GPIO.setup(PIN_SERVO, GPIO.OUT)
GPIO.setup(PIN_HEATER, GPIO.OUT) 

# L298N Pin Kurulumları
GPIO.setup(PIN_FAN_PWM, GPIO.OUT)
GPIO.setup(PIN_FAN_IN1, GPIO.OUT)
GPIO.setup(PIN_FAN_IN2, GPIO.OUT)

GPIO.setup(PIN_LIGHT_PWM, GPIO.OUT)
GPIO.setup(PIN_LIGHT_IN3, GPIO.OUT)
GPIO.setup(PIN_LIGHT_IN4, GPIO.OUT)

# --- SÜRÜCÜLERİ AKTİF ET (Yönleri Ayarla) ---
# Fan Yönünü Ayarla
GPIO.output(PIN_FAN_IN1, GPIO.HIGH)
GPIO.output(PIN_FAN_IN2, GPIO.LOW)

# Aydınlatma Yönünü Ayarla
GPIO.output(PIN_LIGHT_IN3, GPIO.HIGH)
GPIO.output(PIN_LIGHT_IN4, GPIO.LOW)

# Isıtıcı Başlangıç Durumu (Kapalı)
GPIO.output(PIN_HEATER, GPIO.LOW)

# PWM Başlatma
servo = GPIO.PWM(PIN_SERVO, 50)
servo.start(0) 

fan_pwm = GPIO.PWM(PIN_FAN_PWM, 100) 
fan_pwm.start(0) 

aydinlatma_pwm = GPIO.PWM(PIN_LIGHT_PWM, 100) 
aydinlatma_pwm.start(0) 

# --- BAŞLANGIÇ TESTİ (Debug İçin) ---
print("🧪 SİSTEM TESTİ BAŞLIYOR...")
try:
    aydinlatma_pwm.ChangeDutyCycle(100) 
    fan_pwm.ChangeDutyCycle(100)       
    time.sleep(2)
    aydinlatma_pwm.ChangeDutyCycle(0)   
    fan_pwm.ChangeDutyCycle(0)         
    print("✅ Test tamamlandı. Sistem dinlemeye geçiyor.")
except Exception as e:
    print(f"❌ Test sırasında hata: {e}")

# Sensörler
try: dht_device = adafruit_dht.DHT11(board.D4)
except: 
    if IS_RASPBERRY_PI: print("⚠️ DHT11 Hatası")
    else: dht_device = adafruit_dht.DHT11(board.D4) 

TSL2561_ADDR = 0x39 
try: 
    bus = smbus.SMBus(1)
    bus.write_byte_data(TSL2561_ADDR, 0x80 | 0x00, 0x03)
except: 
    if IS_RASPBERRY_PI: print("⚠️ Işık Sensörü Hatası")
    else: bus = smbus.SMBus(1) 

class AkilliEv:
    def __init__(self):
        self.mevcut_duty = 2.0 
        self.servo_hareket_ettir(0, "Fast")
        self.son_arama_zamani = 0
        self.son_dht_okuma = 0 
        self.sensor_overrides = {} 

        if IS_RASPBERRY_PI:
            self.pipeline = (
                "libcamerasrc ! "
                f"video/x-raw, width={GENISLIK}, height={YUKSEKLIK}, framerate={FPS}/1 ! "
                "videoconvert ! "
                "video/x-raw, format=BGR ! "
                "appsink drop=true sync=false"
            )
            print("📷 RPi Kamera pipeline başlatılıyor...")
            self.video = cv2.VideoCapture(self.pipeline, cv2.CAP_GSTREAMER)
        else:
            print("📷 PC Webcam başlatılıyor...")
            self.video = cv2.VideoCapture(0) 
            
        time.sleep(2.0)

        self.client = mqtt.Client()
        self.client.on_connect = self.on_connect
        self.client.on_message = self.on_message
        
        try:
            print(f"📡 VDS'e bağlanılıyor ({VDS_IP})...")
            self.client.connect(VDS_IP, MQTT_PORT, 60)
            self.client.loop_start()
        except Exception as e: print(f"❌ MQTT Hatası: {e}")

        threading.Thread(target=self.kamera_dongusu, daemon=True).start()
        threading.Thread(target=self.sensor_dongusu, daemon=True).start()
        threading.Thread(target=self.konsol_girdisi_dinle, daemon=True).start()

    def on_connect(self, client, userdata, flags, rc):
        print("✅ MQTT Bağlandı!")
        client.subscribe(KONU_CMD_MAIL)
        client.subscribe(KONU_CMD_WP)
        client.subscribe(KONU_TENTE)
        client.subscribe(KONU_HEATER)
        client.subscribe(KONU_COOLER)
        client.subscribe(KONU_LAMBA)

    def on_message(self, client, userdata, msg):
        payload = msg.payload.decode().strip() 
        topic = msg.topic
        
        if topic == KONU_CMD_MAIL:
            print(f"📩 Mail İsteği: {payload}")
            threading.Thread(target=self.video_kaydet_gonder, args=(payload, "mail")).start()
            
        elif topic == KONU_CMD_WP:
            print(f"📱 WhatsApp İsteği: {payload}")
            hedef_no = payload.replace(" ", "").replace("+", "")
            if hedef_no.startswith("0"): hedef_no = hedef_no[1:]
            if len(hedef_no) == 10 and hedef_no.startswith("5"): hedef_no = "90" + hedef_no
            threading.Thread(target=self.video_kaydet_gonder, args=(hedef_no, "wp")).start()

        elif topic == KONU_TENTE:
            try:
                parts = payload.split(';')
                threading.Thread(target=self.servo_hareket_ettir, args=(int(parts[0]), parts[1])).start()
            except: pass
        
        elif topic == KONU_HEATER:
            print(f"🔥 Isıtıcı Komutu: {payload}")
            if payload == "1": GPIO.output(PIN_HEATER, GPIO.HIGH) 
            else: GPIO.output(PIN_HEATER, GPIO.LOW)

        elif topic == KONU_COOLER:
            print(f"❄️ Fan Hızı: %{payload}")
            try:
                hiz = int(float(payload))
                hiz = max(0, min(100, hiz)) 
                fan_pwm.ChangeDutyCycle(hiz) 
            except: print("❌ Fan hızı hatası")

        elif topic == KONU_LAMBA:
            print(f"💡 Lamba Komutu Geldi: {payload}")
            try:
                val = int(float(payload))
                val = max(0, min(100, val))
                aydinlatma_pwm.ChangeDutyCycle(val)
            except: print("❌ Aydınlatma veri hatası")

    def ifttt_arama_yap(self):
        if time.time() - self.son_arama_zamani < 300: return
        print("☎️ ACİL DURUM! IFTTT aranıyor...")
        try:
            requests.post(f"https://maker.ifttt.com/trigger/{IFTTT_EVENT}/with/key/{IFTTT_KEY}")
            self.son_arama_zamani = time.time()
        except: pass

    def servo_hareket_ettir(self, hedef_yuzde, hiz_modu):
        if not IS_RASPBERRY_PI:
            print(f"   [SİMÜLASYON] Servo -> {hedef_yuzde}% ({hiz_modu})")
            return

        hedef_duty = 2.0 + (hedef_yuzde * 0.05)
        adim = 0.1 
        if hiz_modu == "Slow": adim = 0.02
        elif hiz_modu == "Medium": adim = 0.06
        elif hiz_modu == "Fast": adim = 0.20
            
        cur = self.mevcut_duty
        yon = 1 if hedef_duty > cur else -1
        while True:
            if abs(cur - hedef_duty) < adim:
                cur = hedef_duty
                break
            cur += (adim * yon)
            servo.ChangeDutyCycle(cur)
            time.sleep(0.02) 
        servo.ChangeDutyCycle(hedef_duty)
        time.sleep(0.1) 
        self.mevcut_duty = hedef_duty
        servo.ChangeDutyCycle(0)
    
    def konsol_girdisi_dinle(self):
        time.sleep(2) 
        print("\n" + "="*40)
        print("🛠️  SENSÖR TEST MODU AKTİF")
        print("Aşağıdaki komutları yazarak değerleri değiştirebilirsiniz:")
        print("  sicaklik <değer>  -> Örn: sicaklik 45")
        print("  gaz <1/0>         -> Örn: gaz 1")
        print("  yagmur <1/0>      -> Örn: yagmur 1")
        print("  isik <0-100>      -> Örn: isik 80")
        print("  mesafe <1/0>      -> Örn: mesafe 1")
        print("  auto              -> Sensör okumaya geri dön")
        print("="*40 + "\n")
        
        while True:
            try:
                komut = input("Komut Girin: ").strip().lower()
                if not komut: continue
                parts = komut.split()
                anahtar = parts[0]
                if anahtar == "auto":
                    self.sensor_overrides.clear()
                    print("✅ OTOMATİK MOD: Gerçek sensör değerleri okunuyor.")
                    continue
                if len(parts) < 2:
                    print("❌ Eksik parametre! Örn: 'sicaklik 30'")
                    continue
                deger = parts[1]
                if anahtar in ["sicaklik", "gaz", "yagmur", "isik", "mesafe", "nem"]:
                    self.sensor_overrides[anahtar] = deger
                    print(f"🔒 {anahtar.upper()} sabitlendi: {deger} (Sensör devre dışı)")
                else:
                    print("❌ Bilinmeyen komut.")
            except Exception as e:
                print(f"Hata: {e}")

    def sensor_dongusu(self):
        print("🔍 Sensör okuma başladı...")
        while True:
            try:
                # 1. GAZ
                if 'gaz' in self.sensor_overrides:
                    self.client.publish(KONU_GAZ, str(self.sensor_overrides['gaz']))
                else:
                    if GPIO.input(PIN_GAZ) == 0: 
                        time.sleep(0.2)
                        if GPIO.input(PIN_GAZ) == 0:
                            self.client.publish(KONU_GAZ, "1")
                            self.ifttt_arama_yap()
                        else: self.client.publish(KONU_GAZ, "0")
                    else: self.client.publish(KONU_GAZ, "0")

                # 2. YAĞMUR
                if 'yagmur' in self.sensor_overrides:
                    self.client.publish(KONU_YAGMUR, str(self.sensor_overrides['yagmur']))
                else:
                    yagmur = "1" if GPIO.input(PIN_YAGMUR) == 0 else "0"
                    self.client.publish(KONU_YAGMUR, yagmur)

                # 3. MESAFE (GÜNCELLENDİ: HASSASİYET 10 CM)
                if 'mesafe' in self.sensor_overrides:
                    self.client.publish(KONU_MESAFE, str(self.sensor_overrides['mesafe']))
                else:
                    GPIO.output(PIN_TRIG, True)
                    time.sleep(0.00001)
                    GPIO.output(PIN_TRIG, False)
                    
                    bas = time.time()
                    timeout_start = time.time()
                    while GPIO.input(PIN_ECHO) == 0:
                        bas = time.time()
                        if bas - timeout_start > 0.04: break 

                    bit = time.time()
                    timeout_end = time.time()
                    while GPIO.input(PIN_ECHO) == 1:
                        bit = time.time()
                        if bit - timeout_end > 0.04: break
                        
                    mesafe = (bit - bas) * 17150
                    
                    # ⚠️ HASSASİYET AYARI: Sadece 10 cm altını algıla
                    if mesafe < 10 and mesafe > 2: 
                        self.client.publish(KONU_MESAFE, "1")
                    else:
                        self.client.publish(KONU_MESAFE, "0")

                # 4. IŞIK
                if 'isik' in self.sensor_overrides:
                    self.client.publish(KONU_ISIK, str(self.sensor_overrides['isik']))
                else:
                    try:
                        low = bus.read_byte_data(TSL2561_ADDR, 0x80 | 0x0C)
                        high = bus.read_byte_data(TSL2561_ADDR, 0x80 | 0x0D)
                        val = high * 256 + low
                        yuzde = min(int((val / 5000) * 100), 100)
                        self.client.publish(KONU_ISIK, str(yuzde))
                    except: pass

                # 5. SICAKLIK
                if 'sicaklik' in self.sensor_overrides:
                    self.client.publish(KONU_SICAKLIK, str(self.sensor_overrides['sicaklik']))
                    if 'nem' in self.sensor_overrides:
                        self.client.publish(KONU_NEM, str(self.sensor_overrides['nem']))
                else:
                    if time.time() - self.son_dht_okuma > 2.0:
                        try:
                            t = dht_device.temperature
                            if t is not None:
                                self.client.publish(KONU_SICAKLIK, str(t))
                                self.son_dht_okuma = time.time()
                        except: pass

            except Exception as e: print(f"Sensör Hatası: {e}")
            time.sleep(0.5)

    def kamera_dongusu(self):
        while True:
            if self.video.isOpened():
                ret, frame = self.video.read()
                if ret:
                    self.frame = frame
                    with lock: video_buffer.append(frame)
                else: time.sleep(0.1)
            else: break

    def get_jpg(self):
        if not hasattr(self, 'frame') or self.frame is None: return None
        ret, jpeg = cv2.imencode('.jpg', self.frame, [int(cv2.IMWRITE_JPEG_QUALITY), 40])
        return jpeg.tobytes()

    def video_kaydet_gonder(self, hedef, tip="mail"):
        filename = "olay_kaydi.mp4"
        print("📼 Video hazırlanıyor...")
        with lock: frames = list(video_buffer)
        if len(frames) < 10: return

        out = cv2.VideoWriter(filename, cv2.VideoWriter_fourcc(*'mp4v'), FPS, (GENISLIK, YUKSEKLIK))
        for f in frames: out.write(f)
        out.release()
        print(f"✅ Video kaydedildi: {filename}")

        if tip == "mail": self.mail_at(hedef, filename)
        elif tip == "wp": self.whatsapp_video_greenapi(hedef, filename)

    def mail_at(self, alici, dosya_yolu):
        try:
            msg = EmailMessage()
            msg['Subject'] = 'Güvenlik: Kamera Kaydı'
            msg['From'] = GMAIL_USER
            msg['To'] = alici
            msg.set_content('Talep edilen kayıt ektedir.')
            with open(dosya_yolu, 'rb') as f: 
                msg.add_attachment(f.read(), maintype='video', subtype='mp4', filename=f.name)
            with smtplib.SMTP_SSL('smtp.gmail.com', 465) as smtp:
                smtp.login(GMAIL_USER, GMAIL_PASS)
                smtp.send_message(msg)
            print(f"✅ Mail gönderildi: {alici}")
        except Exception as e: print(f"❌ Mail Hatası: {e}")

    def whatsapp_video_greenapi(self, numara, dosya_yolu):
        print(f"🚀 Green API ile Video Gönderiliyor: {numara}")
        url = f"https://api.green-api.com/waInstance{GREEN_INSTANCE_ID}/sendFileByUpload/{GREEN_API_TOKEN}"
        chat_id = f"{numara}@c.us"
        try:
            with open(dosya_yolu, 'rb') as f:
                files = {'file': f}
                payload = {'chatId': chat_id, 'fileName': 'guvenlik_kaydi.mp4', 'caption': '⚠️ Hareket Algılandı!'}
                response = requests.post(url, data=payload, files=files)
                if response.status_code == 200: print("✅ WhatsApp Video Başarıyla Gönderildi!")
                else: print(f"❌ Green API Hatası: {response.text}")
        except Exception as e: print(f"❌ Bağlantı Hatası: {e}")

sistem = AkilliEv()
def generate():
    while True:
        frame = sistem.get_jpg()
        if frame: yield (b'--frame\r\n' b'Content-Type: image/jpeg\r\n\r\n' + frame + b'\r\n')
        else: time.sleep(0.05)

@app.route('/')
def video_feed():
    response = Response(generate(), mimetype='multipart/x-mixed-replace; boundary=frame')
    response.headers["Cache-Control"] = "no-cache, no-store, must-revalidate"
    response.headers["Pragma"] = "no-cache"
    response.headers["Expires"] = "0"
    return response

# --- YENİ EKLENEN KISIM: OTOMATİK NGROK BAŞLATMA ---
if __name__ == '__main__':
    # Ngrok'u başlat (Eğer kütüphane varsa)
    # DÜZELTME: Token uzunluk kontrolü yapılıyor (Boş değilse başlat)
    if NGROK_MEVCUT and len(NGROK_AUTH_TOKEN) > 10:
        try:
            ngrok.set_auth_token(NGROK_AUTH_TOKEN)
            public_url = ngrok.connect(5050)
            print("\n" + "*"*50)
            print(f"🌍 CANLI YAYIN (Ngrok) ADRESİNİZ: {public_url}")
            print("*"*50 + "\n")
        except Exception as e:
            print(f"⚠️ Ngrok başlatılamadı: {e}")
    else:
        print("⚠️ Ngrok Token eksik veya pyngrok yüklü değil. Sadece yerel ağda çalışacak.")

    try: app.run(host='0.0.0.0', port=5050, threaded=True, debug=False)
    finally: GPIO.cleanup()
