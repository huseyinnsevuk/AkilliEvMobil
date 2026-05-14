import React, { useState, useEffect } from 'react';
import {
  User,
  Mail,
  Calendar,
  Shield,
  Power,
  Lightbulb,
  Fan,
  ThermometerSun,
  Warehouse,
  Flame,
  Droplets,
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
}

const DEVICE_ACTIONS = [
  { id: 'light', name: 'Aydınlatma Kontrolü', icon: <Lightbulb size={20} /> },
  { id: 'fan', name: 'Havalandırma (Fan)', icon: <Fan size={20} /> },
  { id: 'heater', name: 'Isıtıcı Sistemi', icon: <ThermometerSun size={20} /> },
  { id: 'tent', name: 'Tente', icon: <Warehouse size={20} /> },
  { id: 'gas', name: 'Gaz Sensörü', icon: <Flame size={20} /> },
  { id: 'camera', name: 'Güvenlik Kamerası', icon: <Camera size={20} /> },
];

const CustomersPage = () => {
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [search, setSearch] = useState("");
  const [selectedCustomerId, setSelectedCustomerId] = useState<string | null>(null);

  // Veritabanından gelecek paket ayarları
  const [basicPlanModules, setBasicPlanModules] = useState<string[]>(['light', 'fan', 'heater']);
  const [premiumPlanModules, setPremiumPlanModules] = useState<string[]>(['light', 'fan', 'heater', 'tent', 'gas', 'camera']);

  // Prisma veritabanından müşterileri çekiyoruz
  useEffect(() => {
    fetch('http://nart3d.com:3000/api/users')
      .then(res => res.json())
      .then(data => {
        const formattedUsers = data.map((u: any, index: number) => ({
          id: u.id,
          name: u.fullName,
          email: u.email,
          plan: u.subscriptionType || 'Free',
          isActive: u.isActive,
          joinDate: new Date(u.createdDate).toLocaleDateString('tr-TR'),
          avatar: `https://i.pravatar.cc/150?img=${(index % 50) + 1}`
        }));
        setCustomers(formattedUsers);
        if (formattedUsers.length > 0) {
          setSelectedCustomerId(formattedUsers[0].id);
        }
      })
      .catch(err => console.error("Kullanıcılar getirilemedi", err));

    // Paket ayarlarını çekiyoruz
    fetch('http://nart3d.com:3000/api/settings')
      .then(res => res.json())
      .then(data => {
        if (data) {
          setBasicPlanModules(data.basicPlanModules || []);
          setPremiumPlanModules(data.premiumPlanModules || []);
        }
      })
      .catch(err => console.error("Ayarlar getirilemedi", err));
  }, []);

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
      const res = await fetch(`http://nart3d.com:3000/api/users/${selectedCustomer.id}/status`, {
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
      const res = await fetch(`http://nart3d.com:3000/api/users/${selectedCustomer.id}/plan`, {
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
                  {selectedCustomer.plan === 'Free' && " Ücretsiz pakette sadece temel özellikler açıktır."}
                </p>
              </div>

              <div className="devices-grid">
                {DEVICE_ACTIONS.map(device => {
                  const isSystemDisabled = !selectedCustomer.isActive;

                  // Kilitleme mantığını dinamik paket ayarlarına göre yapıyoruz
                  const isLocked = isPremium
                    ? !premiumPlanModules.includes(device.id)
                    : !basicPlanModules.includes(device.id);

                  const lockReason = isPremium ? 'Pakete Dahil Değil' : 'Premium Gerektirir';

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
                      {/* Temsili Switch */}
                      <div className={`mock-switch ${(!isLocked && !isSystemDisabled) ? 'switch-on' : 'switch-off'}`}>
                        <div className="switch-knob"></div>
                      </div>
                    </div>
                  );
                })}
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
