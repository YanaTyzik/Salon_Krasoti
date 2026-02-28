using System.ComponentModel.DataAnnotations;

namespace LisBlanc.AdminPanel.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Логин")]
        public string Username { get; set; }

        [Required]
        [Display(Name = "Пароль")]
        public string PasswordHash { get; set; } // Здесь будем хранить ХЕШ пароля, а не сам пароль!

        [Display(Name = "Email")]
        public string? Email { get; set; }

        [Display(Name = "Роль")]
        public string Role { get; set; } = "Admin"; // Пока только админы
    }
}
