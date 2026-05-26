using LisBlanc.AdminPanel.Data;
using LisBlanc.AdminPanel.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Security.Claims;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.IO;

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
        // GET: Profile/PrintWord/5
        public async Task<IActionResult> PrintWord(int id)
        {
            var appointment = await _context.AppointmentRequests
                .Include(a => a.Master)
                .Include(a => a.Service)
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null) return NotFound();

            // Проверка прав
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var username = User.Identity.Name;
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int? currentUserId = !string.IsNullOrEmpty(userIdClaim) ? int.Parse(userIdClaim) : null;

            bool canView = (userRole == "Admin" || userRole == "Manager") ||
                           (currentUserId.HasValue && appointment.UserId == currentUserId.Value) ||
                           appointment.ClientName == username;

            if (!canView) return Forbid();

            using (MemoryStream stream = new MemoryStream())
            {
                using (WordprocessingDocument wordDocument = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
                {
                    MainDocumentPart mainPart = wordDocument.AddMainDocumentPart();
                    mainPart.Document = new Document();
                    Body body = mainPart.Document.AppendChild(new Body());

                    // Стилизация как талон
                    Paragraph headerPara = new Paragraph(
                        new ParagraphProperties(new Justification() { Val = JustificationValues.Center }),
                        new Run(new Text("САЛОН КРАСОТЫ LIS BLANC"))
                        {
                            RunProperties = new RunProperties(new Bold(), new FontSize() { Val = "32" })
                        });
                    body.AppendChild(headerPara);

                    Paragraph ticketPara = new Paragraph(
                        new ParagraphProperties(new Justification() { Val = JustificationValues.Center }),
                        new Run(new Text("ТАЛОН ЗАПИСИ"))
                        {
                            RunProperties = new RunProperties(new Bold(), new FontSize() { Val = "36" })
                        });
                    body.AppendChild(ticketPara);

                    Paragraph numberPara = new Paragraph(
                        new ParagraphProperties(new Justification() { Val = JustificationValues.Center }),
                        new Run(new Text($"№ {appointment.Id}"))
                        {
                            RunProperties = new RunProperties(new Bold(), new FontSize() { Val = "44" })
                        });
                    body.AppendChild(numberPara);

                    body.AppendChild(new Paragraph(new Run(new Break())));

                    // Информация
                    AddClientInfoRow(body, "Дата и время:", appointment.AppointmentDate?.ToString("dd.MM.yyyy HH:mm") ?? "Не указано");
                    AddClientInfoRow(body, "Услуга:", appointment.Service?.Name ?? "—");
                    AddClientInfoRow(body, "Длительность:", $"{appointment.Service?.DurationMinutes ?? 0} минут");
                    AddClientInfoRow(body, "Стоимость:", $"{appointment.Service?.Price.ToString("N0") ?? "0"} ₽");
                    AddClientInfoRow(body, "Мастер:", appointment.Master?.FullName ?? "—");
                    AddClientInfoRow(body, "Статус:", GetClientStatusText(appointment.Status));

                    body.AppendChild(new Paragraph(new Run(new Break())));

                    // Памятка
                    Paragraph reminderPara = new Paragraph(
                        new ParagraphProperties(new Justification() { Val = JustificationValues.Center }),
                        new Run(new Text("ВНИМАНИЕ!"))
                        {
                            RunProperties = new RunProperties(new Bold(), new FontSize() { Val = "22" })
                        });
                    body.AppendChild(reminderPara);

                    body.AppendChild(new Paragraph(new Run(new Text("• Приходите за 5-10 минут до записи"))));
                    body.AppendChild(new Paragraph(new Run(new Text("• При опоздании более 15 минут запись может быть перенесена"))));
                    body.AppendChild(new Paragraph(new Run(new Text("• С животными строго запрещено"))));

                    body.AppendChild(new Paragraph(new Run(new Break())));

                    Paragraph footerPara = new Paragraph(
                        new ParagraphProperties(new Justification() { Val = JustificationValues.Center }),
                        new Run(new Text($"Талон сформирован {DateTime.Now:dd.MM.yyyy HH:mm}"))
                        {
                            RunProperties = new RunProperties(new FontSize() { Val = "18" })
                        });
                    body.AppendChild(footerPara);
                }

                stream.Position = 0;
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"Талон_{appointment.Id}_{DateTime.Now:yyyyMMdd}.docx");
            }
        }

        private void AddClientInfoRow(Body body, string label, string value)
        {
            Paragraph para = new Paragraph(
                new ParagraphProperties(new SpacingBetweenLines() { After = "100" }),
                new Run(new Text(label))
                {
                    RunProperties = new RunProperties(new Bold(), new FontSize() { Val = "24" })
                },
                new Run(new TabChar()),
                new Run(new Text(value))
                {
                    RunProperties = new RunProperties(new FontSize() { Val = "24" })
                });
            body.AppendChild(para);
        }

        private string GetClientStatusText(RequestStatus status)
        {
            return status switch
            {
                RequestStatus.Confirmed => "ПОДТВЕРЖДЕНА ✓",
                RequestStatus.Rejected => "ОТКЛОНЕНА ✗",
                RequestStatus.New => "В РАБОТЕ",
                _ => status.ToString()
            };
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