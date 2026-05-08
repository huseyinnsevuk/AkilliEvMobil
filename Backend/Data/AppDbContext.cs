using Microsoft.EntityFrameworkCore;
using AkilliEvBackend.Models;

namespace AkilliEvBackend.Data
{
    /*
     * AppDbContext: Entity Framework Core'un veritabanı ile konuşan ana sınıfı.
     * Bu sınıf tablolarımızı (DbSet) ve veritabanı ayarlarımızı barındırır.
     */
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Veritabanındaki 'Users' tablosu
        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Email alanının benzersiz (unique) olmasını sağlıyoruz
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();
        }
    }
}
