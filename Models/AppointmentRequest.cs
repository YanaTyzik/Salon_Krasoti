using System.ComponentModel.DataAnnotations;

namespace LisBlanc.AdminPanel.Models
{
    public class AppointmentRequest
    {
        [Key]
        public int Id { get; set; }

        // ID мастера, к которому хотят записаться
        [Required]
        public int MasterId { get; set; }

        // ID услуги, которую хотят
        [Required]
        public int ServiceId { get; set; }

        // Имя клиента
        [Required]
        [Display(Name = "Имя клиента")]
        public string ClientName { get; set; }

        // Телефон клиента
        [Required]
        [Display(Name = "Телефон клиента")]
        public string ClientPhone { get; set; }

        // Когда поступила заявка
        [Display(Name = "Дата заявки")]
        public DateTime CreatedAt { get; set; } // Убрали = DateTime.Now

        // Статус заявки (подтверждена, отклонена)
        [Display(Name = "Статус")]
        public RequestStatus Status { get; set; } = RequestStatus.Confirmed;

        // Если отклонили, то причина (можно не указывать)
        [Display(Name = "Причина отказа")]
        public string? RejectionReason { get; set; }

        // Навигационные свойства (для связей с другими таблицами)
        public virtual Master? Master { get; set; }
        public virtual Service? Service { get; set; }
    }

    // Перечисление возможных статусов заявки
    public enum RequestStatus
    {
        [Display(Name = "Новая")]
        New,
        [Display(Name = "Подтверждена")]
        Confirmed,
        [Display(Name = "Отклонена")]
        Rejected
    }
}