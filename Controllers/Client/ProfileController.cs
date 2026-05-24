using LisBlanc.AdminPanel.Data;
using LisBlanc.AdminPanel.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Security.Claims;

namespace LisBlanc.AdminPanel.Controllers.Client
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProfileController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string tab = "upcoming")
        {
            var username = User.Identity.Name;
            Console.WriteLine($"Имя пользователя из авторизации: {username}");

            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("Login", "Account");
            }

            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var isAdminOrManager = userRole == "Admin" || userRole == "Manager";

            // Получаем ID текущего пользователя
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int? currentUserId = null;
            if (!string.IsNullOrEmpty(userIdClaim))
            {
                currentUserId = int.Parse(userIdClaim);
            }

            IQueryable<AppointmentRequest> query;

            if (isAdminOrManager)
            {
                // Администраторы и менеджеры видят все записи
                query = _context.AppointmentRequests
                    .Include(a => a.Master)
                    .Include(a => a.Service)
                    .Include(a => a.User);
            }
            else
            {
                // Обычные клиенты видят только свои записи (по UserId)
                if (currentUserId.HasValue)
                {
                    query = _context.AppointmentRequests
                        .Include(a => a.Master)
                        .Include(a => a.Service)
                        .Include(a => a.User)
                        .Where(a => a.UserId == currentUserId.Value);
                }
                else
                {
                    // Если UserId нет, пробуем найти по имени (fallback)
                    query = _context.AppointmentRequests
                        .Include(a => a.Master)
                        .Include(a => a.Service)
                        .Include(a => a.User)
                        .Where(a => a.ClientName == username);
                }
            }

            var appointments = await query.ToListAsync();

            Console.WriteLine($"Найдено записей: {appointments.Count}");

            var now = DateTime.Now;

            ViewBag.Upcoming = appointments
                .Where(a => a.AppointmentDate > now && a.Status != RequestStatus.Rejected)
                .ToList();

            ViewBag.Past = appointments
                .Where(a => a.AppointmentDate <= now || a.Status == RequestStatus.Rejected)
                .ToList();

            ViewBag.CurrentTab = tab;
            ViewBag.UserRole = userRole;

            return View("~/Views/Client/Profile/Index.cshtml");
        }

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

            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var username = User.Identity.Name;
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int? currentUserId = !string.IsNullOrEmpty(userIdClaim) ? int.Parse(userIdClaim) : null;

            bool canCancel = false;

            if (userRole == "Admin" || userRole == "Manager")
            {
                canCancel = true;
            }
            else if (currentUserId.HasValue && appointment.UserId == currentUserId.Value)
            {
                canCancel = true;
            }
            else if (appointment.ClientName == username)
            {
                canCancel = true;
            }

            if (!canCancel)
            {
                return Forbid();
            }

            // Проверяем, можно ли отменить (только будущие записи)
            if (appointment.AppointmentDate <= DateTime.Now && userRole != "Admin")
            {
                TempData["Error"] = "Нельзя отменить прошедшую запись";
                return RedirectToAction(nameof(Index));
            }

            appointment.Status = RequestStatus.Rejected;

            if (userRole == "Admin" || userRole == "Manager")
            {
                appointment.RejectionReason = "Отменено администратором";
            }
            else
            {
                appointment.RejectionReason = "Отменено клиентом";
            }

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
            var username = User.Identity.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);

            if (user == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrEmpty(email))
            {
                user.Email = email;
            }

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