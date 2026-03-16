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

        // POST: /Client/Appointment/Step1
        [HttpPost]
            public async Task<IActionResult> Step1(int serviceId)
            {
                // Запоминаем выбранную услугу в сессии или временных данных
                TempData["ServiceId"] = serviceId;

                // Перенаправляем на шаг выбора мастера
                return RedirectToAction("Step2");
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

            // POST: /Client/Appointment/Step2
            [HttpPost]
            public IActionResult Step2(int masterId)
            {
                TempData["MasterId"] = masterId;
                return RedirectToAction("Step3");
            }

            // GET: /Client/Appointment/Step3
            // Шаг 3: Выбор времени
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

                // Выбранная дата (если не выбрана - сегодня)
                DateTime selectedDate = date ?? DateTime.Today;

                // Получаем услугу, чтобы узнать длительность
                var service = await _context.Services.FindAsync(serviceId);

                // Получаем свободные слоты на выбранную дату
                var availableSlots = await GetAvailableSlots(masterId, serviceId, selectedDate);

                ViewBag.MasterId = masterId;
                ViewBag.Service = service;
                ViewBag.SelectedDate = selectedDate;
                ViewBag.AvailableSlots = availableSlots;

                return View();
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

            // Вспомогательный метод для получения свободных слотов
            private async Task<List<object>> GetAvailableSlots(int masterId, int serviceId, DateTime date)
            {
                var service = await _context.Services.FindAsync(serviceId);
                if (service == null) return new List<object>();

                var startOfDay = date.Date;
                var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

                // Занятые слоты
                var busySlots = await _context.ScheduleSlots
                    .Where(s => s.MasterId == masterId &&
                                s.StartTime >= startOfDay &&
                                s.StartTime <= endOfDay &&
                                s.Status == SlotStatus.Booked)
                    .ToListAsync();

                // Выходные/больничные
                var dayOffSlots = await _context.ScheduleSlots
                    .Where(s => s.MasterId == masterId &&
                                s.StartTime >= startOfDay &&
                                s.StartTime <= endOfDay &&
                                (s.Status == SlotStatus.DayOff ||
                                 s.Status == SlotStatus.SickLeave ||
                                 s.Status == SlotStatus.Vacation))
                    .ToListAsync();

                // Если есть слот на весь день - нет свободного времени
                if (dayOffSlots.Any(s => s.StartTime <= startOfDay && s.EndTime >= endOfDay))
                {
                    return new List<object>();
                }

                // Генерируем слоты с 9 до 21 с шагом, равным длительности услуги
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
        }
}
