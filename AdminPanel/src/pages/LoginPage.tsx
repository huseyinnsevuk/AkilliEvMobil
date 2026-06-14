import React, { useState } from 'react';
import { Home, Eye, EyeOff, Lock, User } from 'lucide-react';
import './LoginPage.css';

interface LoginPageProps {
  onLogin: (username: string) => void;
}

const LoginPage: React.FC<LoginPageProps> = ({ onLogin }) => {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    
    if (!username.trim() || !password.trim()) {
      setError('Lütfen tüm alanları doldurunuz.');
      return;
    }

    setIsLoading(true);

    // Profesyonel görünüm için hafif bir yapay yükleme beklemesi ekleyelim
    setTimeout(() => {
      // Admin Giriş Bilgileri Kontrolü
      if (
        (username.toLowerCase() === 'admin' || username === 'admin@nest.com') &&
        (password === 'admin' || password === 'nest123')
      ) {
        onLogin(username);
      } else {
        setError('Geçersiz kullanıcı adı veya şifre.');
        setIsLoading(false);
      }
    }, 800);
  };

  return (
    <div className="login-wrapper">
      {/* Arka Plan Işık Küreleri (Glow Orbs) */}
      <div className="glow-orb glow-orb-1"></div>
      <div className="glow-orb glow-orb-2"></div>
      
      <div className="login-card glass-panel-login">
        <div className="login-header">
          <div className="login-logo-container">
            <Home className="login-home-icon" size={32} />
          </div>
          <h1 className="login-title">NEST</h1>
          <p className="login-subtitle">Akıllı Ev Sistem Yöneticisi Girişi</p>
        </div>

        <form onSubmit={handleSubmit} className="login-form">
          {error && <div className="login-error-message">{error}</div>}

          <div className="form-group">
            <label htmlFor="username">Kullanıcı Adı</label>
            <div className="input-wrapper">
              <User className="input-icon" size={20} />
              <input
                id="username"
                type="text"
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                placeholder="Örn: admin"
                disabled={isLoading}
                autoFocus
              />
            </div>
          </div>

          <div className="form-group">
            <label htmlFor="password">Şifre</label>
            <div className="input-wrapper">
              <Lock className="input-icon" size={20} />
              <input
                id="password"
                type={showPassword ? 'text' : 'password'}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="••••••••"
                disabled={isLoading}
              />
              <button
                type="button"
                className="show-password-btn"
                onClick={() => setShowPassword(!showPassword)}
                disabled={isLoading}
              >
                {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
              </button>
            </div>
          </div>

          <div className="form-actions">
            <div className="remember-me">
              <input type="checkbox" id="remember" disabled={isLoading} />
              <label htmlFor="remember">Beni hatırla</label>
            </div>
            <a href="#" className="forgot-password" onClick={(e) => { e.preventDefault(); alert("Lütfen sistem yöneticisi ile iletişime geçiniz."); }}>
              Şifremi unuttum
            </a>
          </div>

          <button type="submit" className="login-submit-btn" disabled={isLoading}>
            {isLoading ? <div className="spinner"></div> : 'Giriş Yap'}
          </button>
        </form>

        <div className="login-footer">
          <p>© 2026 Nest Smart Home System. Tüm Hakları Saklıdır.</p>
        </div>
      </div>
    </div>
  );
};

export default LoginPage;
