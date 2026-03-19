using LisBlanc.AdminPanel.Data;
using LisBlanc.AdminPanel.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace LisBlanc.AdminPanel.Controllers.Client
{
    
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProfileController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string tab = "upcoming")
        {
            var phone = User.Identity.Name;
            Console.WriteLine($"Телефон из авторизации: {phone}");

            if (string.IsNullOrEmpty(phone))
            {
                return RedirectToAction("Login", "Account");
            }

            var appointments = await _context.AppointmentRequests
     .Include(a => a.Master)
     .Include(a => a.Service)
     .Where(a => a.ClientPhone == phone)
     .ToListAsync();

            Console.WriteLine($"Найдено записей: {appointments.Count}");

            var now = DateTime.Now;

            ViewBag.Upcoming = appointments
                .Where(a => a.CreatedAt > now && a.Status != RequestStatus.Rejected)
                .ToList();

            ViewBag.Past = appointments
                .Where(a => a.CreatedAt <= now || a.Status == RequestStatus.Rejected)
                .ToList();

            ViewBag.CurrentTab = tab;

            return View("~/Views/Client/Profile/Index.cshtml");
        }

        // POST: /Client/Profile/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var appointment = await _context.AppointmentRequests
                .Include(a => a.Service)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null)
            {
                return NotFound();
            }

            // Проверяем, что это запись текущего клиента
            if (appointment.ClientPhone != User.Identity.Name)
            {
                return Forbid();
            }

            // Проверяем, можно ли отменить (не прошло ли уже время)
            if (appointment.CreatedAt <= DateTime.Now)
            {
                TempData["Error"] = "Нельзя отменить прошедшую запись";
                return RedirectToAction(nameof(Index));
            }

            // Меняем статус заявки
            appointment.Status = RequestStatus.Rejected;
            appointment.RejectionReason = "Отменено клиентом";

            // Удаляем слот из расписания
            var slot = await _context.ScheduleSlots
                .FirstOrDefaultAsync(s => s.AppointmentRequestId == id);

            if (slot != null)
            {
                _context.ScheduleSlots.Remove(slot);
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Запись успешно отменена";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile(string email, string newPassword, string confirmPassword)
        {
            var phone = User.Identity.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == phone);

            if (user == null)
            {
                return NotFound();
            }

            // Обновляем email, если ввели
            if (!string.IsNullOrEmpty(email))
            {
                user.Email = email;
            }

            // Обновляем пароль, если ввели
            if (!string.IsNullOrEmpty(newPassword))
            {
                if (newPassword != confirmPassword)
                {
                    TempData["Error"] = "Пароли не совпадают";
                    return RedirectToAction(nameof(Index), new { tab = "settings" });
                }
                user.PasswordHash = HashPassword(newPassword);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Данные успешно обновлены";
            return RedirectToAction(nameof(Index), new { tab = "settings" });
        }

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