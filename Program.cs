using LisBlanc.AdminPanel.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddXmlSerializerFormatters(); // Добавляем поддержку XML
builder.Services.AddControllersWithViews()
    .AddXmlSerializerFormatters()
    .AddMvcOptions(options =>
    {
        // Добавляем поддержку XML в согласование содержимого
        options.FormatterMappings.SetMediaTypeMappingForFormat("xml", "application/xml");
        options.FormatterMappings.SetMediaTypeMappingForFormat("json", "application/json");
    });
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
// Добавляем настройки аутентификации
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login"; // Если не авторизован, отправлять сюда
        options.AccessDeniedPath = "/Account/AccessDenied"; // Если нет прав
        options.ExpireTimeSpan = TimeSpan.FromHours(8); // Куки живут 8 часов
    });

builder.Services.AddAuthorization(); // Добавляем авторизацию

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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
