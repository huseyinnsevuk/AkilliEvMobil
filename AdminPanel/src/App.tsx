import { useState, useEffect } from 'react';
import {
  Search,
  Bell,
  Globe,
  Activity,
  Users,
  UserMinus,
  DollarSign,
  ArrowUpRight,
  Home,
  FileText,
  Settings,
  LogOut
} from 'lucide-react';
import './App.css';
import CustomersPage from './pages/CustomersPage';
import SubscriptionsPage from './pages/SubscriptionsPage';

// Yüklenen Yeni Tasarım Varlıkları
import sensorImg from './assets/sensor.png';
import aktifImg from './assets/aktif.png';
import pasifImg from './assets/pasif.png';
import incomeImg from './assets/income.png';

interface DashboardStats {
  metrics: {
    activeSensors: number;
    activeCustomers: number;
    premiumCustomers: number;
    basicCustomers: number;
    totalRevenue: number;
  };
  latestActivities: any[];
}

function App() {
  const [currentPage, setCurrentPage] = useState('dashboard');
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [, setLoading] = useState(true);

  useEffect(() => {
    const fetchStats = async () => {
      try {
        const res = await fetch('http://nart3d.com:3000/api/dashboard/stats');
        const data = await res.json();
        setStats(data);
      } catch (err) {
        console.error('Stats fetch error:', err);
      } finally {
        setLoading(false);
      }
    };

    if (currentPage === 'dashboard') {
      fetchStats();
      const interval = setInterval(fetchStats, 10000); // 10 saniyede bir güncelle
      return () => clearInterval(interval);
    }
  }, [currentPage]);

  const formatCurrency = (val: number) => {
    return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' }).format(val);
  };

  return (
    <div className="layout-wrapper">

      {/* SOL MENÜ (SIDEBAR) */}
      <aside className="sidebar">
        <div className="sidebar-logo">
          <h1 className="logo-text">Akıllı<span>Ev</span></h1>
        </div>

        <nav className="sidebar-nav">
          <a
            href="#"
            className={`nav-item ${currentPage === 'dashboard' ? 'active' : ''}`}
            onClick={(e) => { e.preventDefault(); setCurrentPage('dashboard'); }}
          >
            <Home size={22} /> Ana Panel
          </a>
          <a
            href="#"
            className={`nav-item ${currentPage === 'customers' ? 'active' : ''}`}
            onClick={(e) => { e.preventDefault(); setCurrentPage('customers'); }}
          >
            <Users size={22} /> Müşteriler
          </a>
          <a href="#" className="nav-item"><Activity size={22} /> Cihaz & Sensör</a>
          <a
            href="#"
            className={`nav-item ${currentPage === 'subscriptions' ? 'active' : ''}`}
            onClick={(e) => { e.preventDefault(); setCurrentPage('subscriptions'); }}
          >
            <FileText size={22} /> Abonelik & Fatura
          </a>
          <a href="#" className="nav-item"><Settings size={22} /> Ayarlar</a>
        </nav>

        <div className="sidebar-bottom">
          <a href="#" className="nav-item logout-item"><LogOut size={22} /> Çıkış Yap</a>
        </div>
      </aside>

      {/* SAĞ TARAFTAKİ ANA ALAN */}
      <div className="dashboard-container">

        {/* ÜST BAR (TOPBAR) */}
        <header className="topbar">
          <div className="topbar-left">
            <h2 className="topbar-title">
              {currentPage === 'dashboard' ? 'Dashboard Overview' :
                currentPage === 'customers' ? 'Müşteri Yönetimi' :
                  'Abonelik Yönetimi'}
            </h2>
          </div>

          <div className="search-container">
            <Search size={18} className="search-icon" />
            <input type="text" placeholder="Genel arama..." className="search-input" />
          </div>

          <div className="topbar-right">
            <button className="icon-btn">
              <Bell size={20} />
              <span className="notification-dot"></span>
            </button>
            <button className="icon-btn">
              <Globe size={20} />
            </button>
            <div className="user-profile">
              <img src="https://i.pravatar.cc/100?img=33" alt="Admin" className="avatar" />
              <div className="user-info">
                <span className="user-name">Hüseyin Sevük</span>
                <span className="user-role">Sistem Yöneticisi</span>
              </div>
            </div>
          </div>
        </header>

        {/* ANA İÇERİK (MAIN CONTENT) */}
        <main className="main-content">
          {currentPage === 'dashboard' ? (
            <>
              {/* İSTATİSTİK KARTLARI (METRIC CARDS) */}
              <section className="metrics-grid">
                {/* Kart 1 */}
                <div className="metric-card glass-panel card-green">
                  <div className="card-header">
                    <h3>Aktif Sensör Sayısı</h3>
                    <div className="icon-wrapper"><Activity size={20} /></div>
                  </div>
                  <div className="card-body">
                    <h2>{stats?.metrics.activeSensors || 0}</h2>
                    <div className="trend positive">
                      <ArrowUpRight size={16} />
                      <span>Sistem Aktif</span>
                    </div>
                  </div>
                  <img src={sensorImg} alt="Sensor Grafiği" className="card-custom-img" />
                  <div className="bg-shape"></div>
                </div>

                {/* Kart 2 */}
                <div className="metric-card glass-panel card-blue">
                  <div className="card-header">
                    <h3>Aktif Müşteri</h3>
                    <div className="icon-wrapper"><Users size={20} /></div>
                  </div>
                  <div className="card-body">
                    <h2>{stats?.metrics.activeCustomers || 0}</h2>
                    <div className="trend positive">
                      <ArrowUpRight size={16} />
                      <span>{stats?.metrics.premiumCustomers || 0} Premium</span>
                    </div>
                  </div>
                  <img src={aktifImg} alt="Aktif Kullanıcı Grafiği" className="card-custom-img" />
                  <div className="bg-shape"></div>
                </div>

                {/* Kart 3 */}
                <div className="metric-card glass-panel card-orange">
                  <div className="card-header">
                    <h3>Basic Üyelik</h3>
                    <div className="icon-wrapper"><UserMinus size={20} /></div>
                  </div>
                  <div className="card-body">
                    <h2>{stats?.metrics.basicCustomers || 0}</h2>
                    <div className="trend">
                      <span>Standart Paket</span>
                    </div>
                  </div>
                  <img src={pasifImg} alt="Pasif Kullanıcı Grafiği" className="card-custom-img" />
                  <div className="bg-shape"></div>
                </div>

                {/* Kart 4 */}
                <div className="metric-card glass-panel card-purple">
                  <div className="card-header">
                    <h3>Toplam Ciro</h3>
                    <div className="icon-wrapper"><DollarSign size={20} /></div>
                  </div>
                  <div className="card-body">
                    <h2>{formatCurrency(stats?.metrics.totalRevenue || 0)}</h2>
                    <div className="trend positive">
                      <ArrowUpRight size={16} />
                      <span>Toplam Kazanç</span>
                    </div>
                  </div>
                  <img src={incomeImg} alt="Ciro Grafiği" className="card-custom-img" />
                  <div className="bg-shape"></div>
                </div>
              </section>

              {/* ALT ALAN: GRAFİK VE AKTİVİTELER */}
              <section className="dashboard-bottom-grid">

                {/* GELİR GRAFİĞİ (BAR CHART) - Şimdilik statik ama başlığı gerçek ciroya bağladık */}
                <div className="chart-section glass-panel">
                  <div className="section-header">
                    <h3>Aylık Ciro Grafiği ({formatCurrency(stats?.metrics.totalRevenue || 0)})</h3>
                  </div>
                  <div className="bar-chart-container">
                    <div className="chart-y-axis">
                      <span>50k</span>
                      <span>25k</span>
                      <span>0</span>
                    </div>
                    <div className="bars-wrapper">
                      <div className="bar-group"><div className="bar" style={{ height: '40%' }}></div><span className="bar-label">Oca</span></div>
                      <div className="bar-group"><div className="bar" style={{ height: '55%' }}></div><span className="bar-label">Şub</span></div>
                      <div className="bar-group"><div className="bar" style={{ height: '35%' }}></div><span className="bar-label">Mar</span></div>
                      <div className="bar-group"><div className="bar" style={{ height: '70%' }}></div><span className="bar-label">Nis</span></div>
                      <div className="bar-group"><div className="bar" style={{ height: '85%' }}></div><span className="bar-label">May</span></div>
                      <div className="bar-group"><div className="bar active-bar" style={{ height: '95%' }}></div><span className="bar-label">Haz</span></div>
                    </div>
                  </div>
                </div>

                {/* SON AKTİVİTELER */}
                <div className="activities-section glass-panel">
                  <div className="section-header">
                    <h3>Son Aktiviteler</h3>
                  </div>
                  <div className="activities-list">
                    {stats?.latestActivities.map((activity: any) => (
                      <div className="activity-item" key={activity.id}>
                        <div className={`activity-icon ${activity.type === 'USER_REGISTER' ? 'bg-blue' :
                            activity.type === 'PAYMENT_SUCCESS' ? 'bg-green' :
                              activity.type === 'SENSOR_ALERT' ? 'bg-red' : 'bg-orange'
                          }`}><Activity size={16} /></div>
                        <div className="activity-details">
                          <h4>{activity.title}</h4>
                          <p>{activity.description}</p>
                        </div>
                        <span className="activity-time">
                          {new Date(activity.createdAt).toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })}
                        </span>
                      </div>
                    ))}
                    {stats?.latestActivities.length === 0 && (
                      <p style={{ padding: '20px', textAlign: 'center', color: '#888' }}>Henüz aktivite yok.</p>
                    )}
                  </div>
                </div>

              </section>
            </>
          ) : currentPage === 'customers' ? (
            <CustomersPage />
          ) : (
            <SubscriptionsPage />
          )}
        </main>
      </div>
    </div>
  );
}

export default App;
