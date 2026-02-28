using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using LisBlanc.AdminPanel.Models;
using LisBlanc.AdminPanel.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace LisBlanc.AdminPanel.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            // Ищем пользователя в базе
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user == null)
            {
                ViewBag.Error = "Неверный логин или пароль";
                return View();
            }

            // Проверяем пароль (хешируем введённый и сравниваем с хешем из базы)
            string hashedPassword = HashPassword(password);
            if (user.PasswordHash != hashedPassword)
            {
                ViewBag.Error = "Неверный логин или пароль";
                return View();
            }

            // Создаем "удостоверение личности" для пользователя
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            // Выполняем вход (создаем куки)
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity));

            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/Register (только для первого админа, потом можно убрать)
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        public async Task<IActionResult> Register(string username, string password, string email)
        {
            // Проверяем, нет ли уже такого пользователя
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == username);

            if (existingUser != null)
            {
                ViewBag.Error = "Пользователь с таким логином уже существует";
                return View();
            }

            // Создаем нового пользователя
            var user = new User
            {
                Username = username,
                PasswordHash = HashPassword(password),
                Email = email,
                Role = "Admin"
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return RedirectToAction("Login");
        }

        // GET: /Account/Logout
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        // Вспомогательный метод для хеширования пароля
        private string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}