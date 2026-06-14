import { useState, useEffect } from 'react';
import { Mail, Calendar, MessageSquare, CornerDownRight, Send, HelpCircle, CheckCircle2 } from 'lucide-react';
import './SupportPage.css';

interface Ticket {
  id: string;
  subject: string;
  message: string;
  userEmail: string;
  userName: string | null;
  status: string;
  reply: string | null;
  createdAt: string;
}

const SupportPage = () => {
  const [tickets, setTickets] = useState<Ticket[]>([]);
  const [selectedTicketId, setSelectedTicketId] = useState<string | null>(null);
  const [replyText, setReplyText] = useState("");
  const [sending, setSending] = useState(false);

  const fetchTickets = () => {
    fetch('http://141.98.48.101:3000/api/support/tickets')
      .then(res => res.json())
      .then(data => {
        setTickets(data);
        if (data.length > 0 && !selectedTicketId) {
          setSelectedTicketId(data[0].id);
        }
      })
      .catch(err => console.error("Destek talepleri çekilemedi:", err));
  };

  useEffect(() => {
    fetchTickets();
  }, []);

  const selectedTicket = tickets.find(t => t.id === selectedTicketId);

  const handleSendReply = async () => {
    if (!selectedTicket || !replyText.trim() || sending) return;

    try {
      setSending(true);
      const res = await fetch(`http://141.98.48.101:3000/api/support/tickets/${selectedTicket.id}/reply`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ reply: replyText })
      });

      if (res.ok) {
        setReplyText("");
        fetchTickets(); // Listeyi yenile
      } else {
        alert("Cevap iletilemedi. Sunucu hatası.");
      }
    } catch (err) {
      console.error(err);
      alert("Bağlantı hatası oluştu.");
    } finally {
      setSending(false);
    }
  };

  return (
    <div className="support-page">
      {/* Sol Taraf: Talepler Listesi */}
      <div className="tickets-sidebar glass-panel">
        <div className="sidebar-header">
          <h2>Destek Talepleri</h2>
          <p>Müşterilerden gelen güncel destek ve yardım mesajları.</p>
        </div>

        <div className="tickets-list">
          {tickets.map(ticket => (
            <div
              key={ticket.id}
              className={`ticket-list-item ${selectedTicketId === ticket.id ? 'active' : ''}`}
              onClick={() => setSelectedTicketId(ticket.id)}
            >
              <div className="ticket-list-header">
                <h4>{ticket.subject}</h4>
                <span className={`status-tag ${ticket.status.toLowerCase()}`}>
                  {ticket.status === 'Open' ? 'Açık' : 'Yanıtlandı'}
                </span>
              </div>
              <p className="ticket-excerpt">{ticket.message.substring(0, 50)}{ticket.message.length > 50 ? '...' : ''}</p>
              <div className="ticket-meta">
                <span>{ticket.userName || ticket.userEmail}</span>
                <span>{new Date(ticket.createdAt).toLocaleDateString('tr-TR')}</span>
              </div>
            </div>
          ))}
          {tickets.length === 0 && (
            <div className="no-tickets-sidebar">
              <HelpCircle size={32} />
              <p>Destek talebi bulunmuyor.</p>
            </div>
          )}
        </div>
      </div>

      {/* Sağ Taraf: Detay ve Cevaplama */}
      <div className="ticket-detail-area">
        {selectedTicket ? (
          <>
            {/* Talep İçerik Kartı */}
            <div className="ticket-detail-card glass-panel">
              <div className="detail-header">
                <div className="header-info">
                  <h2>{selectedTicket.subject}</h2>
                  <span className={`status-badge ${selectedTicket.status.toLowerCase()}`}>
                    {selectedTicket.status === 'Open' ? 'Açık Talep' : 'Yanıtlandı'}
                  </span>
                </div>
                <div className="sender-meta">
                  <span className="meta-item">
                    <Mail size={14} /> <strong>{selectedTicket.userName || 'Kullanıcı'}</strong> ({selectedTicket.userEmail})
                  </span>
                  <span className="meta-item">
                    <Calendar size={14} /> {new Date(selectedTicket.createdAt).toLocaleString('tr-TR')}
                  </span>
                </div>
              </div>

              <div className="detail-body">
                <h3>Kullanıcı Mesajı:</h3>
                <div className="message-content">
                  {selectedTicket.message}
                </div>
              </div>
            </div>

            {/* Cevap Geçmişi veya Formu */}
            {selectedTicket.status === 'Replied' ? (
              <div className="reply-card glass-panel resolved">
                <div className="reply-header">
                  <CheckCircle2 size={18} className="success-icon" />
                  <h3>Yönetici Yanıtı</h3>
                </div>
                <div className="reply-body">
                  <div className="reply-icon-wrapper">
                    <CornerDownRight size={18} />
                  </div>
                  <div className="reply-content">
                    {selectedTicket.reply}
                  </div>
                </div>
              </div>
            ) : (
              <div className="reply-card glass-panel open">
                <div className="reply-header">
                  <MessageSquare size={18} className="pending-icon" />
                  <h3>Talebi Yanıtla</h3>
                  <span className="email-warning">(Yanıtınız müşteriye otomatik olarak e-posta ile gönderilecektir.)</span>
                </div>
                <div className="reply-form">
                  <textarea
                    placeholder="Müşteriye gönderilecek cevabı buraya yazın..."
                    value={replyText}
                    onChange={(e) => setReplyText(e.target.value)}
                    rows={6}
                    className="reply-textarea"
                  />
                  <button
                    className="send-reply-btn"
                    onClick={handleSendReply}
                    disabled={!replyText.trim() || sending}
                  >
                    {sending ? (
                      'Gönderiliyor...'
                    ) : (
                      <>
                        <Send size={16} /> Yanıtla ve Mail Gönder
                      </>
                    )}
                  </button>
                </div>
              </div>
            )}
          </>
        ) : (
          <div className="no-selection glass-panel">
            <HelpCircle size={48} />
            <h3>Talep Seçilmedi</h3>
            <p>Detayları görmek ve cevaplamak için sol taraftan bir destek talebi seçin.</p>
          </div>
        )}
      </div>
    </div>
  );
};

export default SupportPage;
