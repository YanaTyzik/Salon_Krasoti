using System.ComponentModel.DataAnnotations;

namespace LisBlanc.AdminPanel.Models
{
    public class CreateAppointmentRequestViewModel
    {
        [Required]
        [Display(Name = "Мастер")]
        public int MasterId { get; set; }

        [Required]
        [Display(Name = "Услуга")]
        public int ServiceId { get; set; }

        [Required]
        [Display(Name = "Имя клиента")]
        public string ClientName { get; set; }

        [Required]
        [Display(Name = "Телефон клиента")]
        public string ClientPhone { get; set; }

        [Required]
        [Display(Name = "Желаемая дата и время")]
        public DateTime SelectedDateTime { get; set; }

        // Для хранения доступных слотов
        public List<AvailableSlot> AvailableSlots { get; set; } = new();
    }

    public class AvailableSlot
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string DisplayTime { get; set; }
    }
}