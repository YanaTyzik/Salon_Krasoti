using LisBlanc.AdminPanel.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Cryptography;
using System.Text;
using LisBlanc.AdminPanel.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddXmlSerializerFormatters();
builder.Services.AddControllersWithViews()
    .AddXmlSerializerFormatters()
    .AddMvcOptions(options =>
    {
        options.FormatterMappings.SetMediaTypeMappingForFormat("xml", "application/xml");
        options.FormatterMappings.SetMediaTypeMappingForFormat("json", "application/json");
    });

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Автоматическое создание администратора при первом запуске
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // Применяем миграции, если они есть
    context.Database.EnsureCreated();

    var adminExists = context.Users.Any(u => u.Role == "Admin");

    if (!adminExists)
    {
        string hashedPassword;
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes("admin123"));
            StringBuilder builder2 = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                builder2.Append(bytes[i].ToString("x2"));
            }
            hashedPassword = builder2.ToString();
        }

        var admin = new User
        {
            Username = "admin",
            PasswordHash = hashedPassword,
            Email = "admin@lisblanc.ru",
            Role = "Admin"
        };

        context.Users.Add(admin);
        context.SaveChanges();

        Console.WriteLine("Администратор создан: логин 'admin', пароль 'admin123'");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
