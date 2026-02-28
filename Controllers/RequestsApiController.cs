using LisBlanc.AdminPanel.Data;
using LisBlanc.AdminPanel.Models;
using System.Text;
using System.Xml.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LisBlanc.AdminPanel.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RequestsApiController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RequestsApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        // POST: api/RequestsApi
        // Этот метод принимает XML с заявкой
        [HttpPost]
        public async Task<IActionResult> PostRequest()
        {
            try
            {
                // Читаем тело запроса (там наш XML)
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

                // Проверяем, есть ли такой мастер и услуга в базе
                var master = await _context.Masters.FindAsync(xmlRequest.MasterId);
                var service = await _context.Services.FindAsync(xmlRequest.ServiceId);

                if (master == null || service == null)
                {
                    return BadRequest("Мастер или услуга не найдены");
                }

                // Проверяем, свободен ли мастер в это время
                bool isSlotAvailable = await CheckSlotAvailability(
                    xmlRequest.MasterId,
                    xmlRequest.RequestedDateTime,
                    service.DurationMinutes
                );
                if (!isSlotAvailable)
                {
                    // Если время занято, возвращаем ошибку
                    return Conflict("Выбранное время уже занято");
                }

                // Создаем новую заявку в нашей базе
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

                // Возвращаем успешный ответ (можно тоже в XML, но для простоты вернем JSON)
                return Ok(new
                {
                    message = "Заявка успешно создана и ожидает подтверждения",
                    requestId = appointmentRequest.Id
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Внутренняя ошибка сервера: {ex.Message}");
            }
        }

        // Вспомогательный метод для проверки свободного времени
        private async Task<bool> CheckSlotAvailability(int masterId, DateTime startTime, int durationMinutes)
        {
            DateTime endTime = startTime.AddMinutes(durationMinutes);

            // Проверяем, есть ли уже занятые слоты у этого мастера на это время
            var conflictingSlots = await _context.ScheduleSlots
                .Where(s => s.MasterId == masterId &&
                            s.Status == SlotStatus.Booked &&
                            ((s.StartTime <= startTime && s.EndTime > startTime) ||
                             (s.StartTime < endTime && s.EndTime >= endTime) ||
                             (s.StartTime >= startTime && s.EndTime <= endTime)))
                .AnyAsync();

            return !conflictingSlots; // Если конфликтов нет - слот свободен
        }
    }
}


            

