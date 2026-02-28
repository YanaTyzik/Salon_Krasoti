using System;
using System.Collections.Generic;

namespace LisBlanc.AdminPanel.Models
{
    public class ScheduleViewModel
    {
        // Выбранная дата
        public DateTime SelectedDate { get; set; }

        // Список всех активных мастеров
        public List<Master> Masters { get; set; }

        // Слоты расписания на выбранную дату
        public List<ScheduleSlot> Slots { get; set; }

        // Для удобства — часы работы (с 9 до 21)
        public List<string> TimeSlots { get; set; }
    }
}
