import time
import paho.mqtt.client as mqtt
import RPi.GPIO as GPIO

# ==========================================
# ⚙️ AYARLAR
# ==========================================
VDS_DOMAIN = "nart3d.com" 
MQTT_PORT = 1883

# MQTT KONULARI
TOPIC_BASE     = "Nest/home"
KONU_YAGMUR    = f"{TOPIC_BASE}/sensor/yagmur"
KONU_TENTE     = f"{TOPIC_BASE}/command/tente"
KONU_AYDINLATMA= f"{TOPIC_BASE}/command/aydinlatma" # Yeni eklendi

# Pin Ayarları
PIN_YAGMUR = 17
PIN_SERVO  = 18
PIN_AYDINLATMA = 27 # Aydınlatma (Röle/LED) için eklendi

GPIO.setmode(GPIO.BCM)
GPIO.setup(PIN_YAGMUR, GPIO.IN)
GPIO.setup(PIN_SERVO, GPIO.OUT)
GPIO.setup(PIN_AYDINLATMA, GPIO.OUT)
GPIO.output(PIN_AYDINLATMA, False) # Başlangıçta kapalı olsun

# PWM Ayarı (Servo için)
pwm = GPIO.PWM(PIN_SERVO, 50)
pwm.start(0)

def set_servo_angle(angle):
    duty = angle / 18 + 2.5
    GPIO.output(PIN_SERVO, True)
    pwm.ChangeDutyCycle(duty)
    time.sleep(0.5)
    GPIO.output(PIN_SERVO, False)
    pwm.ChangeDutyCycle(0)

# ==========================================
# 📡 MQTT KURULUMU
# ==========================================
def on_connect(client, userdata, flags, rc):
    if rc == 0:
        print("✅ MQTT Broker'a Bağlandı!")
        # TÜM NEST KANALLARINI DİNLE (Debug için)
        client.subscribe("Nest/#")
        print(f"📡 Nest/# kanalları dinleniyor...")
    else:
        print(f"❌ Bağlantı Hatası: {rc}")

current_angle = 0

def set_servo_angle_with_speed(target_angle, speed_percent):
    global current_angle
    
    # Hızı gecikmeye çevir (Hız arttıkça gecikme azalır)
    # %100 hız -> 0.001s gecikme, %10 hız -> 0.1s gecikme
    delay = (101 - speed_percent) / 1000.0
    
    step = 1 if target_angle > current_angle else -1
    
    print(f"🔄 Hareket Başladı: {current_angle} -> {target_angle} (Hız: %{speed_percent})")
    
    for angle in range(int(current_angle), int(target_angle), step):
        duty = angle / 18 + 2.5
        pwm.ChangeDutyCycle(duty)
        time.sleep(delay)
    
    # Hedef açıya tam oturt
    pwm.ChangeDutyCycle(target_angle / 18 + 2.5)
    time.sleep(0.1)
    pwm.ChangeDutyCycle(0) # Titremeyi önlemek için akımı kes
    current_angle = target_angle
    print("✅ Hedefe ulaşıldı.")

def on_message(client, userdata, msg):
    try:
        import json
        payload_str = msg.payload.decode().strip()
        print(f"📩 Komut Geldi: {msg.topic} -> {payload_str}")
        
        if msg.topic == KONU_TENTE:
            # Backend'den JSON formatında veri bekliyoruz: {"position": 50, "speed": 50}
            data = json.loads(payload_str)
            opening_percent = int(data.get("position", 0))
            speed = int(data.get("speed", 50))
            
            target_angle = (opening_percent * 180) / 100
            set_servo_angle_with_speed(target_angle, speed)
            
        elif msg.topic == KONU_AYDINLATMA:
            # Örn: {"state": "ON", "brightness": 100}
            data = json.loads(payload_str)
            state = data.get("state", "OFF")
            
            if state == "ON":
                GPIO.output(PIN_AYDINLATMA, True)
                print("💡 Aydınlatma AÇILDI")
            else:
                GPIO.output(PIN_AYDINLATMA, False)
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
# 🔄 ANA DÖNGÜ (Yağmur Verisi Gönderimi)
# ==========================================
print("🚀 SİSTEM ÇİFT YÖNLÜ ÇALIŞIYOR...")

while True:
    try:
        durum = GPIO.input(PIN_YAGMUR)
        yagmur_var_mi = "1" if durum == 0 else "0"
        client.publish(KONU_YAGMUR, yagmur_var_mi)
    except Exception as e:
        print(f"❌ Hata: {e}")
    time.sleep(3)
