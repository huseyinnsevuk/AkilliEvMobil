namespace AkilliEvMobil.Models
{
    /*
     * RegisterRequest: Kayıt sayfasından backend'e gönderilecek veri paketi.
     */
    public class RegisterRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    /*
     * LoginRequest: Giriş yaparken kullanılacak veri paketi.
     */
    public class LoginRequest
    {
        public string Identifier { get; set; } = string.Empty; // Email veya Telefon
        public string Password { get; set; } = string.Empty;
    }
}
