using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace LisBlanc.AdminPanel.Models
{
    public class Master
    {
        [Key]
        public int Id { get; set; }

        // Имя мастера (обязательное поле)
        [Required(ErrorMessage = "Имя обязательно для заполнения")]
        [Display(Name = "Имя")]
        public string FirstName { get; set; }

        // Фамилия мастера (обязательное поле)
        [Required(ErrorMessage = "Фамилия обязательна для заполнения")]
        [Display(Name = "Фамилия")]
        public string LastName { get; set; }

        // Отчество (необязательное)
        [Display(Name = "Отчество")]
        public string? MiddleName { get; set; }

        // Специализация (например, "Парикмахер", "Косметолог")
        [Required(ErrorMessage = "Специализация обязательна")]
        [Display(Name = "Специализация")]
        public string Specialization { get; set; }

        // Контактный телефон
        [Required(ErrorMessage = "Телефон обязателен")]
        [Display(Name = "Телефон")]
        [Phone(ErrorMessage = "Введите корректный номер телефона")]
        public string Phone { get; set; }

        // Активен ли мастер? (true - работает, false - уволен/в архиве)
        // По умолчанию новый мастер активен
        [Display(Name = "Активен")]
        public bool IsActive { get; set; } = true;

        // Это для удобства: полное имя (Фамилия Имя Отчество)
        // [NotMapped] значит, что это поле не будет сохраняться в базу данных
        [NotMapped]
        public string FullName => $"{LastName} {FirstName} {MiddleName}".Trim();
    }
}
