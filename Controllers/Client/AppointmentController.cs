using global::LisBlanc.AdminPanel.Data;
using global::LisBlanc.AdminPanel.Models;
using LisBlanc.AdminPanel.Data;
using LisBlanc.AdminPanel.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LisBlanc.AdminPanel.Controllers.Client
{
        public class AppointmentController : Controller
        {
            private readonly ApplicationDbContext _context;

            public AppointmentController(ApplicationDbContext context)
            {
                _context = context;
            }

        // GET: /Client/Appointment/Step1
        // Шаг 1: Выбор услуги
        public async Task<IActionResult> Step1()
        {
            var services = await _context.Services.ToListAsync();
            return View("/Views/Client/Appointment/Step1.cshtml", services);
        }

        [HttpPost]
        public async Task<IActionResult> Step1(int serviceId)
        {
            TempData["ServiceId"] = serviceId;

            // Получаем выбранную услугу
            var selectedService = await _context.Services.FindAsync(serviceId);

            if (selectedService == null)
            {
                return RedirectToAction("Step1");
            }

            // Получаем всех активных мастеров с подходящей специализацией
            var allMasters = await _context.Masters
                .Where(m => m.IsActive &&
                            m.Specialization == selectedService.Category)
                .ToListAsync();

            // Фильтруем: оставляем только тех, у кого есть свободные слоты
            var availableMasters = new List<Master>();

            foreach (var master in allMasters)
            {
                if (await MasterHasAvailableSlots(master.Id, serviceId))
                {
                    availableMasters.Add(master);
                }
            }

            // Если есть мастера со слотами — показываем их
            if (availableMasters.Any())
            {
                return View("~/Views/Client/Appointment/Step2.cshtml", availableMasters);
            }

            // Если нет ни одного — показываем всех с предупреждением
            ViewBag.Warning = "На ближайшие дни нет свободных слотов. Показаны все мастера, но записаться можно только на те дни, где есть свободное время.";
            return View("~/Views/Client/Appointment/Step2.cshtml", allMasters);
        }

        // GET: /Client/Appointment/ChooseCategory
        public async Task<IActionResult> ChooseCategory()
        {
            // Получаем все уникальные категории из услуг
            var categories = await _context.Services
                .Select(s => s.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
            return View(categories);
        }

        // POST: /Client/Appointment/ChooseCategory
        [HttpPost]
        public IActionResult ChooseCategory(string category)
        {
            TempData["SelectedCategory"] = category;
            return RedirectToAction("Step1");
        }

        // GET: /Client/Appointment/Step2
        // Шаг 2: Выбор мастера
        public async Task<IActionResult> Step2()
            {
                if (!TempData.ContainsKey("ServiceId"))
                {
                    return RedirectToAction("Step1");
                }

                int serviceId = (int)TempData["ServiceId"];
                TempData.Keep("ServiceId"); // Сохраняем для следующего шага

                // Получаем всех активных мастеров
                var masters = await _context.Masters
                    .Where(m => m.IsActive)
                    .ToListAsync();

                ViewBag.ServiceId = serviceId;
                return View(masters);
            }

        [HttpPost]
        public IActionResult Step2(int masterId)
        {
            TempData["MasterId"] = masterId;

            // Временные данные для Step3
            int serviceId = (int)TempData["ServiceId"];
            TempData.Keep("ServiceId");

            // Сразу возвращаем Step3 с нужными данными
            return RedirectToAction("Step3");
        }

        // GET: /Client/Appointment/Step3
        public async Task<IActionResult> Step3(DateTime? date)
        {
            if (!TempData.ContainsKey("ServiceId") || !TempData.ContainsKey("MasterId"))
            {
                return RedirectToAction("Step1");
            }

            int serviceId = (int)TempData["ServiceId"];
            int masterId = (int)TempData["MasterId"];

            TempData.Keep("ServiceId");
            TempData.Keep("MasterId");

            DateTime selectedDate = date ?? DateTime.Today;

            var service = await _context.Services.FindAsync(serviceId);

            // ИСПРАВЛЕНО: вызываем правильный метод
            var (availableSlots, blockMessage) = await GetAvailableSlotsWithMessage(masterId, serviceId, selectedDate);

            ViewBag.MasterId = masterId;
            ViewBag.Service = service;
            ViewBag.SelectedDate = selectedDate;
            ViewBag.AvailableSlots = availableSlots;
            ViewBag.BlockMessage = blockMessage;

            return View("~/Views/Client/Appointment/Step3.cshtml");
        }
        // POST: /Client/Appointment/Step3
        [HttpPost]
            public IActionResult Step3(DateTime selectedDateTime)
            {
                TempData["SelectedDateTime"] = selectedDateTime.ToString("yyyy-MM-dd HH:mm:ss");
                return RedirectToAction("Step4");
            }

            // GET: /Client/Appointment/Step4
            // Шаг 4: Ввод контактных данных
            public IActionResult Step4()
            {
                if (!TempData.ContainsKey("ServiceId") ||
                    !TempData.ContainsKey("MasterId") ||
                    !TempData.ContainsKey("SelectedDateTime"))
                {
                    return RedirectToAction("Step1");
                }

                var model = new CreateAppointmentRequestViewModel
                {
                    MasterId = (int)TempData["MasterId"],
                    ServiceId = (int)TempData["ServiceId"],
                    SelectedDateTime = DateTime.Parse(TempData["SelectedDateTime"].ToString())
                };

                return View(model);
            }

            // POST: /Client/Appointment/Create
            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> Create(CreateAppointmentRequestViewModel model)
            {
                if (ModelState.IsValid)
                {
                    // Проверяем, свободно ли ещё это время
                    bool isAvailable = await CheckAvailability(
                        model.MasterId,
                        model.ServiceId,
                        model.SelectedDateTime);

                    if (!isAvailable)
                    {
                        ModelState.AddModelError("", "К сожалению, это время уже занято. Пожалуйста, выберите другое.");
                        return View("Step4", model);
                    }

                    // Создаём заявку
                    var appointmentRequest = new AppointmentRequest
                    {
                        MasterId = model.MasterId,
                        ServiceId = model.ServiceId,
                        ClientName = model.ClientName,
                        ClientPhone = model.ClientPhone,
                        CreatedAt = model.SelectedDateTime,
                        Status = RequestStatus.Confirmed // Сразу подтверждаем
                    };

                    _context.AppointmentRequests.Add(appointmentRequest);
                    await _context.SaveChangesAsync();

                    // Создаём слот в расписании
                    var service = await _context.Services.FindAsync(model.ServiceId);
                    var slot = new ScheduleSlot
                    {
                        MasterId = model.MasterId,
                        StartTime = model.SelectedDateTime,
                        EndTime = model.SelectedDateTime.AddMinutes(service.DurationMinutes),
                        Status = SlotStatus.Booked,
                        AppointmentRequestId = appointmentRequest.Id
                    };

                    _context.ScheduleSlots.Add(slot);
                    await _context.SaveChangesAsync();

                    // Если пользователь авторизован, связываем заявку с ним
                    if (User.Identity.IsAuthenticated && User.IsInRole("Client"))
                    {
                        // TODO: Связать заявку с пользователем (если нужно)
                    }

                    TempData["SuccessMessage"] = "Вы успешно записаны!";
                    return RedirectToAction("Success");
                }

                return View("Step4", model);
            }

            // GET: /Client/Appointment/Success
            public IActionResult Success()
            {
                return View();
            }

        private async Task<List<object>> GetAvailableSlotsInternal(int masterId, int serviceId, DateTime date)
        {
            var service = await _context.Services.FindAsync(serviceId);
            if (service == null) return new List<object>();

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

            // Проверяем, есть ли блокировка на весь день
            var fullDayOff = dayOffSlots.FirstOrDefault(s => s.StartTime <= startOfDay && s.EndTime >= endOfDay);
            if (fullDayOff != null)
            {
                // Определяем тип блокировки для сообщения
                string message = fullDayOff.Status switch
                {
                    SlotStatus.DayOff => "У мастера выходной в этот день",
                    SlotStatus.SickLeave => "Мастер на больничном в этот день",
                    SlotStatus.Vacation => "Мастер в отпуске в этот день",
                    _ => "Мастер не работает в этот день"
                };

                // Здесь можно сохранить сообщение, но метод возвращает List<object>
                // Поэтому просто возвращаем пустой список
                return new List<object>();
            }

            // Генерируем все возможные слоты с 9:00 до 21:00 с шагом 30 минут
            var availableSlots = new List<object>();
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
                    availableSlots.Add(new
                    {
                        time = currentTime.ToString("HH:mm"),
                        value = currentTime.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                }

                currentTime = currentTime.AddMinutes(30);
            }

            return availableSlots;
        }

        // Проверка доступности слота
        private async Task<bool> CheckAvailability(int masterId, int serviceId, DateTime startTime)
            {
                var service = await _context.Services.FindAsync(serviceId);
                if (service == null) return false;

                DateTime endTime = startTime.AddMinutes(service.DurationMinutes);

                var conflictingSlots = await _context.ScheduleSlots
                    .Where(s => s.MasterId == masterId &&
                                s.Status == SlotStatus.Booked &&
                                ((s.StartTime <= startTime && s.EndTime > startTime) ||
                                 (s.StartTime < endTime && s.EndTime >= endTime) ||
                                 (s.StartTime >= startTime && s.EndTime <= endTime)))
                    .AnyAsync();

                return !conflictingSlots;
            }

        private async Task<(List<object> slots, string message)> GetAvailableSlotsWithMessage(int masterId, int serviceId, DateTime date)
        {
            var service = await _context.Services.FindAsync(serviceId);
            if (service == null) return (new List<object>(), null);

            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

            var dayOffSlots = await _context.ScheduleSlots
                .Where(s => s.MasterId == masterId
                    && s.StartTime >= startOfDay
                    && s.StartTime <= endOfDay
                    && (s.Status == SlotStatus.DayOff
                        || s.Status == SlotStatus.SickLeave
                        || s.Status == SlotStatus.Vacation))
                .ToListAsync();

            var fullDayOff = dayOffSlots.FirstOrDefault(s => s.StartTime <= startOfDay && s.EndTime >= endOfDay);
            if (fullDayOff != null)
            {
                string message = fullDayOff.Status switch
                {
                    SlotStatus.DayOff => "У мастера выходной в этот день",
                    SlotStatus.SickLeave => "Мастер на больничном в этот день",
                    SlotStatus.Vacation => "Мастер в отпуске в этот день",
                    _ => "Мастер не работает в этот день"
                };
                return (new List<object>(), message);
            }

            var slots = await GetAvailableSlotsInternal(masterId, serviceId, date);
            return (slots, null);


        }

        public async Task<IActionResult> GetAvailableSlots(int masterId, int serviceId, DateTime date)
        {
            var (slots, _) = await GetAvailableSlotsWithMessage(masterId, serviceId, date);
            return Json(slots);
        }

        private async Task<bool> MasterHasAvailableSlots(int masterId, int serviceId, int daysAhead = 7)
        {
            var service = await _context.Services.FindAsync(serviceId);
            if (service == null) return false;

            var startDate = DateTime.Today;
            var endDate = startDate.AddDays(daysAhead);

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var (slots, _) = await GetAvailableSlotsWithMessage(masterId, serviceId, date);
                if (slots.Any())
                {
                    return true; // Есть хотя бы один свободный слот
                }
            }

            return false; // Нет свободных слотов
        }
    }
}
