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

# Pin Ayarı (GPIO 17)
PIN_YAGMUR = 17

GPIO.setmode(GPIO.BCM)
GPIO.setup(PIN_YAGMUR, GPIO.IN)

# ==========================================
# 📡 MQTT KURULUMU
# ==========================================
def on_connect(client, userdata, flags, rc):
    if rc == 0:
        print("✅ MQTT Broker'a (nart3d.com) Bağlandı!")
    else:
        print(f"❌ Bağlantı Hatası! Kod: {rc}")

client = mqtt.Client()
client.on_connect = on_connect

try:
    print(f"📡 {VDS_DOMAIN} adresine bağlanılıyor...")
    client.connect(VDS_DOMAIN, MQTT_PORT, 60)
    client.loop_start()
except Exception as e:
    print(f"❌ MQTT Bağlantı Hatası: {e}")

# ==========================================
# 🔄 ANA DÖNGÜ (Sadece Yağmur)
# ==========================================
print("🚀 YAĞMUR SENSÖRÜ VERİ GÖNDERİMİ BAŞLADI...")

while True:
    try:
        # Sensörden oku
        durum = GPIO.input(PIN_YAGMUR)
        yagmur_var_mi = "1" if durum == 0 else "0"
        
        # MQTT'ye gönder
        client.publish(KONU_YAGMUR, yagmur_var_mi)
        
        durum_metni = "YAĞMUR VAR 💧" if yagmur_var_mi == "1" else "Hava Kuru ☀️"
        print(f"📝 VDS'e Gönderildi -> {durum_metni}")

    except Exception as e:
        print(f"❌ Hata: {e}")
    
    time.sleep(3)
