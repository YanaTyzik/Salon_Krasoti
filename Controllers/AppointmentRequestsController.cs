using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using LisBlanc.AdminPanel.Data;
using LisBlanc.AdminPanel.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace LisBlanc.AdminPanel.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class AppointmentRequestsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AppointmentRequestsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: AppointmentRequests
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.AppointmentRequests
                .Include(a => a.Master)
                .Include(a => a.Service)
                .Include(a => a.User);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: AppointmentRequests/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var appointmentRequest = await _context.AppointmentRequests
                .Include(a => a.Master)
                .Include(a => a.Service)
                .Include(a => a.User)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (appointmentRequest == null)
            {
                return NotFound();
            }

            return View(appointmentRequest);
        }
        // GET: AppointmentRequests/PrintWord/5
        public async Task<IActionResult> PrintWord(int? id)
        {
            if (id == null) return NotFound();

            var appointment = await _context.AppointmentRequests
                .Include(a => a.Master)
                .Include(a => a.Service)
                .Include(a => a.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (appointment == null) return NotFound();

            using (MemoryStream stream = new MemoryStream())
            {
                // Создаём Word-документ
                using (WordprocessingDocument wordDocument = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
                {
                    MainDocumentPart mainPart = wordDocument.AddMainDocumentPart();
                    mainPart.Document = new Document();
                    Body body = mainPart.Document.AppendChild(new Body());

                    // Заголовок
                    Paragraph headerPara = new Paragraph(
                        new ParagraphProperties(new Justification() { Val = JustificationValues.Center }),
                        new Run(new Text("САЛОН КРАСОТЫ LIS BLANC"))
                        {
                            RunProperties = new RunProperties(new Bold(), new FontSize() { Val = "32" })
                        });
                    body.AppendChild(headerPara);

                    body.AppendChild(new Paragraph(new Run(new Break())));

                    // Номер заявки
                    Paragraph titlePara = new Paragraph(
                        new ParagraphProperties(new Justification() { Val = JustificationValues.Center }),
                        new Run(new Text($"ЗАЯВКА НА ЗАПИСЬ № {appointment.Id}"))
                        {
                            RunProperties = new RunProperties(new Bold(), new FontSize() { Val = "28" })
                        });
                    body.AppendChild(titlePara);

                    body.AppendChild(new Paragraph(new Run(new Break())));

                    // Даты
                    AddInfoRow(body, "Дата создания заявки:", appointment.CreatedAt.ToString("dd.MM.yyyy HH:mm"));
                    AddInfoRow(body, "Статус:", GetStatusText(appointment.Status));

                    if (!string.IsNullOrEmpty(appointment.RejectionReason))
                    {
                        AddInfoRow(body, "Причина отказа:", appointment.RejectionReason);
                    }

                    body.AppendChild(new Paragraph(new Run(new Break())));

                    // Заголовок секции "Клиент"
                    AddSectionHeader(body, "ИНФОРМАЦИЯ О КЛИЕНТЕ");
                    AddInfoRow(body, "Имя клиента:", appointment.ClientName);
                    AddInfoRow(body, "Телефон:", appointment.ClientPhone);

                    body.AppendChild(new Paragraph(new Run(new Break())));

                    // Заголовок секции "Запись"
                    AddSectionHeader(body, "ИНФОРМАЦИЯ О ЗАПИСИ");
                    AddInfoRow(body, "Мастер:", appointment.Master?.FullName ?? "Не указан");
                    AddInfoRow(body, "Специализация:", appointment.Master?.Specialization ?? "—");
                    AddInfoRow(body, "Услуга:", appointment.Service?.Name ?? "Не указана");
                    AddInfoRow(body, "Длительность:", $"{appointment.Service?.DurationMinutes ?? 0} минут");
                    AddInfoRow(body, "Стоимость:", $"{appointment.Service?.Price.ToString("N0") ?? "0"} ₽");
                    AddInfoRow(body, "Дата и время записи:", appointment.AppointmentDate?.ToString("dd.MM.yyyy HH:mm") ?? "Не указано");

                    body.AppendChild(new Paragraph(new Run(new Break())));

                    // Подписи
                    Paragraph signaturesPara = new Paragraph(
                        new ParagraphProperties(new Justification() { Val = JustificationValues.Both }),
                        new Run(new TabChar()),
                        new Run(new Text("____________________")),
                        new Run(new TabChar()),
                        new Run(new TabChar()),
                        new Run(new Text("____________________")));
                    body.AppendChild(signaturesPara);

                    Paragraph signaturesTextPara = new Paragraph(
                        new ParagraphProperties(new Justification() { Val = JustificationValues.Both }),
                        new Run(new Text("Подпись клиента")),
                        new Run(new TabChar()),
                        new Run(new TabChar()),
                        new Run(new TabChar()),
                        new Run(new Text("Подпись мастера")));
                    body.AppendChild(signaturesTextPara);

                    body.AppendChild(new Paragraph(new Run(new Break())));

                    // Футер
                    Paragraph footerPara = new Paragraph(
                        new ParagraphProperties(new Justification() { Val = JustificationValues.Center }),
                        new Run(new Text($"Документ сформирован {DateTime.Now:dd.MM.yyyy HH:mm:ss}"))
                        {
                            RunProperties = new RunProperties(new FontSize() { Val = "20" })
                        });
                    body.AppendChild(footerPara);
                }

                stream.Position = 0;
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"Заявка_{appointment.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.docx");
            }
        }

        // Вспомогательные методы
        private void AddSectionHeader(Body body, string text)
        {
            Paragraph para = new Paragraph(
                new ParagraphProperties(
                    new Justification() { Val = JustificationValues.Center },
                    new SpacingBetweenLines() { Before = "240", After = "120" }),
                new Run(new Text(text))
                {
                    RunProperties = new RunProperties(new Bold(), new FontSize() { Val = "24" })
                });
            body.AppendChild(para);
        }

        private void AddInfoRow(Body body, string label, string value)
        {
            Paragraph para = new Paragraph(
                new ParagraphProperties(new SpacingBetweenLines() { After = "80" }),
                new Run(new Text(label))
                {
                    RunProperties = new RunProperties(new Bold(), new FontSize() { Val = "22" })
                },
                new Run(new TabChar()),
                new Run(new Text(value))
                {
                    RunProperties = new RunProperties(new FontSize() { Val = "22" })
                });
            body.AppendChild(para);
        }

        private string GetStatusText(RequestStatus status)
        {
            return status switch
            {
                RequestStatus.Confirmed => "ПОДТВЕРЖДЕНА",
                RequestStatus.Rejected => "ОТКЛОНЕНА",
                RequestStatus.New => "НОВАЯ",
                _ => status.ToString()
            };
        }

        // GET: AppointmentRequests/Create
        public async Task<IActionResult> Create()
        {
            var viewModel = new CreateAppointmentRequestViewModel();

            ViewData["MasterId"] = new SelectList(_context.Masters, "Id", "FirstName");
            ViewData["ServiceId"] = new SelectList(_context.Services, "Id", "Name");

            return View(viewModel);
        }

        // GET: AppointmentRequests/GetAvailableSlots
        public async Task<IActionResult> GetAvailableSlots(int masterId, int serviceId, DateTime date)
        {
            var service = await _context.Services.FindAsync(serviceId);
            if (service == null)
            {
                return Json(new { error = "Услуга не найдена" });
            }

            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

            var busySlots = await _context.ScheduleSlots
                .Where(s => s.MasterId == masterId
                    && s.StartTime >= startOfDay
                    && s.StartTime <= endOfDay
                    && s.Status == SlotStatus.Booked)
                .ToListAsync();

            var dayOffSlots = await _context.ScheduleSlots
                .Where(s => s.MasterId == masterId
                    && s.StartTime >= startOfDay
                    && s.StartTime <= endOfDay
                    && (s.Status == SlotStatus.DayOff
                        || s.Status == SlotStatus.SickLeave
                        || s.Status == SlotStatus.Vacation))
                .ToListAsync();

            if (dayOffSlots.Any(s => s.StartTime <= startOfDay && s.EndTime >= endOfDay))
            {
                return Json(new List<object>());
            }

            var availableSlots = new List<AvailableSlot>();
            var currentTime = startOfDay.AddHours(9);

            while (currentTime < startOfDay.AddHours(21))
            {
                var slotEnd = currentTime.AddMinutes(service.DurationMinutes);

                if (slotEnd > startOfDay.AddHours(21))
                {
                    break;
                }

                bool isBusy = busySlots.Any(s =>
                    (currentTime >= s.StartTime && currentTime < s.EndTime) ||
                    (slotEnd > s.StartTime && slotEnd <= s.EndTime) ||
                    (s.StartTime >= currentTime && s.StartTime < slotEnd));

                bool isDayOff = dayOffSlots.Any(s =>
                    currentTime >= s.StartTime && slotEnd <= s.EndTime);

                if (!isBusy && !isDayOff)
                {
                    availableSlots.Add(new AvailableSlot
                    {
                        StartTime = currentTime,
                        EndTime = slotEnd,
                        DisplayTime = currentTime.ToString("HH:mm")
                    });
                }

                currentTime = currentTime.AddMinutes(30);
            }

            return Json(availableSlots.Select(s => new {
                time = s.StartTime.ToString("HH:mm"),
                value = s.StartTime.ToString("yyyy-MM-dd HH:mm:ss")
            }));
        }


        // POST: AppointmentRequests/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateAppointmentRequestViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                // Получаем ID текущего пользователя (если есть авторизация)
                int? userId = null;
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userIdClaim))
                {
                    var user = await _context.Users.FirstOrDefaultAsync(u => u.Id.ToString() == userIdClaim);
                    if (user != null)
                    {
                        userId = user.Id;
                    }
                }

                var appointmentRequest = new AppointmentRequest
                {
                    MasterId = viewModel.MasterId,
                    ServiceId = viewModel.ServiceId,
                    ClientName = viewModel.ClientName,
                    ClientPhone = viewModel.ClientPhone,
                    CreatedAt = DateTime.Now,
                    AppointmentDate = viewModel.SelectedDateTime,
                    Status = RequestStatus.Confirmed,
                    RejectionReason = null,
                    UserId = userId
                };

                _context.Add(appointmentRequest);
                await _context.SaveChangesAsync();

                var service = await _context.Services.FindAsync(viewModel.ServiceId);
                if (service != null)
                {
                    var slot = new ScheduleSlot
                    {
                        MasterId = viewModel.MasterId,
                        StartTime = viewModel.SelectedDateTime,
                        EndTime = viewModel.SelectedDateTime.AddMinutes(service.DurationMinutes),
                        Status = SlotStatus.Booked,
                        AppointmentRequestId = appointmentRequest.Id
                    };

                    _context.ScheduleSlots.Add(slot);
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Index));
            }

            ViewData["MasterId"] = new SelectList(_context.Masters, "Id", "FirstName", viewModel.MasterId);
            ViewData["ServiceId"] = new SelectList(_context.Services, "Id", "Name", viewModel.ServiceId);
            return View(viewModel);
        }

        // GET: AppointmentRequests/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var appointmentRequest = await _context.AppointmentRequests.FindAsync(id);
            if (appointmentRequest == null)
            {
                return NotFound();
            }
            ViewData["MasterId"] = new SelectList(_context.Masters, "Id", "FirstName", appointmentRequest.MasterId);
            ViewData["ServiceId"] = new SelectList(_context.Services, "Id", "Name", appointmentRequest.ServiceId);
            return View(appointmentRequest);
        }

        // POST: AppointmentRequests/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,MasterId,ServiceId,ClientName,ClientPhone,CreatedAt,AppointmentDate,Status,RejectionReason,UserId")] AppointmentRequest appointmentRequest)
        {
            if (id != appointmentRequest.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var originalRequest = await _context.AppointmentRequests
                        .AsNoTracking()
                        .FirstOrDefaultAsync(ar => ar.Id == id);

                    if (originalRequest == null)
                    {
                        return NotFound();
                    }

                    var appointmentTime = appointmentRequest.AppointmentDate ?? appointmentRequest.CreatedAt;
                    var originalTime = originalRequest.AppointmentDate ?? originalRequest.CreatedAt;

                    // Изменился статус на "Отклонена"
                    if (originalRequest.Status == RequestStatus.Confirmed &&
                        appointmentRequest.Status == RequestStatus.Rejected)
                    {
                        var slot = await _context.ScheduleSlots
                            .FirstOrDefaultAsync(s => s.AppointmentRequestId == id);

                        if (slot != null)
                        {
                            _context.ScheduleSlots.Remove(slot);
                        }
                    }
                    // Изменился статус с "Отклонена" на "Подтверждена"
                    else if (originalRequest.Status == RequestStatus.Rejected &&
                             appointmentRequest.Status == RequestStatus.Confirmed)
                    {
                        var service = await _context.Services.FindAsync(appointmentRequest.ServiceId);
                        if (service != null)
                        {
                            var slot = new ScheduleSlot
                            {
                                MasterId = appointmentRequest.MasterId,
                                StartTime = appointmentTime,
                                EndTime = appointmentTime.AddMinutes(service.DurationMinutes),
                                Status = SlotStatus.Booked,
                                AppointmentRequestId = appointmentRequest.Id
                            };
                            _context.ScheduleSlots.Add(slot);
                        }
                    }
                    // Изменилось время у подтвержденной заявки
                    else if (originalRequest.Status == RequestStatus.Confirmed &&
                             appointmentRequest.Status == RequestStatus.Confirmed &&
                             originalTime != appointmentTime)
                    {
                        var slot = await _context.ScheduleSlots
                            .FirstOrDefaultAsync(s => s.AppointmentRequestId == id);

                        if (slot != null)
                        {
                            var service = await _context.Services.FindAsync(appointmentRequest.ServiceId);
                            if (service != null)
                            {
                                slot.StartTime = appointmentTime;
                                slot.EndTime = appointmentTime.AddMinutes(service.DurationMinutes);
                                _context.ScheduleSlots.Update(slot);
                            }
                        }
                    }

                    _context.Update(appointmentRequest);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AppointmentRequestExists(appointmentRequest.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            ViewData["MasterId"] = new SelectList(_context.Masters, "Id", "FirstName", appointmentRequest.MasterId);
            ViewData["ServiceId"] = new SelectList(_context.Services, "Id", "Name", appointmentRequest.ServiceId);
            return View(appointmentRequest);
        }

        // GET: AppointmentRequests/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var appointmentRequest = await _context.AppointmentRequests
                .Include(a => a.Master)
                .Include(a => a.Service)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (appointmentRequest == null)
            {
                return NotFound();
            }

            return View(appointmentRequest);
        }

        // POST: AppointmentRequests/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var appointmentRequest = await _context.AppointmentRequests.FindAsync(id);
            if (appointmentRequest != null)
            {
                var slot = await _context.ScheduleSlots
                    .FirstOrDefaultAsync(s => s.AppointmentRequestId == id);
                if (slot != null)
                {
                    _context.ScheduleSlots.Remove(slot);
                }

                _context.AppointmentRequests.Remove(appointmentRequest);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool AppointmentRequestExists(int id)
        {
            return _context.AppointmentRequests.Any(e => e.Id == id);
        }

        // GET: AppointmentRequests/Confirm/5
        public async Task<IActionResult> Confirm(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var appointmentRequest = await _context.AppointmentRequests
                .Include(ar => ar.Master)
                .Include(ar => ar.Service)
                .FirstOrDefaultAsync(ar => ar.Id == id);

            if (appointmentRequest == null)
            {
                return NotFound();
            }

            return View(appointmentRequest);
        }

        // POST: AppointmentRequests/Confirm/5
        [HttpPost, ActionName("Confirm")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmConfirmed(int id)
        {
            var appointmentRequest = await _context.AppointmentRequests
                .Include(ar => ar.Service)
                .FirstOrDefaultAsync(ar => ar.Id == id);

            if (appointmentRequest == null)
            {
                return NotFound();
            }

            appointmentRequest.Status = RequestStatus.Confirmed;

            var service = await _context.Services.FindAsync(appointmentRequest.ServiceId);
            if (service == null)
            {
                return NotFound("Услуга не найдена");
            }

            var appointmentTime = appointmentRequest.AppointmentDate ?? appointmentRequest.CreatedAt;

            var slot = new ScheduleSlot
            {
                MasterId = appointmentRequest.MasterId,
                StartTime = appointmentTime,
                EndTime = appointmentTime.AddMinutes(service.DurationMinutes),
                Status = SlotStatus.Booked,
                AppointmentRequestId = appointmentRequest.Id
            };

            _context.ScheduleSlots.Add(slot);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // GET: AppointmentRequests/Reject/5
        public async Task<IActionResult> Reject(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var appointmentRequest = await _context.AppointmentRequests
                .Include(ar => ar.Master)
                .Include(ar => ar.Service)
                .FirstOrDefaultAsync(ar => ar.Id == id);

            if (appointmentRequest == null)
            {
                return NotFound();
            }

            return View(appointmentRequest);
        }

        // POST: AppointmentRequests/Reject/5
        [HttpPost, ActionName("Reject")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectConfirmed(int id, string reason)
        {
            var appointmentRequest = await _context.AppointmentRequests.FindAsync(id);

            if (appointmentRequest == null)
            {
                return NotFound();
            }

            appointmentRequest.Status = RequestStatus.Rejected;
            appointmentRequest.RejectionReason = reason;

            var slot = await _context.ScheduleSlots
                .FirstOrDefaultAsync(s => s.AppointmentRequestId == id);
            if (slot != null)
            {
                _context.ScheduleSlots.Remove(slot);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}