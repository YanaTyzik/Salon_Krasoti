using Microsoft.AspNetCore.Mvc;
using System.Xml.Serialization;
using System.Text;
using LisBlanc.AdminPanel.Models;
using LisBlanc.AdminPanel.Data;
using Microsoft.EntityFrameworkCore;

namespace LisBlanc.AdminPanel.Controllers
{
    [Route("api/xml")]
    [ApiController]
    public class XmlApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public XmlApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        // POST: api/xml/create-request
        // Принимает XML с заявкой, возвращает XML с результатом
        [HttpPost("create-request")]
        public async Task<IActionResult> CreateRequest()
        {
            try
            {
                // Читаем тело запроса как строку (там XML)
                string xmlString;
                using (StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8))
                {
                    xmlString = await reader.ReadToEndAsync();
                }

                // Превращаем XML в объект C#
                XmlSerializer serializer = new XmlSerializer(typeof(XmlAppointmentRequest));
                XmlAppointmentRequest xmlRequest;

                using (StringReader stringReader = new StringReader(xmlString))
                {
                    xmlRequest = (XmlAppointmentRequest)serializer.Deserialize(stringReader);
                }

                // Валидация: проверяем существование мастера и услуги
                var master = await _context.Masters.FindAsync(xmlRequest.MasterId);
                var service = await _context.Services.FindAsync(xmlRequest.ServiceId);

                if (master == null)
                {
                    return CreateErrorResponse("Мастер с указанным ID не найден");
                }

                if (service == null)
                {
                    return CreateErrorResponse("Услуга с указанным ID не найдена");
                }

                // Проверяем, активны ли мастер и услуга
                if (!master.IsActive)
                {
                    return CreateErrorResponse("Мастер временно не принимает записи");
                }

                // Проверяем, свободен ли мастер в это время
                bool isAvailable = await CheckAvailability(
                    xmlRequest.MasterId,
                    xmlRequest.RequestedDateTime,
                    service.DurationMinutes);

                if (!isAvailable)
                {
                    return CreateErrorResponse("Выбранное время уже занято");
                }

                // Создаём заявку
                var appointmentRequest = new AppointmentRequest
                {
                    MasterId = xmlRequest.MasterId,
                    ServiceId = xmlRequest.ServiceId,
                    ClientName = xmlRequest.ClientName,
                    ClientPhone = xmlRequest.ClientPhone,
                    Status = RequestStatus.Confirmed,
                    CreatedAt = DateTime.Now
                };

                _context.AppointmentRequests.Add(appointmentRequest);
                await _context.SaveChangesAsync();

                // Формируем успешный XML-ответ
                var successResponse = new CreateRequestSuccessResponse
                {
                    Status = "success",
                    Message = "Заявка успешно создана и ожидает подтверждения администратора",
                    RequestId = appointmentRequest.Id,
                    Timestamp = DateTime.Now
                };

                return Ok(successResponse);
            }
            catch (InvalidOperationException ex)
            {
                // Ошибка десериализации XML
                return CreateErrorResponse("Неверный формат XML: " + ex.Message);
            }
            catch (Exception ex)
            {
                return CreateErrorResponse("Внутренняя ошибка сервера: " + ex.Message);
            }
        }

        // GET: api/xml/request-status/{id}
        // Возвращает статус заявки по ID
        [HttpGet("request-status/{id}")]
        public async Task<IActionResult> GetRequestStatus(int id)
        {
            var request = await _context.AppointmentRequests
                .Include(r => r.Master)
                .Include(r => r.Service)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
            {
                return CreateErrorResponse("Заявка с указанным ID не найдена");
            }

            var response = new RequestStatusResponse
            {
                Id = request.Id,
                Status = request.Status.ToString(),
                ClientName = request.ClientName,
                MasterName = request.Master?.FullName ?? "Не указан",
                ServiceName = request.Service?.Name ?? "Не указана"
            };

            return Ok(response);
        }

        // GET: api/xml/available-slots
        // Возвращает свободные слоты для мастера на дату
        [HttpGet("available-slots")]
        public async Task<IActionResult> GetAvailableSlots(int masterId, DateTime date)
        {
            var master = await _context.Masters.FindAsync(masterId);
            if (master == null)
            {
                return CreateErrorResponse("Мастер не найден");
            }

            // Начало и конец выбранного дня
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

            // Получаем все занятые слоты мастера на эту дату
            var bookedSlots = await _context.ScheduleSlots
                .Where(s => s.MasterId == masterId &&
                            s.StartTime >= startOfDay &&
                            s.StartTime <= endOfDay)
                .ToListAsync();

            // Генерируем слоты с 9 до 21 с шагом 30 минут
            var slots = new List<TimeSlotInfo>();
            for (int hour = 9; hour < 21; hour++)
            {
                for (int minute = 0; minute < 60; minute += 30)
                {
                    var slotTime = startOfDay.AddHours(hour).AddMinutes(minute);
                    var slotEnd = slotTime.AddMinutes(30);

                    // Проверяем, не занят ли слот
                    bool isBooked = bookedSlots.Any(s =>
                        s.StartTime <= slotEnd &&
                        s.EndTime > slotTime);

                    slots.Add(new TimeSlotInfo
                    {
                        StartTime = slotTime,
                        EndTime = slotEnd,
                        Available = !isBooked
                    });
                }
            }


            var response = new AvailableSlotsResponse
            {
                MasterId = masterId,
                Date = date,
                Slots = slots
            };

            return Ok(response);
        }


        // Вспомогательный метод для проверки доступности
        private async Task<bool> CheckAvailability(int masterId, DateTime startTime, int durationMinutes)
        {
            DateTime endTime = startTime.AddMinutes(durationMinutes);

            var conflictingSlots = await _context.ScheduleSlots
                .Where(s => s.MasterId == masterId &&
                            s.Status == SlotStatus.Booked &&
                            ((s.StartTime <= startTime && s.EndTime > startTime) ||
                             (s.StartTime < endTime && s.EndTime >= endTime) ||
                             (s.StartTime >= startTime && s.EndTime <= endTime)))
                .AnyAsync();

            return !conflictingSlots;
        }

        // Вспомогательный метод для создания XML-ответа с ошибкой
        private IActionResult CreateErrorResponse(string message)
        {
            var errorResponse = new ErrorResponse
            {
                Status = "error",
                Message = message,
                Timestamp = DateTime.Now
            };

            // Возвращаем ошибку с кодом 400 (Bad Request) или 500
            return BadRequest(errorResponse);
        }
    }
}