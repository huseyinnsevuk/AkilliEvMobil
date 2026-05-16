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

# Pin Ayarları
PIN_YAGMUR = 17
PIN_SERVO  = 18

GPIO.setmode(GPIO.BCM)
GPIO.setup(PIN_YAGMUR, GPIO.IN)
GPIO.setup(PIN_SERVO, GPIO.OUT)

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
        # KOMUT KANALINA ABONE OL
        client.subscribe(KONU_TENTE)
        print(f"📡 {KONU_TENTE} kanalı dinleniyor...")
    else:
        print(f"❌ Bağlantı Hatası: {rc}")

def on_message(client, userdata, msg):
    payload = msg.payload.decode().strip()
    print(f"📩 Komut Geldi: {msg.topic} -> {payload}")
    
    if msg.topic == KONU_TENTE:
        if payload == "1":
            print("➡️ Tente Açılıyor (180 derece)...")
            set_servo_angle(180)
        else:
            print("➡️ Tente Kapanıyor (0 derece)...")
            set_servo_angle(0)

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
