import time
import paho.mqtt.client as mqtt
import RPi.GPIO as GPIO
import cv2
import http.server
import socketserver
import threading

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

# Pin Ayarları
PIN_YAGMUR = 17
PIN_SERVO  = 18

# Aydınlatma (L298N Kanal B)
PIN_LIGHT_PWM  = 13  # ENB (Parlaklık - Hız)
PIN_LIGHT_IN3  = 20  # IN3 (Yön +) 
PIN_LIGHT_IN4  = 21  # IN4 (Yön -) 

GPIO.setmode(GPIO.BCM)
GPIO.setup(PIN_YAGMUR, GPIO.IN)
GPIO.setup(PIN_SERVO, GPIO.OUT)

# L298N Aydınlatma Pin Kurulumları
GPIO.setup(PIN_LIGHT_PWM, GPIO.OUT)
GPIO.setup(PIN_LIGHT_IN3, GPIO.OUT)
GPIO.setup(PIN_LIGHT_IN4, GPIO.OUT)

# Aydınlatma Yönünü Ayarla (Akım IN3'ten IN4'e akacak)
GPIO.output(PIN_LIGHT_IN3, GPIO.HIGH)
GPIO.output(PIN_LIGHT_IN4, GPIO.LOW)

# Aydınlatma için PWM (L298N üzerinden parlaklık)
pwm_aydinlatma = GPIO.PWM(PIN_LIGHT_PWM, 1000) # Kırpışmayı önlemek için 1000 Hz
pwm_aydinlatma.start(0) # Başlangıçta %0 duty cycle (kapalı)

# PWM Ayarı (Servo için)
pwm = GPIO.PWM(PIN_SERVO, 50)
pwm.start(0)

# --- BAŞLANGIÇ TESTİ (Debug İçin) ---
print("🧪 DONANIM TESTİ BAŞLIYOR... (Lamba 2 saniye yanmalı)")
try:
    GPIO.output(PIN_LIGHT_IN3, GPIO.HIGH) # Yönü aç
    pwm_aydinlatma.ChangeDutyCycle(100) 
    time.sleep(2)
    
    # Tamamen Kapat
    pwm_aydinlatma.ChangeDutyCycle(0)   
    GPIO.output(PIN_LIGHT_IN3, GPIO.LOW)  # Kaçak voltajı engelle
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
class CameraCapture:
    def __init__(self):
        self.width = 640
        self.height = 480
        self.fps = 30
        self.frame = None
        self.lock = threading.Lock()
        
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
        
        # Arka planda frame okuma döngüsünü başlat
        self.running = True
        self.thread = threading.Thread(target=self._capture_loop, daemon=True)
        self.thread.start()

    def _capture_loop(self):
        print("🏃 Arka planda kamera okuma döngüsü başladı...")
        first_frame = True
        frame_count = 0
        
        while self.running:
            if self.video.isOpened():
                ret, frame = self.video.read()
                if ret:
                    if first_frame:
                        print("🎉 GStreamer üzerinden ilk kamera karesi başarıyla okundu!")
                        first_frame = False
                    
                    # 180 Derece Döndürme (Ters montaj için)
                    frame = cv2.rotate(frame, cv2.ROTATE_180)
                    
                    frame_count += 1
                    if frame_count % 90 == 0:
                        print(f"📡 Canlı yayından 90 kare başarıyla okundu (Toplam: {frame_count})")
                        
                    with self.lock:
                        self.frame = frame
                else:
                    time.sleep(0.03)
            else:
                time.sleep(0.1)

    def get_frame(self):
        with self.lock:
            if self.frame is None:
                return None
            ret, jpeg = cv2.imencode('.jpg', self.frame, [int(cv2.IMWRITE_JPEG_QUALITY), 50])
            if ret:
                return jpeg.tobytes()
        return None

    def close(self):
        self.running = False
        print("🔌 Kamera kaynakları serbest bırakılıyor...")
        if self.video.isOpened():
            self.video.release()

camera = None

# ==========================================
# 📡 HTTP MJPEG STREAMING SERVER
# ==========================================
class StreamingHandler(http.server.BaseHTTPRequestHandler):
    def log_message(self, format, *args):
        # Konsol kirliliğini önlemek için HTTP loglarını sessize alıyoruz
        return

    def do_GET(self):
        global camera
        if self.path == '/':
            self.send_response(301)
            self.send_header('Location', '/index.html')
            self.end_headers()
        elif self.path == '/index.html':
            # Uygulama doğrudan /stream.mjpg adresini de çekebilir. 
            # Tarayıcı testi için şık bir HTML sayfası
            PAGE = """\
            <html>
            <head><title>Akıllı Ev - Canlı Kamera</title></head>
            <body style="background:#0f172a; color:white; font-family:sans-serif; text-align:center; padding-top:50px;">
                <h2>Akıllı Ev Kamera Sistemi</h2>
                <img src="stream.mjpg" style="border: 3px solid #4a90e2; border-radius:12px; max-width:640px;" />
                <p>Protokol: MJPEG (HTTP Stream) | Çözünürlük: 640x480 (180 Derece Çevrilmiş)</p>
            </body>
            </html>
            """
            content = PAGE.encode('utf-8')
            self.send_response(200)
            self.send_header('Content-Type', 'text/html')
            self.send_header('Content-Length', len(content))
            self.end_headers()
            self.wfile.write(content)
        elif self.path == '/stream.mjpg':
            self.send_response(200)
            self.send_header('Age', 0)
            self.send_header('Cache-Control', 'no-cache, private')
            self.send_header('Pragma', 'no-cache')
            self.send_header('Content-Type', 'multipart/x-mixed-replace; boundary=frame')
            self.end_headers()
            try:
                while True:
                    if camera:
                        frame = camera.get_frame()
                        if frame:
                            self.wfile.write(b'--frame\r\n')
                            self.send_header('Content-Type', 'image/jpeg')
                            self.send_header('Content-Length', len(frame))
                            self.end_headers()
                            self.wfile.write(frame)
                            self.wfile.write(b'\r\n')
                        else:
                            time.sleep(0.03)
                    else:
                        time.sleep(0.1)
            except Exception as e:
                pass
        else:
            self.send_error(404)

class StreamingServer(socketserver.ThreadingMixIn, socketserver.TCPServer):
    allow_reuse_address = True
    daemon_threads = True

def baslat_kamera_yayini():
    global camera
    PORT = 8000
    try:
        camera = CameraCapture()
        server_address = ('', PORT)
        server = StreamingServer(server_address, StreamingHandler)
        print(f"📡 Kamera Yayın Sunucusu Arka Planda Başlatıldı! (Port: {PORT})")
        server.serve_forever()
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
    delay = (101 - speed_percent) / 1000.0
    step = 1 if target_angle > current_angle else -1
    
    print(f"🔄 Hareket Başladı: {current_angle} -> {target_angle} (Hız: %{speed_percent})")
    
    for angle in range(int(current_angle), int(target_angle), step):
        duty = angle / 18 + 2.5
        pwm.ChangeDutyCycle(duty)
        time.sleep(delay)
    
    pwm.ChangeDutyCycle(target_angle / 18 + 2.5)
    time.sleep(0.1)
    pwm.ChangeDutyCycle(0) # Titremeyi önle
    current_angle = target_angle
    print("✅ Hedefe ulaşıldı.")

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
            
    except Exception as e:
        print(f"❌ Komut işleme hatası: {e}")

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

print("🚀 SİSTEM ÇİFT YÖNLÜ ÇALIŞIYOR...")

while True:
    try:
        durum = GPIO.input(PIN_YAGMUR)
        yagmur_var_mi = "1" if durum == 0 else "0"
        client.publish(KONU_YAGMUR, yagmur_var_mi)
    except Exception as e:
        print(f"❌ Hata: {e}")
    time.sleep(3)
