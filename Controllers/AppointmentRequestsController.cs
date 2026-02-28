using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using LisBlanc.AdminPanel.Data;
using LisBlanc.AdminPanel.Models;
using Microsoft.AspNetCore.Authorization;

namespace LisBlanc.AdminPanel.Controllers
{
    [Authorize]
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
            var applicationDbContext = _context.AppointmentRequests.Include(a => a.Master).Include(a => a.Service);
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
                .FirstOrDefaultAsync(m => m.Id == id);
            if (appointmentRequest == null)
            {
                return NotFound();
            }

            return View(appointmentRequest);
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
            // Получаем услугу, чтобы узнать длительность
            var service = await _context.Services.FindAsync(serviceId);
            if (service == null)
            {
                return Json(new { error = "Услуга не найдена" });
            }

            // Начало и конец выбранного дня
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

            // Получаем все занятые слоты мастера на этот день
            var busySlots = await _context.ScheduleSlots
                .Where(s => s.MasterId == masterId
                    && s.StartTime >= startOfDay
                    && s.StartTime <= endOfDay
                    && s.Status == SlotStatus.Booked)
                .ToListAsync();

            // Получаем выходные/больничные/отпуска мастера
            var dayOffSlots = await _context.ScheduleSlots
                .Where(s => s.MasterId == masterId
                    && s.StartTime >= startOfDay
                    && s.StartTime <= endOfDay
                    && (s.Status == SlotStatus.DayOff
                        || s.Status == SlotStatus.SickLeave
                        || s.Status == SlotStatus.Vacation))
                .ToListAsync();

            // Если есть слот на весь день (выходной и т.д.), то нет свободного времени
            if (dayOffSlots.Any(s => s.StartTime <= startOfDay && s.EndTime >= endOfDay))
            {
                return Json(new List<object>());
            }

            // Генерируем все возможные слоты с 9:00 до 21:00 с шагом 30 минут
            var availableSlots = new List<AvailableSlot>();
            var currentTime = startOfDay.AddHours(9); // Начинаем с 9:00

            while (currentTime < startOfDay.AddHours(21)) // До 21:00
            {
                var slotEnd = currentTime.AddMinutes(service.DurationMinutes);

                // Проверяем, не выходит ли за 21:00
                if (slotEnd > startOfDay.AddHours(21))
                {
                    break;
                }

                // Проверяем, не занят ли слот
                bool isBusy = busySlots.Any(s =>
                    (currentTime >= s.StartTime && currentTime < s.EndTime) ||
                    (slotEnd > s.StartTime && slotEnd <= s.EndTime) ||
                    (s.StartTime >= currentTime && s.StartTime < slotEnd));

                // Проверяем, не попадает ли на выходной/больничный
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

                currentTime = currentTime.AddMinutes(30); // Шаг 30 минут
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
                var appointmentRequest = new AppointmentRequest
                {
                    MasterId = viewModel.MasterId,
                    ServiceId = viewModel.ServiceId,
                    ClientName = viewModel.ClientName,
                    ClientPhone = viewModel.ClientPhone,
                    CreatedAt = viewModel.SelectedDateTime,
                    Status = RequestStatus.Confirmed,
                    RejectionReason = null
                };

                _context.Add(appointmentRequest);
                await _context.SaveChangesAsync();

                // Сразу создаем слот в расписании
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
        public async Task<IActionResult> Edit(int id, [Bind("Id,MasterId,ServiceId,ClientName,ClientPhone,CreatedAt,Status,RejectionReason")] AppointmentRequest appointmentRequest)
        {
            if (id != appointmentRequest.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Получаем оригинальную заявку из базы до изменений
                    var originalRequest = await _context.AppointmentRequests
                        .AsNoTracking()
                        .FirstOrDefaultAsync(ar => ar.Id == id);

                    if (originalRequest == null)
                    {
                        return NotFound();
                    }

                    // Проверяем, изменился ли статус на "Отклонена"
                    if (originalRequest.Status == RequestStatus.Confirmed &&
                        appointmentRequest.Status == RequestStatus.Rejected)
                    {
                        // Удаляем слот из расписания
                        var slot = await _context.ScheduleSlots
                            .FirstOrDefaultAsync(s => s.AppointmentRequestId == id);

                        if (slot != null)
                        {
                            _context.ScheduleSlots.Remove(slot);
                        }
                    }
                    // Проверяем, изменился ли статус с "Отклонена" на "Подтверждена"
                    else if (originalRequest.Status == RequestStatus.Rejected &&
                             appointmentRequest.Status == RequestStatus.Confirmed)
                    {
                        // Создаем новый слот
                        var service = await _context.Services.FindAsync(appointmentRequest.ServiceId);
                        if (service != null)
                        {
                            var slot = new ScheduleSlot
                            {
                                MasterId = appointmentRequest.MasterId,
                                StartTime = appointmentRequest.CreatedAt,
                                EndTime = appointmentRequest.CreatedAt.AddMinutes(service.DurationMinutes),
                                Status = SlotStatus.Booked,
                                AppointmentRequestId = appointmentRequest.Id
                            };
                            _context.ScheduleSlots.Add(slot);
                        }
                    }
                    // Проверяем, изменилось ли время записи у подтвержденной заявки
                    else if (originalRequest.Status == RequestStatus.Confirmed &&
                             appointmentRequest.Status == RequestStatus.Confirmed &&
                             originalRequest.CreatedAt != appointmentRequest.CreatedAt)
                    {
                        // Обновляем время в слоте
                        var slot = await _context.ScheduleSlots
                            .FirstOrDefaultAsync(s => s.AppointmentRequestId == id);

                        if (slot != null)
                        {
                            var service = await _context.Services.FindAsync(appointmentRequest.ServiceId);
                            if (service != null)
                            {
                                slot.StartTime = appointmentRequest.CreatedAt;
                                slot.EndTime = appointmentRequest.CreatedAt.AddMinutes(service.DurationMinutes);
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
                // Удаляем связанный слот
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

            // Меняем статус
            appointmentRequest.Status = RequestStatus.Confirmed;

            // Получаем услугу, чтобы узнать длительность
            var service = await _context.Services.FindAsync(appointmentRequest.ServiceId);
            if (service == null)
            {
                return NotFound("Услуга не найдена");
            }

            // Создаем занятый слот в расписании
            var slot = new ScheduleSlot
            {
                MasterId = appointmentRequest.MasterId,
                StartTime = appointmentRequest.CreatedAt,
                EndTime = appointmentRequest.CreatedAt.AddMinutes(service.DurationMinutes),
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

            // Удаляем слот, если он был создан
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