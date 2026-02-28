using LisBlanc.AdminPanel.Models;
using Microsoft.EntityFrameworkCore;

namespace LisBlanc.AdminPanel.Data
{
    public class ApplicationDbContext : DbContext
    {
        // Конструктор, который принимает настройки подключения
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Здесь мы перечисляем все наши модели, которые должны стать таблицами в базе
        public DbSet<Master> Masters { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<AppointmentRequest> AppointmentRequests { get; set; }
        public DbSet<ScheduleSlot> ScheduleSlots { get; set; }
        public DbSet<User> Users { get; set; }

        // Здесь можно настроить связи между таблицами более точно
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Настройка связи: у одной заявки - один мастер
            modelBuilder.Entity<AppointmentRequest>()
                .HasOne(ar => ar.Master)
                .WithMany() // У мастера может быть много заявок
                .HasForeignKey(ar => ar.MasterId)
                .OnDelete(DeleteBehavior.Restrict); // Запрещаем удалять мастера, если на него есть заявки

            // Настройка связи: у одной заявки - одна услуга
            modelBuilder.Entity<AppointmentRequest>()
                .HasOne(ar => ar.Service)
                .WithMany() // У услуги может быть много заявок
                .HasForeignKey(ar => ar.ServiceId)
                .OnDelete(DeleteBehavior.Restrict); // Запрещаем удалять услугу, если на неё есть заявки

            // Настройка связи: у слота расписания - один мастер
            modelBuilder.Entity<ScheduleSlot>()
                .HasOne(ss => ss.Master)
                .WithMany() // У мастера может быть много слотов
                .HasForeignKey(ss => ss.MasterId)
                .OnDelete(DeleteBehavior.Restrict);

            // Настройка связи: слот может быть связан с заявкой (если занят клиентом)
            modelBuilder.Entity<ScheduleSlot>()
                .HasOne(ss => ss.AppointmentRequest)
                .WithOne() // У одной заявки - один слот
                .HasForeignKey<ScheduleSlot>(ss => ss.AppointmentRequestId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
