using System.ComponentModel.DataAnnotations;

namespace LisBlanc.AdminPanel.Models
{
    public class ScheduleSlot
    {
            [Key]
            public int Id { get; set; }

            // ID мастера
            [Required]
            public int MasterId { get; set; }

            // Начало занятого времени
            [Required]
            public DateTime StartTime { get; set; }

            // Конец занятого времени
            [Required]
            public DateTime EndTime { get; set; }

            // Статус слота (занят клиентом, мастер на больничном, выходной и т.д.)
            [Required]
            public SlotStatus Status { get; set; }

            // ID заявки, если слот занят клиентом (может быть пустым)
            public int? AppointmentRequestId { get; set; }

            // Навигационные свойства
            public virtual Master? Master { get; set; }
            public virtual AppointmentRequest? AppointmentRequest { get; set; }
    }

        public enum SlotStatus
        {
            [Display(Name = "Свободно")]
            Free,           // Слот свободен (но мы будем хранить только занятые слоты)
            [Display(Name = "Занято клиентом")]
            Booked,         // Занято клиентом
            [Display(Name = "Выходной")]
            DayOff,         // Мастер отдыхает
            [Display(Name = "Больничный")]
            SickLeave,      // Мастер болеет
            [Display(Name = "Отпуск")]
            Vacation        // Мастер в отпуске
        }
}

