using Microsoft.AspNetCore.Mvc;
using AkilliEvBackend.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System;

namespace AkilliEvBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly Data.AppDbContext _context;

        public AuthController(Data.AppDbContext context)
        {
            _context = context;
        }
        
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] FirebaseRegisterRequest request)
        {
            try 
            {
                // 1. Firebase Token Doğrulama
                var decodedToken = await FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance
                    .VerifyIdTokenAsync(request.FirebaseToken);
                
                var uid = decodedToken.Uid;

                // 2. Email kontrolü (Local DB)
                if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                    return Ok(new { Message = "Giriş başarılı." }); // Zaten kayıtlıysa giriş yapmış sayılırlar

                // 3. Yeni kullanıcı oluştur ve Firebase UID ile eşleştir
                var newUser = new User
                {
                    FullName = request.FullName,
                    Email = request.Email,
                    PhoneNumber = request.PhoneNumber,
                    PasswordHash = "Firebase-Auth", // Şifre Firebase'de saklı
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();
                
                return Ok(new { Message = "Kayıt başarılı.", UserId = newUser.Id });
            }
            catch (Exception ex)
            {
                return BadRequest("Geçersiz Firebase Token: " + ex.Message);
            }
        }
    }

    public class FirebaseRegisterRequest
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string FirebaseToken { get; set; } // Mobilden gelen doğrulama anahtarı
    }
}
