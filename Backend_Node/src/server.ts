import express from 'express';
import cors from 'cors';
import { PrismaClient } from '@prisma/client';
import dotenv from 'dotenv';
import Stripe from 'stripe';
import fetch from 'node-fetch';
import mqtt from 'mqtt';

dotenv.config();
console.log("-----------------------------------------");
console.log("🚀 SURUM 2.0 - GUNCEL KOD AKTIF!");
console.log("-----------------------------------------");

const prisma = new PrismaClient();
const app = express();
app.use(cors());
app.use(express.json());

// MQTT Bridge Ayarları
const mqttClient = mqtt.connect('mqtt://localhost');

// Cihaz Kontrol API (Mobil -> Backend -> MQTT -> Raspi)
app.post('/api/devices/control', async (req, res) => {
  console.log('📬 Yeni komut isteği geldi:', JSON.stringify(req.body));
  try {
    const { deviceType, data } = req.body;
    if (!deviceType || !data) {
      console.warn('⚠️ Eksik veri: body içinden deviceType veya data alınamadı!');
      return res.status(400).json({ error: 'Eksik veri' });
    }
    const topic = `Nest/home/command/${deviceType}`;
    const payload = JSON.stringify(data);
    console.log(`📡 MQTT Bağlantı Durumu: ${mqttClient.connected ? 'BAĞLI' : 'BAĞLI DEĞİL'}`);
    if (mqttClient.connected) {
      mqttClient.publish(topic, payload, (err) => {
        if (err) console.error('❌ MQTT Publish Hatası:', err);
        else console.log(`🚀 Mesaj Broker'a başarıyla iletildi: ${topic} -> ${payload}`);
      });
    } else {
      console.error('❌ MQTT Broker\'a bağlı değiliz! Komut gönderilemedi.');
    }
    await logActivity('DEVICE_CONTROL', 'Cihaz Kontrolü', `${deviceType} cihazına komut gönderildi.`);
    res.json({ success: true, message: 'Komut iletildi' });
  } catch (err) {
    console.error('❌ Komut işleme hatası:', err);
    res.status(500).json({ error: 'Komut iletilemedi' });
  }
});



mqttClient.on('connect', () => {
  console.log('✅ Backend MQTT Broker\'a bağlandı!');
  mqttClient.subscribe('Nest/home/sensor/#', (err) => {
    if (!err) console.log('📡 Nest sensör kanalları dinleniyor...');
  });
});

mqttClient.on('message', async (topic, message) => {
  const payload = message.toString();
  const sensorType = topic.split('/').pop(); // sicaklik, nem, gaz vb.

  try {
    // 1. Cihazı bul, yoksa otomatik oluştur
    let device = await prisma.device.findFirst();
    
    if (!device) {
      console.log('📝 İlk cihaz bulunamadı, "Ana Kontrol Birimi" oluşturuluyor...');
      // Rastgele bir kullanıcı bul (cihaz bir kullanıcıya bağlı olmalı)
      const user = await prisma.user.findFirst();
      if (!user) {
        console.error('❌ Cihaz oluşturulamadı: Veritabanında kayıtlı kullanıcı yok!');
        return;
      }

      device = await prisma.device.create({
        data: {
          name: 'Ana Kontrol Birimi (Raspi)',
          macAddress: 'AA:BB:CC:DD:EE:FF',
          type: 'RaspberryPi',
          userId: user.id
        }
      });
    }

    // 2. Sensör verisini logla
    await prisma.sensorLog.create({
      data: {
        deviceId: device.id,
        temperature: sensorType === 'sicaklik' ? parseFloat(payload) : 0,
        humidity: sensorType === 'nem' ? parseFloat(payload) : 0,
        isRaining: sensorType === 'yagmur' ? payload === '1' : false,
        gasDetected: sensorType === 'gaz' ? payload === '1' : false,
        lightLevel: sensorType === 'isik' ? parseFloat(payload) : 0,
      }
    });
    console.log(`📝 Sensör Kaydı (${device.name}): ${sensorType} -> ${payload}`);
  } catch (err) {
    console.error('❌ Sensör verisi kaydedilemedi:', err);
  }
});

app.get('/api/sensors/latest', async (req, res) => {
  try {
    const latestTempLog = await prisma.sensorLog.findFirst({
      where: { temperature: { gt: 0 } },
      orderBy: { createdAt: 'desc' }
    });

    const latestHumLog = await prisma.sensorLog.findFirst({
      where: { humidity: { gt: 0 } },
      orderBy: { createdAt: 'desc' }
    });

    const latestLightLog = await prisma.sensorLog.findFirst({
      where: { lightLevel: { gt: 0 } },
      orderBy: { createdAt: 'desc' }
    });

    const absoluteLatestLog = await prisma.sensorLog.findFirst({
      orderBy: { createdAt: 'desc' }
    });

    res.json({
      temperature: latestTempLog?.temperature ?? 22,
      humidity: latestHumLog?.humidity ?? 45,
      isRaining: absoluteLatestLog?.isRaining ?? false,
      gasDetected: absoluteLatestLog?.gasDetected ?? false,
      lightLevel: latestLightLog?.lightLevel ?? 250,
      createdAt: absoluteLatestLog?.createdAt ?? new Date()
    });
  } catch (err) {
    res.status(500).json({ error: 'Sensör verisi alınamadı' });
  }
});


const stripeKey = process.env.STRIPE_SECRET_KEY || 'sk_test_dummy_key';
const stripe = new Stripe(stripeKey, {
  apiVersion: '2025-01-27.acacia' as any
});


const PORT = Number(process.env.PORT) || 3000;


// Log middleware
app.use((req, res, next) => {
  console.log(`${new Date().toISOString()} - ${req.method} ${req.url}`);
  next();
});

// Aktivite Kaydı Yardımcı Fonksiyonu
async function logActivity(type: string, title: string, description: string) {
  try {
    await prisma.activityLog.create({
      data: { type, title, description }
    });
  } catch (err) {
    console.error('Aktivite kaydedilemedi:', err);
  }
}

// Sistem Sağlık Kontrolü
app.get('/api/health', (req, res) => {
  res.json({ status: 'Online', database: 'PostgreSQL Active' });
});

// Tüm Kullanıcıları Getir (Admin Paneli İçin)
app.get('/api/users', async (req, res) => {
  try {
    const users = await prisma.user.findMany();
    res.json(users);
  } catch (error) {
    res.status(500).json({ error: 'Kullanıcılar alınırken hata oluştu.' });
  }
});

// Yeni Kullanıcı Ekleme
app.post('/api/users', async (req, res) => {
  try {
    const { fullName, email, phoneNumber, passwordHash } = req.body;
    
    // Şimdilik varsayılan doğrulama ile kayıt diyelim
    const newUser = await prisma.user.create({
      data: {
        fullName,
        email,
        phoneNumber,
        passwordHash: passwordHash || 'firebase-handled',
      }
    });
    
    await logActivity('USER_REGISTER', 'Yeni Kullanıcı Kaydı', `${fullName} sisteme dahil oldu.`);
    res.status(201).json(newUser);
  } catch (error) {
    console.error(error);
    res.status(400).json({ error: 'Email veya Telefon numarası zaten sistemde kayıtlı olabilir.' });
  }
});

// Kullanıcı Durumunu Değiştir (Kilitle / Aç)
app.put('/api/users/:id/status', async (req, res) => {
  try {
    const { id } = req.params;
    const { isActive } = req.body;

    const updateData: any = { isActive };
    
    // Eğer hesap kilitleniyorsa, otomatik olarak Basic pakete düşür
    if (isActive === false) {
      updateData.subscriptionType = 'Basic';
    }

    const updatedUser = await prisma.user.update({
      where: { id },
      data: updateData
    });

    res.json(updatedUser);
  } catch (error) {
    console.error(error);
    res.status(500).json({ error: 'Kullanıcı durumu güncellenemedi.' });
  }
});

// Kullanıcı Paketini Değiştir (Basic / Premium)
app.put('/api/users/:id/plan', async (req, res) => {
  try {
    const { id } = req.params;
    const { subscriptionType } = req.body;

    const updatedUser = await prisma.user.update({
      where: { id },
      data: { subscriptionType }
    });

    res.json(updatedUser);
  } catch (error) {
    console.error(error);
    res.status(500).json({ error: 'Kullanıcı paketi güncellenemedi.' });
  }
});

// Kullanıcıya ait en son sensör verilerini getir
app.get('/api/users/:userId/sensors/latest', async (req, res) => {
  try {
    const { userId } = req.params;
    
    // Kullanıcının bir cihazını bul
    const device = await prisma.device.findFirst({
      where: { userId }
    });

    if (!device) {
      return res.status(404).json({ error: 'Bu kullanıcıya ait bir cihaz bulunamadı.' });
    }

    const latestTempLog = await prisma.sensorLog.findFirst({
      where: { deviceId: device.id, temperature: { gt: 0 } },
      orderBy: { createdAt: 'desc' }
    });

    const latestHumLog = await prisma.sensorLog.findFirst({
      where: { deviceId: device.id, humidity: { gt: 0 } },
      orderBy: { createdAt: 'desc' }
    });

    const latestLightLog = await prisma.sensorLog.findFirst({
      where: { deviceId: device.id, lightLevel: { gt: 0 } },
      orderBy: { createdAt: 'desc' }
    });

    const absoluteLatestLog = await prisma.sensorLog.findFirst({
      where: { deviceId: device.id },
      orderBy: { createdAt: 'desc' }
    });

    res.json({
      temperature: latestTempLog?.temperature ?? 0,
      humidity: latestHumLog?.humidity ?? 0,
      isRaining: absoluteLatestLog?.isRaining ?? false,
      gasDetected: absoluteLatestLog?.gasDetected ?? false,
      lightLevel: latestLightLog?.lightLevel ?? 0,
      createdAt: absoluteLatestLog?.createdAt ?? new Date()
    });
  } catch (error) {
    console.error(error);
    res.status(500).json({ error: 'Sensör verileri alınamadı.' });
  }
});

// Stripe Ödeme Oturumu Oluşturma
app.post('/api/payments/create-checkout-session', async (req, res) => {
  try {
    const { userId } = req.body;
    
    // Güncel fiyatı çek
    const settings = await prisma.subscriptionSettings.findUnique({ where: { id: 'default' } });
    const amount = settings ? settings.premiumPrice : 250;

    const session = await stripe.checkout.sessions.create({
      payment_method_types: ['card'],
      line_items: [
        {
          price_data: {
            currency: 'try',
            product_data: {
              name: 'Akıllı Ev Premium Üyelik',
              description: 'Tüm cihaz kısıtlamalarını kaldırır ve gelişmiş özellikler sunar.',
            },
            unit_amount: Math.round(amount * 100), // Kuruş cinsinden
          },
          quantity: 1,
        },
      ],
      mode: 'payment',
      success_url: `http://nart3d.com:3000/api/payments/success?userId=${userId}&amount=${amount}`,
      cancel_url: `http://nart3d.com:3000/api/payments/cancel`,
      metadata: { userId }
    });

    res.json({ url: session.url });
  } catch (error) {
    console.error('Stripe Error:', error);
    res.status(500).json({ error: 'Ödeme oturumu oluşturulamadı.' });
  }
});

// Ödeme Başarılı Callback (Basit Yönlendirme)
app.get('/api/payments/success', async (req, res) => {
    const { userId, amount } = req.query;
    if (userId) {
        const user = await prisma.user.findUnique({ where: { id: userId.toString() } });
        // Veritabanını güncelle
        await prisma.user.update({
            where: { id: userId.toString() },
            data: { subscriptionType: 'Premium' }
        });

        // Ödeme kaydı oluştur
        await prisma.paymentRecord.create({
          data: {
            userId: userId.toString(),
            amount: parseFloat(amount?.toString() || '0'),
            status: 'Success'
          }
        });

        await logActivity('PAYMENT_SUCCESS', 'Ödeme Alındı', `${user?.fullName} tarafından ₺${amount} tutarında ödeme yapıldı.`);
    }
    res.send(`
        <html>
            <body style="display:flex; flex-direction:column; align-items:center; justify-content:center; height:100vh; font-family:sans-serif;">
                <h1 style="font-size:3rem;">🎉</h1>
                <h1>Ödeme Başarılı!</h1>
                <p>Uygulamaya geri yönlendiriliyorsunuz...</p>
                <script>
                    window.location.href = "akilliev://payment-success";
                </script>
            </body>
        </html>
    `);
});

// Abonelik İptali
app.post('/api/payments/cancel-subscription', async (req, res) => {
    try {
        const { userId } = req.body;
        if (!userId) return res.status(400).json({ error: 'UserId gerekli.' });

        await prisma.user.update({
            where: { id: userId },
            data: { subscriptionType: 'Basic' }
        });

        res.json({ message: 'Abonelik başarıyla iptal edildi.' });
    } catch (error) {
        console.error(error);
        res.status(500).json({ error: 'Abonelik iptal edilemedi.' });
    }
});

app.get('/api/payments/cancel', (req, res) => {
    res.send('<h1>Ödeme İptal Edildi. ❌</h1>');
});

// Hava Durumu API'si (Open-Meteo entegrasyonu - API KEY Gerektirmez)
app.get('/api/weather', async (req, res) => {
  try {
    const { lat = 40.76, lon = 29.92 } = req.query; // Varsayılan: İzmit/Kocaeli
    const weatherUrl = `https://api.open-meteo.com/v1/forecast?latitude=${lat}&longitude=${lon}&current_weather=true`;
    
    const response = await fetch(weatherUrl);
    const data = await response.json();
    
    res.json(data);
  } catch (error) {
    console.error(error);
    res.status(500).json({ error: 'Hava durumu alınamadı.' });
  }
});

// Şehir Adından Koordinat Bulma (Geocoding Proxy)
app.get('/api/geocode', async (req, res) => {
  try {
    const { name } = req.query;
    if (!name) return res.status(400).json({ error: 'Şehir adı gerekli.' });
    
    const geoUrl = `https://geocoding-api.open-meteo.com/v1/search?name=${encodeURIComponent(name.toString())}&count=1&language=tr`;
    const response = await fetch(geoUrl);
    const data = await response.json();
    
    if (data.results && data.results.length > 0) {
      res.json(data.results[0]);
    } else {
      res.status(404).json({ error: 'Şehir bulunamadı.' });
    }
  } catch (error) {
    console.error(error);
    res.status(500).json({ error: 'Geocoding hatası.' });
  }
});

// Rastgele Sensör Verisi Simüle Et (Test İçin)
app.post('/api/simulate/:userId', async (req, res) => {
  try {
    const { userId } = req.params;
    
    // Önce kullanıcıya ait bir cihaz var mı bak, yoksa oluştur
    let device = await prisma.device.findFirst({ where: { userId } });
    if (!device) {
      device = await prisma.device.create({
        data: {
          name: 'Ana Kontrol Paneli',
          macAddress: `00:B0:D0:${Math.floor(Math.random()*100)}:${Math.floor(Math.random()*100)}`,
          type: 'RaspberryPi',
          userId
        }
      });
    }

    const newLog = await prisma.sensorLog.create({
      data: {
        temperature: parseFloat((Math.random() * (30 - 18) + 18).toFixed(1)),
        humidity: parseFloat((Math.random() * (60 - 30) + 30).toFixed(1)),
        isRaining: Math.random() > 0.8,
        gasDetected: Math.random() > 0.95,
        lightLevel: parseFloat((Math.random() * (600 - 150) + 150).toFixed(1)),
        deviceId: device.id
      }
    });

    res.json(newLog);
  } catch (error) {
    console.error(error);
    res.status(500).json({ error: 'Simülasyon başarısız.' });
  }
});

// Sensör Verilerini Kaydetme API'si (Cihazdan Gelen)
app.post('/api/sensors', async (req, res) => {
  try {
    const { temperature, humidity, isRaining, gasDetected, lightLevel, deviceId } = req.body;

    // Test aşamasında "DeviceId" gelmezse, veritabanında sahte bir Test Cihazı (Dummy Device) yaratıyoruz.
    let targetDeviceId = deviceId;
    if (!targetDeviceId) {
       let testDevice = await prisma.device.findFirst({ where: { name: 'Test Cihazı' } });
       if (!testDevice) {
           // Önce sahte bir kullanıcı (Eğer ilk test ise)
           let testUser = await prisma.user.findFirst();
           if (!testUser) {
               testUser = await prisma.user.create({
                   data: { fullName: "Test Kullanıcı", email: "test@akilliev.com", phoneNumber: "0000", passwordHash: "test" }
               });
           }
           // Sahte cihazı oluştur
           testDevice = await prisma.device.create({
               data: { name: 'Test Cihazı', type: 'Simulated', macAddress: '00:00:00:00:00:00', userId: testUser.id }
           });
       }
       targetDeviceId = testDevice.id;
     }

    // Sensör verisini PostgreSQL'e yazıyoruz
    const log = await prisma.sensorLog.create({
      data: {
        temperature,
        humidity: humidity || 0,
        isRaining,
        gasDetected,
        lightLevel: lightLevel || 0,
        deviceId: targetDeviceId
      }
    });

    console.log(`[+] Sensör Verisi Alındı -> Sıcaklık: ${temperature}°C, Yağmur: ${isRaining}, Gaz: ${gasDetected}, Işık: ${lightLevel || 0} Lux`);
    res.status(201).json({ message: 'Sensör verisi başarıyla PostgreSQL veritabanına kaydedildi.', data: log });
  } catch (error) {
    console.error('Sensör verisi kaydedilemedi:', error);
    res.status(500).json({ error: 'Veri kaydedilirken hata oluştu.' });
  }
});
// Paket Ayarlarını Getir
app.get('/api/settings', async (req, res) => {
  try {
    let settings = await prisma.subscriptionSettings.findUnique({
      where: { id: 'default' }
    });
    
    // Eğer tablo boşsa varsayılan ayarları oluştur
    if (!settings) {
      settings = await prisma.subscriptionSettings.create({
        data: {
          id: 'default',
          basicPlanModules: ['light', 'fan', 'heater'],
          premiumPlanModules: ['light', 'fan', 'heater', 'tent', 'gas', 'camera'],
          premiumPrice: 250
        }
      });
    }
    res.json(settings);
  } catch (error) {
    res.status(500).json({ error: 'Ayarlar getirilemedi.' });
  }
});

// Paket Ayarlarını Güncelle
app.put('/api/settings', async (req, res) => {
  try {
    const { basicPlanModules, premiumPlanModules, premiumPrice } = req.body;
    
    const settings = await prisma.subscriptionSettings.upsert({
      where: { id: 'default' },
      update: {
        basicPlanModules,
        premiumPlanModules,
        premiumPrice
      },
      create: {
        id: 'default',
        basicPlanModules: basicPlanModules || [],
        premiumPlanModules: premiumPlanModules || [],
        premiumPrice: premiumPrice || 250
      }
    });
    
    res.json(settings);
  } catch (error) {
    res.status(500).json({ error: 'Ayarlar güncellenemedi.' });
  }
});

// DASHBOARD İSTATİSTİKLERİ
app.get('/api/dashboard/stats', async (req, res) => {
  try {
    const activeCustomers = await prisma.user.count({ where: { isActive: true } });
    const premiumCustomers = await prisma.user.count({ where: { subscriptionType: 'Premium' } });
    const basicCustomers = await prisma.user.count({ where: { subscriptionType: 'Basic' } });
    
    const totalRevenue = await prisma.paymentRecord.aggregate({
      _sum: { amount: true },
      where: { status: 'Success' }
    });

    const latestActivities = await prisma.activityLog.findMany({
      orderBy: { createdAt: 'desc' },
      take: 10
    });

    const sensorCount = await prisma.device.count(); // Şimdilik cihaz sayısı sensör sayısı gibi

    res.json({
      metrics: {
        activeSensors: sensorCount,
        activeCustomers,
        premiumCustomers,
        basicCustomers,
        totalRevenue: totalRevenue._sum.amount || 0
      },
      latestActivities
    });
  } catch (error) {
    console.error(error);
    res.status(500).json({ error: 'İstatistikler alınamadı.' });
  }
});

// ==========================================
// 🎬 TAMPON BELLEKLİ GERÇEK ZAMANLI KAMERA BULUT KÖPRÜSÜ
// ==========================================
let latestCameraFrame: Buffer | null = null;
const streamClients = new Set<any>();

// Raspberry Pi bu endpoint'e anlık JPEG karelerini binary olarak POST eder
app.post('/api/camera/upload', express.raw({ type: 'image/jpeg', limit: '5mb' }), (req, res) => {
  latestCameraFrame = req.body as Buffer;
  
  // Bağlı olan tüm telefonlara bu yeni kareyi anında gönder (Gerçek zamanlı dağıtım)
  for (const client of streamClients) {
    try {
      client.write(`--frame\r\nContent-Type: image/jpeg\r\nContent-Length: ${latestCameraFrame.length}\r\n\r\n`);
      client.write(latestCameraFrame);
      client.write('\r\n');
    } catch (err) {
      streamClients.delete(client);
    }
  }
  
  res.sendStatus(200);
});

// Anlık JPEG karesini doğrudan servis eden uyumlu endpoint
app.get('/api/camera/frame', (req, res) => {
  if (latestCameraFrame) {
    res.setHeader('Content-Type', 'image/jpeg');
    res.setHeader('Cache-Control', 'no-cache, no-store, must-revalidate');
    res.send(latestCameraFrame);
  } else {
    res.sendStatus(404);
  }
});

// Mobil uygulamalar için gömülü, şık HTML oynatıcı sayfası (Aynı origin üzerinden, sıfır CORS/Mixed-Content hatası!)
app.get('/api/camera/view', (req, res) => {
  res.setHeader('Content-Type', 'text/html');
  res.send(`
    <!DOCTYPE html>
    <html>
    <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no">
        <title>Kamera Yayini</title>
        <style>
            html, body {
                margin: 0;
                padding: 0;
                background-color: #0F172A;
                display: flex;
                justify-content: center;
                align-items: center;
                height: 100%;
                width: 100%;
                overflow: hidden;
            }
            img {
                width: 100%;
                height: 100%;
                object-fit: contain;
                image-rendering: auto;
            }
        </style>
    </head>
    <body>
        <img id="feed" src="" alt="Canli Yayin" />
        <script>
            const img = document.getElementById('feed');
            function updateFrame() {
                // Her kareyi benzersiz zaman damgasıyla çekerek önbelleği devre dışı bırakırız
                img.src = '/api/camera/frame?t=' + Date.now();
            }
            img.onload = () => {
                // Önceki kare başarıyla yüklendiğinde yenisini hemen isteriz (Akıcı görüntü)
                setTimeout(updateFrame, 16); // ~60 FPS max
            };
            img.onerror = () => {
                // Hata durumunda yarım saniye bekleyip tekrar deneriz
                setTimeout(updateFrame, 500);
            };
            // Döngüyü başlat
            updateFrame();
        </script>
    </body>
    </html>
  `);
});

// Mobil uygulamalar bu adresten canlı yayını dünyanın her yerinden izler
app.get('/api/camera/stream', (req, res) => {
  res.setHeader('Age', 0);
  res.setHeader('Cache-Control', 'no-cache, private');
  res.setHeader('Pragma', 'no-cache');
  res.setHeader('Content-Type', 'multipart/x-mixed-replace; boundary=frame');
  
  // Eğer hafızada ilk kare varsa hemen gönder
  if (latestCameraFrame) {
    res.write(`--frame\r\nContent-Type: image/jpeg\r\nContent-Length: ${latestCameraFrame.length}\r\n\r\n`);
    res.write(latestCameraFrame);
    res.write('\r\n');
  }
  
  // İstemciyi dinleyiciler arasına ekle
  streamClients.add(res);
  
  // İstemci sayfadan çıkarsa listeden çıkar ve soketi temizle
  req.on('close', () => {
    streamClients.delete(res);
  });
});

app.listen(PORT, '0.0.0.0', () => {
  console.log(`Sunucu ${PORT} portunda başarıyla başlatıldı.`);
  console.log(`Tüm arayüzlerden (0.0.0.0) dinleniyor...`);
});
