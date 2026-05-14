import { useState, useEffect } from 'react';
import {
  Check,
  X,
  Save,
  Shield,
  Zap,
  Lightbulb,
  Fan,
  ThermometerSun,
  Warehouse,
  Flame,
  Camera,
  Edit3
} from 'lucide-react';
import './SubscriptionsPage.css';

// Sistemdeki Tüm Modüller (Sabit Liste)
const SYSTEM_MODULES = [
  { id: 'light', name: 'Aydınlatma Kontrolü', icon: <Lightbulb size={18} /> },
  { id: 'fan', name: 'Havalandırma (Fan)', icon: <Fan size={18} /> },
  { id: 'heater', name: 'Isıtıcı Sistemi', icon: <ThermometerSun size={18} /> },
  { id: 'tent', name: 'Tente Kontrolü', icon: <Warehouse size={18} /> },
  { id: 'gas', name: 'Gaz Sensörü Bildirimleri', icon: <Flame size={18} /> },
  { id: 'camera', name: 'Güvenlik Kamerası Erişimi', icon: <Camera size={18} /> },
];

const SubscriptionsPage = () => {
  const [basicPlanModules, setBasicPlanModules] = useState<string[]>(['light', 'fan', 'heater']);
  const [premiumPlanModules, setPremiumPlanModules] = useState<string[]>(['light', 'fan', 'heater', 'tent', 'gas', 'camera']);
  const [premiumPrice, setPremiumPrice] = useState<number>(250);

  const [isEditingPrice, setIsEditingPrice] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [isLoading, setIsLoading] = useState(true);

  // Sayfa yüklendiğinde ayarları API'den getir
  useEffect(() => {
    fetch('http://nart3d.com:3000/api/settings')
      .then(res => res.json())
      .then(data => {
        if (data) {
          setBasicPlanModules(data.basicPlanModules || []);
          setPremiumPlanModules(data.premiumPlanModules || []);
          setPremiumPrice(data.premiumPrice || 250);
        }
        setIsLoading(false);
      })
      .catch(err => {
        console.error("Ayarlar getirilemedi", err);
        setIsLoading(false);
      });
  }, []);

  const toggleModule = (plan: 'basic' | 'premium', moduleId: string) => {
    if (plan === 'basic') {
      if (basicPlanModules.includes(moduleId)) {
        setBasicPlanModules(basicPlanModules.filter(id => id !== moduleId));
      } else {
        setBasicPlanModules([...basicPlanModules, moduleId]);
      }
    } else {
      if (premiumPlanModules.includes(moduleId)) {
        setPremiumPlanModules(premiumPlanModules.filter(id => id !== moduleId));
      } else {
        setPremiumPlanModules([...premiumPlanModules, moduleId]);
      }
    }
  };

  const handleSave = async () => {
    setIsSaving(true);

    try {
      const response = await fetch('http://nart3d.com:3000/api/settings', {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          basicPlanModules,
          premiumPlanModules,
          premiumPrice
        })
      });

      if (response.ok) {
        alert('Paket içerikleri ve fiyat başarıyla veritabanına kaydedildi!');
      } else {
        alert('Kayıt sırasında bir hata oluştu.');
      }
    } catch (err) {
      console.error(err);
      alert('Sunucu ile bağlantı kurulamadı.');
    } finally {
      setIsSaving(false);
    }
  };

  if (isLoading) {
    return <div style={{ padding: '40px', textAlign: 'center' }}>Ayarlar Yükleniyor...</div>;
  }

  return (
    <div className="subscriptions-page">
      <div className="page-header">
        <div>
          <h2>Abonelik Paketleri Yönetimi</h2>
          <p>Müşterilerin hangi pakette hangi modüllere erişebileceğini buradan belirleyin.</p>
        </div>
        <button className={`btn-save ${isSaving ? 'saving' : ''}`} onClick={handleSave}>
          <Save size={18} /> {isSaving ? 'Kaydediliyor...' : 'Değişiklikleri Kaydet'}
        </button>
      </div>

      <div className="plans-container">

        {/* BASIC (FREE) PAKET KARTI */}
        <div className="plan-card glass-panel">
          <div className="plan-header basic-header">
            <div className="plan-title">
              <div className="icon-box"><Zap size={24} /></div>
              <div>
                <h3>Basic (Ücretsiz) Paket</h3>
                <span className="plan-price">₺0 / Ay</span>
              </div>
            </div>
          </div>

          <div className="plan-body">
            <p className="plan-desc">Sisteme yeni kayıt olan müşterilerin standart olarak sahip olduğu temel yetkiler.</p>

            <div className="module-list">
              {SYSTEM_MODULES.map(mod => {
                const isActive = basicPlanModules.includes(mod.id);
                return (
                  <div
                    key={`basic-${mod.id}`}
                    className={`module-item ${isActive ? 'active' : 'inactive'}`}
                    onClick={() => toggleModule('basic', mod.id)}
                  >
                    <div className="mod-icon">{mod.icon}</div>
                    <span className="mod-name">{mod.name}</span>
                    <div className={`mod-toggle ${isActive ? 'on' : 'off'}`}>
                      {isActive ? <Check size={14} /> : <X size={14} />}
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        </div>

        {/* PREMIUM PAKET KARTI */}
        <div className="plan-card glass-panel border-premium">
          <div className="plan-header premium-header">
            <div className="plan-title">
              <div className="icon-box"><Shield size={24} /></div>
              <div>
                <h3>Premium Paket</h3>
                <div className="plan-price-container">
                  {isEditingPrice ? (
                    <div className="price-edit-mode">
                      <span className="currency-symbol">₺</span>
                      <input
                        type="number"
                        value={premiumPrice}
                        onChange={(e) => setPremiumPrice(Number(e.target.value))}
                        autoFocus
                        onBlur={() => setIsEditingPrice(false)}
                        onKeyDown={(e) => { if (e.key === 'Enter') setIsEditingPrice(false); }}
                      />
                      <span>/ Ay</span>
                    </div>
                  ) : (
                    <span className="plan-price editable" onClick={() => setIsEditingPrice(true)}>
                      ₺{premiumPrice} / Ay <Edit3 size={14} className="edit-icon" />
                    </span>
                  )}
                </div>
              </div>
            </div>
          </div>

          <div className="plan-body">
            <p className="plan-desc">Aylık ücret ödeyen müşterilerin erişebileceği tüm gelişmiş özellikler ve modüller.</p>

            <div className="module-list">
              {SYSTEM_MODULES.map(mod => {
                const isActive = premiumPlanModules.includes(mod.id);
                return (
                  <div
                    key={`premium-${mod.id}`}
                    className={`module-item ${isActive ? 'active' : 'inactive'}`}
                    onClick={() => toggleModule('premium', mod.id)}
                  >
                    <div className="mod-icon">{mod.icon}</div>
                    <span className="mod-name">{mod.name}</span>
                    <div className={`mod-toggle ${isActive ? 'on' : 'off'}`}>
                      {isActive ? <Check size={14} /> : <X size={14} />}
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        </div>

      </div>
    </div>
  );
};

export default SubscriptionsPage;
