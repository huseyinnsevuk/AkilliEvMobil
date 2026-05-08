using Microsoft.EntityFrameworkCore;
using AkilliEvBackend.Data;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;

var builder = WebApplication.CreateBuilder(args);

// 1. Firebase Admin SDK Yapılandırması
var firebasePath = builder.Configuration["ExternalServices:Firebase:ServiceAccountPath"];
if (!string.IsNullOrEmpty(firebasePath) && File.Exists(firebasePath))
{
    FirebaseApp.Create(new AppOptions()
    {
        Credential = GoogleCredential.FromFile(firebasePath)
    });
}

// 2. Veritabanı Yapılandırması (SQLite)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

// 3. Servislerin Kaydedilmesi (Dependency Injection)
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(); // API dökümantasyonu için Swagger

// 3. Cors Politikası (Mobil uygulamanın erişebilmesi için)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var app = builder.Build();

// 4. Veritabanının Otomatik Oluşturulması (Migration gerektirmeden başlangıç için)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// 5. Middleware Yapılandırması
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();
