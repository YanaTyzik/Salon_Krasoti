using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LisBlanc.AdminPanel.Data;
using LisBlanc.AdminPanel.Models;

namespace LisBlanc.AdminPanel.Controllers
{
    [Authorize]
    public class ScheduleController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ScheduleController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Schedule
        public async Task<IActionResult> Index(DateTime? date)
        {
            // Если дата не выбрана, показываем сегодня
            DateTime selectedDate = date ?? DateTime.Today;

            // Получаем всех активных мастеров
            var masters = await _context.Masters
                .Where(m => m.IsActive)
                .ToListAsync();

            // Получаем слоты на выбранную дату (с 00:00 до 23:59)
            var startOfDay = selectedDate.Date;
            var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

            // Загружаем слоты с мастерами и заявками (а через заявки - услуги)
            var slots = await _context.ScheduleSlots
                .Include(s => s.Master)
                .Include(s => s.AppointmentRequest)
                    .ThenInclude(ar => ar.Service)  // Подгружаем услугу через заявку
                .Where(s => s.StartTime >= startOfDay && s.StartTime <= endOfDay)
                .ToListAsync();

            // Создаем список временных слотов (с 9:00 до 21:00 с шагом 30 минут)
            var timeSlots = new List<string>();
            for (int hour = 9; hour <= 21; hour++)
            {
                timeSlots.Add($"{hour:D2}:00");
                if (hour < 21) // Не добавляем 21:30
                {
                    timeSlots.Add($"{hour:D2}:30");
                }
            }

            var viewModel = new ScheduleViewModel
            {
                SelectedDate = selectedDate,
                Masters = masters,
                Slots = slots,
                TimeSlots = timeSlots
            };

            return View(viewModel);
        }

        // GET: Schedule/CreateBlock
        public async Task<IActionResult> CreateBlock()
        {
            // Получаем список активных мастеров для выпадающего списка
            ViewBag.Masters = await _context.Masters
                .Where(m => m.IsActive)
                .Select(m => new {
                    m.Id,
                    FullName = m.LastName + " " + m.FirstName + " " + (m.MiddleName ?? "")
                })
                .ToListAsync();

            return View();
        }

        // POST: Schedule/CreateBlock
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBlock(int masterId, DateTime startDate, DateTime endDate, SlotStatus status, string reason)
        {
            // Проверяем, что даты корректны
            if (endDate < startDate)
            {
                ModelState.AddModelError("", "Дата окончания не может быть раньше даты начала");
                ViewBag.Masters = await _context.Masters
                    .Where(m => m.IsActive)
                    .Select(m => new { m.Id, FullName = m.LastName + " " + m.FirstName + " " + (m.MiddleName ?? "") })
                    .ToListAsync();
                return View();
            }

            var master = await _context.Masters.FindAsync(masterId);
            if (master == null)
            {
                return NotFound();
            }

            // Создаем слоты для каждого дня в диапазоне
            var currentDate = startDate.Date;
            var endDateTime = endDate.Date;

            while (currentDate <= endDateTime)
            {
                // Создаем слот на весь день (с 00:00 до 23:59)
                var slot = new ScheduleSlot
                {
                    MasterId = masterId,
                    StartTime = currentDate,
                    EndTime = currentDate.AddDays(1).AddTicks(-1),
                    Status = status,
                    AppointmentRequestId = null
                };

                // Проверяем, нет ли уже конфликтующих слотов
                var existingSlot = await _context.ScheduleSlots
                    .FirstOrDefaultAsync(s => s.MasterId == masterId &&
                        s.StartTime <= slot.EndTime &&
                        s.EndTime >= slot.StartTime);

                if (existingSlot == null)
                {
                    _context.ScheduleSlots.Add(slot);
                }

                currentDate = currentDate.AddDays(1);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // POST: Schedule/DeleteSlot/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSlot(int id)
        {
            var slot = await _context.ScheduleSlots.FindAsync(id);
            if (slot != null)
            {
                // Не даем удалить слоты, связанные с подтвержденными заявками
                if (slot.Status == SlotStatus.Booked)
                {
                    TempData["Error"] = "Нельзя удалить слот с подтвержденной записью";
                    return RedirectToAction(nameof(Index));
                }

                _context.ScheduleSlots.Remove(slot);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}