namespace LisBlanc.AdminPanel.Models
{
    public class RevenueViewModel
    {
        // Фильтры
        public int? SelectedMasterId { get; set; }
        public string SelectedPeriod { get; set; } = "month"; // day, month, halfyear, year, all

        // Данные для фильтров
        public List<Master> Masters { get; set; } = new();

        // Результаты
        public List<RevenueItem> RevenueItems { get; set; } = new();
        public decimal TotalRevenue { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        // Название выбранного мастера
        public string MasterName { get; set; } = "Все мастера";
    }

    public class RevenueItem
    {
        public int Id { get; set; }
        public string ClientName { get; set; }
        public string ClientPhone { get; set; }
        public string MasterName { get; set; }
        public string ServiceName { get; set; }
        public decimal Price { get; set; }
        public DateTime AppointmentDate { get; set; }
        public string Status { get; set; }
    }
}

