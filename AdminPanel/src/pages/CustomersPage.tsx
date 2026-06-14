import { useState, useEffect } from 'react';
import {
  User,
  Mail,
  Calendar,
  Shield,
  Power,
  Lightbulb,
  ThermometerSun,
  Warehouse,
  Flame,
  Camera,
  Search,
  Lock,
  Unlock
} from 'lucide-react';
import './CustomersPage.css';

// Admin panel için müşteri tipi
interface Customer {
  id: string;
  name: string;
  email: string;
  plan: string;
  isActive: boolean;
  joinDate: string;
  avatar: string;
  daysSinceLastPayment: number;
  lockedModules: string[];
}

const DEVICE_ACTIONS = [
  { id: 'light', name: 'Aydınlatma Kontrolü', icon: <Lightbulb size={20} /> },
  { id: 'fan', name: 'Havalandırma (Fan)', icon: <Power size={20} /> },
  { id: 'heater', name: 'Isıtıcı Sistemi', icon: <ThermometerSun size={20} /> },
  { id: 'tent', name: 'Tente', icon: <Warehouse size={20} /> },
  { id: 'gas', name: 'Gaz Sensörü', icon: <Flame size={20} /> },
  { id: 'camera', name: 'Güvenlik Kamerası', icon: <Camera size={20} /> },
];

const CustomersPage = ({ initialSelectedId }: { initialSelectedId?: string | null }) => {
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [search, setSearch] = useState("");
  const [selectedCustomerId, setSelectedCustomerId] = useState<string | null>(initialSelectedId || null);
  const [payments, setPayments] = useState<any[]>([]);

  // Veritabanından gelecek paket ayarları
  const [basicPlanModules, setBasicPlanModules] = useState<string[]>(['light', 'fan', 'heater']);
  const [premiumPlanModules, setPremiumPlanModules] = useState<string[]>(['light', 'fan', 'heater', 'tent', 'gas', 'camera']);

  // Focus customer when initialSelectedId changes
  useEffect(() => {
    if (initialSelectedId) {
      setSelectedCustomerId(initialSelectedId);
    }
  }, [initialSelectedId]);

  // Prisma veritabanından müşterileri çekiyoruz
  useEffect(() => {
    fetch('http://141.98.48.101:3000/api/users')
      .then(res => res.json())
      .then(data => {
        const formattedUsers = data.map((u: any, index: number) => ({
          id: u.id,
          name: u.fullName,
          email: u.email,
          plan: u.subscriptionType || 'Free',
          isActive: u.isActive,
          joinDate: new Date(u.createdDate).toLocaleDateString('tr-TR'),
          avatar: `https://i.pravatar.cc/150?img=${(index % 50) + 1}`,
          daysSinceLastPayment: u.daysSinceLastPayment || 0,
          lockedModules: u.lockedModules || []
        }));
        setCustomers(formattedUsers);
        if (formattedUsers.length > 0 && !initialSelectedId) {
          setSelectedCustomerId(formattedUsers[0].id);
        }
      })
      .catch(err => console.error("Kullanıcılar getirilemedi", err));

    // Paket ayarlarını çekiyoruz
    fetch('http://141.98.48.101:3000/api/settings')
      .then(res => res.json())
      .then(data => {
        if (data) {
          setBasicPlanModules(data.basicPlanModules || []);
          setPremiumPlanModules(data.premiumPlanModules || []);
        }
      })
      .catch(err => console.error("Ayarlar getirilemedi", err));
  }, [initialSelectedId]);

  // Müşteri değiştikçe ödeme geçmişini çek
  useEffect(() => {
    if (!selectedCustomerId) return;
    fetch(`http://141.98.48.101:3000/api/users/${selectedCustomerId}/payments`)
      .then(res => res.json())
      .then(data => setPayments(data))
      .catch(err => console.error("Ödeme geçmişi alınamadı:", err));
  }, [selectedCustomerId]);

  const selectedCustomer = customers.find(c => c.id === selectedCustomerId);
  const isPremium = selectedCustomer?.plan === 'Premium';

  const filteredCustomers = customers.filter(c =>
    c.name.toLowerCase().includes(search.toLowerCase()) ||
    c.email.toLowerCase().includes(search.toLowerCase())
  );

  const toggleAccountStatus = async () => {
    if (!selectedCustomer) return;

    const newStatus = !selectedCustomer.isActive;

    try {
      const res = await fetch(`http://141.98.48.101:3000/api/users/${selectedCustomer.id}/status`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ isActive: newStatus })
      });

      if (res.ok) {
        const updatedUser = await res.json();
        setCustomers(customers.map(c =>
          c.id === selectedCustomer.id ? {
            ...c,
            isActive: updatedUser.isActive,
            plan: updatedUser.subscriptionType // Backend'den gelen yeni plan (Basic'e düşmüş olabilir)
          } : c
        ));
      }
    } catch (err) {
      console.error("Hesap durumu güncellenemedi:", err);
    }
  };

  const handlePlanChange = async (newPlan: string) => {
    if (!selectedCustomer) return;

    try {
      const res = await fetch(`http://141.98.48.101:3000/api/users/${selectedCustomer.id}/plan`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ subscriptionType: newPlan })
      });

      if (res.ok) {
        const updatedUser = await res.json();
        setCustomers(customers.map(c =>
          c.id === selectedCustomer.id ? {
            ...c,
            plan: updatedUser.subscriptionType
          } : c
        ));
      }
    } catch (err) {
      console.error("Paket güncellenemedi:", err);
    }
  };

  const handleToggleDeviceLock = async (deviceId: string) => {
    if (!selectedCustomer) return;

    const currentLocked = selectedCustomer.lockedModules || [];
    let newLocked;
    if (currentLocked.includes(deviceId)) {
      newLocked = currentLocked.filter(id => id !== deviceId);
    } else {
      newLocked = [...currentLocked, deviceId];
    }

    try {
      const res = await fetch(`http://141.98.48.101:3000/api/users/${selectedCustomer.id}/locked-modules`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ lockedModules: newLocked })
      });

      if (res.ok) {
        const updatedUser = await res.json();
        setCustomers(customers.map(c =>
          c.id === selectedCustomer.id ? {
            ...c,
            lockedModules: updatedUser.lockedModules
          } : c
        ));
      }
    } catch (err) {
      console.error("Cihaz kilitleme durumu güncellenemedi:", err);
    }
  };

  const handleUpdatePaymentDays = async (days: number) => {
    if (!selectedCustomer) return;

    try {
      const res = await fetch(`http://141.98.48.101:3000/api/users/${selectedCustomer.id}/payment-days`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ daysSinceLastPayment: days })
      });

      if (res.ok) {
        const updatedUser = await res.json();
        setCustomers(customers.map(c =>
          c.id === selectedCustomer.id ? {
            ...c,
            daysSinceLastPayment: updatedUser.daysSinceLastPayment
          } : c
        ));
      }
    } catch (err) {
      console.error("Ödeme süresi güncellenemedi:", err);
    }
  };

  return (
    <div className="customers-page">
      {/* Sol Taraf: Müşteri Listesi */}
      <div className="customers-sidebar glass-panel">
        <div className="sidebar-header">
          <h2>Müşteriler</h2>
          <div className="search-box">
            <Search size={16} />
            <input
              type="text"
              placeholder="İsim veya e-posta ara..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
        </div>

        <div className="customers-list">
          {filteredCustomers.map(customer => (
            <div
              key={customer.id}
              className={`customer-list-item ${selectedCustomerId === customer.id ? 'active' : ''}`}
              onClick={() => setSelectedCustomerId(customer.id)}
            >
              <img src={customer.avatar} alt={customer.name} className="list-avatar" />
              <div className="list-info">
                <h4>{customer.name}</h4>
                <span>{customer.plan} Paket</span>
              </div>
              {!customer.isActive && <Lock size={14} className="lock-icon" />}
            </div>
          ))}
        </div>
      </div>

      {/* Sağ Taraf: Müşteri Profili Detayları */}
      <div className="customer-profile-area">
        {selectedCustomer ? (
          <>
            {/* Profil Üst Kartı */}
            <div className="profile-header-card glass-panel">
              <div className="profile-main-info">
                <img src={selectedCustomer.avatar} alt={selectedCustomer.name} className="profile-avatar" />
                <div className="profile-details">
                  <div className="name-row">
                    <h2>{selectedCustomer.name}</h2>
                    <span className={`plan-badge ${selectedCustomer.plan.toLowerCase()}`}>
                      {selectedCustomer.plan}
                    </span>
                    {!selectedCustomer.isActive && (
                      <span className="status-badge suspended">Askıya Alındı</span>
                    )}
                  </div>
                  <div className="meta-info">
                    <span className="meta-item"><Mail size={14} /> {selectedCustomer.email}</span>
                    <span className="meta-item"><Calendar size={14} /> Katılım: {selectedCustomer.joinDate}</span>
                  </div>
                  <div className="meta-info" style={{ marginTop: '8px' }}>
                    <span className="meta-item">
                      Ödemeden Geçen Süre: <strong>{selectedCustomer.daysSinceLastPayment} gün</strong>
                      {selectedCustomer.daysSinceLastPayment > 30 && (
                        <span className="warning-badge">Ödeme Gecikti!</span>
                      )}
                    </span>
                  </div>
                  {/* Sunum Simülasyonu Kontrolü */}
                  <div className="payment-days-test">
                    <span className="days-label">Süre Değiştir (Sunum Testi):</span>
                    <input
                      type="number"
                      min="0"
                      value={selectedCustomer.daysSinceLastPayment}
                      onChange={(e) => handleUpdatePaymentDays(parseInt(e.target.value) || 0)}
                      className="days-input"
                    />
                    <span>gün</span>
                  </div>
                </div>
              </div>

              {/* Hızlı Aksiyonlar */}
              <div className="profile-actions">
                <div className="plan-selector">
                  <Shield size={16} className="selector-icon" />
                  <select
                    value={selectedCustomer.plan}
                    onChange={(e) => handlePlanChange(e.target.value)}
                    className="plan-dropdown"
                  >
                    <option value="Basic">Basic Paket</option>
                    <option value="Premium">Premium Paket</option>
                  </select>
                </div>

                <button
                  className={`status-toggle-btn ${selectedCustomer.isActive ? 'btn-danger' : 'btn-success'}`}
                  onClick={toggleAccountStatus}
                >
                  {selectedCustomer.isActive ? (
                    <><Lock size={16} /> Hesabı Kilitle</>
                  ) : (
                    <><Unlock size={16} /> Kilidi Aç</>
                  )}
                </button>
              </div>
            </div>

            {/* İzin Verilen Aksiyonlar / Cihazlar */}
            <div className="actions-card glass-panel">
              <div className="actions-header">
                <h3>Kullanılabilir Cihaz Aksiyonları</h3>
                <p>
                  Müşterinin mevcut ({selectedCustomer.plan}) paketi dahilinde kullanabileceği modüller aşağıdadır.
                  Kırmızı kilit simgesi olan cihazlar yönetici tarafından kilitlenmiştir.
                </p>
              </div>

              <div className="devices-grid">
                {DEVICE_ACTIONS.map(device => {
                  const isSystemDisabled = !selectedCustomer.isActive;

                  // Kilitleme mantığını dinamik paket ayarlarına göre yapıyoruz
                  const isLockedByPlan = isPremium
                    ? !premiumPlanModules.includes(device.id)
                    : !basicPlanModules.includes(device.id);

                  const isManuallyLocked = selectedCustomer.lockedModules?.includes(device.id) || false;
                  const isLocked = isLockedByPlan || isManuallyLocked;
                  const lockReason = isLockedByPlan 
                    ? (isPremium ? 'Pakete Dahil Değil' : 'Premium Gerektirir') 
                    : 'Yönetici Tarafından Kilitli';

                  return (
                    <div
                      key={device.id}
                      className={`device-action-card ${isLocked ? 'locked' : ''} ${isSystemDisabled ? 'system-disabled' : ''}`}
                    >
                      <div className="device-icon-wrapper">
                        {device.icon}
                      </div>
                      <div className="device-info">
                        <h4>{device.name}</h4>
                        {isSystemDisabled ? (
                          <span className="device-status error">Sistem Kapalı</span>
                        ) : isLocked ? (
                          <span className="device-status warning"><Shield size={12} /> {lockReason}</span>
                        ) : (
                          <span className="device-status success"><Power size={12} /> Aktif</span>
                        )}
                      </div>
                      {/* Aktif Kilitleme Butonu */}
                      <button
                        className={`device-toggle-btn ${isManuallyLocked ? 'locked' : 'unlocked'}`}
                        onClick={() => handleToggleDeviceLock(device.id)}
                        disabled={isSystemDisabled}
                        title={isManuallyLocked ? 'Cihaz Kilidini Aç' : 'Cihazı Kilitle'}
                      >
                        {isManuallyLocked ? <Lock size={16} /> : <Unlock size={16} />}
                      </button>
                    </div>
                  );
                })}
              </div>
            </div>

            {/* Ödeme Geçmişi */}
            <div className="payments-card glass-panel">
              <div className="payments-header">
                <h3>Ödeme Geçmişi</h3>
                <p>Müşterinin geçmişte gerçekleştirdiği tüm ödeme işlemlerinin dökümü (Tarih ve Saat detayıyla).</p>
              </div>

              <div className="payments-table-wrapper">
                {payments && payments.length > 0 ? (
                  <table className="payments-table">
                    <thead>
                      <tr>
                        <th>Tarih & Saat</th>
                        <th>Tutar</th>
                        <th>Para Birimi</th>
                        <th>Durum</th>
                      </tr>
                    </thead>
                    <tbody>
                      {payments.map((payment: any) => (
                        <tr key={payment.id}>
                          <td>{new Date(payment.createdAt).toLocaleString('tr-TR')}</td>
                          <td>₺{payment.amount}</td>
                          <td>{payment.currency}</td>
                          <td>
                            <span className={`payment-status ${payment.status.toLowerCase()}`}>
                              {payment.status === 'Success' ? 'Başarılı' : 'Başarısız'}
                            </span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                ) : (
                  <div className="no-payments">Kayıtlı ödeme işlemi bulunmuyor.</div>
                )}
              </div>
            </div>
          </>
        ) : (
          <div className="no-selection glass-panel">
            <User size={48} />
            <h3>Müşteri Seçilmedi</h3>
            <p>Detayları görmek için sol taraftan bir müşteri seçin.</p>
          </div>
        )}
      </div>
    </div>
  );
};

export default CustomersPage;
