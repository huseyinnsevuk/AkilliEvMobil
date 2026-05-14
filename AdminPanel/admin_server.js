const express = require('express');
const path = require('path');
const app = express();
const PORT = 80; // Admin panel için standart HTTP portu

app.use(express.static(path.join(__dirname, 'dist')));

app.get('*', (req, res) => {
  res.sendFile(path.join(__dirname, 'dist', 'index.html'));
});

app.listen(PORT, '0.0.0.0', () => {
  console.log(`Admin Panel http://0.0.0.0:${PORT} adresinde çalışıyor 🚀`);
});
