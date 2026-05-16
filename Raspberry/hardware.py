import time
import paho.mqtt.client as mqtt
import adafruit_dht
import board

# ==========================================
# ⚙️ AYARLAR
# ==========================================
VDS_DOMAIN = "nart3d.com" 
MQTT_PORT = 1883

# MQTT KONULARI
TOPIC_BASE     = "Nest/home"
KONU_SICAKLIK  = f"{TOPIC_BASE}/sensor/sicaklik"
KONU_NEM       = f"{TOPIC_BASE}/sensor/nem"

# DHT11 Sensör Ayarı (Orijinal pininiz D4 korunmuştur)
try:
    dht_device = adafruit_dht.DHT11(board.D4)
    print("✅ DHT11 Sensörü Hazır (Pin: D4)")
except Exception as e:
    print(f"❌ Sensör Başlatılamadı: {e}")

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
# 🔄 ANA DÖNGÜ (Sadece Veri Gönderimi)
# ==========================================
print("🚀 Veri gönderimi başlıyor (5 saniyede bir)...")

while True:
    try:
        # Sensörden verileri oku
        temperature = dht_device.temperature
        humidity = dht_device.humidity

        if temperature is not None and humidity is not None:
            # MQTT'ye gönder
            client.publish(KONU_SICAKLIK, str(temperature))
            client.publish(KONU_NEM, str(humidity))
            
            print(f"📝 Gönderildi -> Sıcaklık: {temperature}°C | Nem: %{humidity}")
        else:
            print("⚠️ Sensörden veri okunamadı, tekrar deneniyor...")

    except RuntimeError as error:
        # DHT bazen okuma hatası verebilir, devam et
        print(f"⚠️ Okuma uyarısı: {error.args[0]}")
    except Exception as e:
        print(f"❌ Beklenmedik Hata: {e}")
    
    time.sleep(5)
