using System.ComponentModel.DataAnnotations;

namespace LisBlanc.AdminPanel.Models
{
    public class Service
    {
        [Key]
        public int Id { get; set; }

        // Название услуги (например, "Женская стрижка")
        [Required(ErrorMessage = "Название услуги обязательно")]
        [Display(Name = "Название услуги")]
        public string Name { get; set; }

        // Описание (подробности об услуге)
        [Display(Name = "Описание")]
        public string? Description { get; set; }

        // Стоимость услуги в рублях
        [Required(ErrorMessage = "Укажите стоимость")]
        [Display(Name = "Стоимость")]
        [Range(0, 100000, ErrorMessage = "Стоимость должна быть от 0 до 100 000 руб.")]
        public decimal Price { get; set; }

        // Длительность услуги в минутах
        [Required(ErrorMessage = "Укажите длительность")]
        [Display(Name = "Длительность (минут)")]
        [Range(5, 480, ErrorMessage = "Длительность должна быть от 5 до 480 минут")]
        public int DurationMinutes { get; set; }

        // Категория (например, "Парикмахерские услуги", "Косметология")
        [Required(ErrorMessage = "Выберите категорию")]
        [Display(Name = "Категория")]
        public string Category { get; set; }
    }
}