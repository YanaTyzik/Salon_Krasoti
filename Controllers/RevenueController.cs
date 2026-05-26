using Microsoft.AspNetCore.Mvc;
using LisBlanc.AdminPanel.Data;
using LisBlanc.AdminPanel.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LisBlanc.AdminPanel.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class RevenueController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RevenueController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int? masterId, string period = "month")
        {
            var viewModel = new RevenueViewModel
            {
                SelectedMasterId = masterId,
                SelectedPeriod = period,
                Masters = await _context.Masters.Where(m => m.IsActive).ToListAsync()
            };

            // Определяем даты периода
            var (startDate, endDate) = GetDateRange(period);
            viewModel.StartDate = startDate;
            viewModel.EndDate = endDate;

            // Строим запрос
            var query = _context.AppointmentRequests
                .Include(a => a.Master)
                .Include(a => a.Service)
                .Where(a => a.Status == RequestStatus.Confirmed)
                .Where(a => a.AppointmentDate >= startDate && a.AppointmentDate <= endDate);

            // Фильтр по мастеру
            if (masterId.HasValue && masterId.Value > 0)
            {
                query = query.Where(a => a.MasterId == masterId.Value);
                var master = await _context.Masters.FindAsync(masterId.Value);
                viewModel.MasterName = master?.FullName ?? "Мастер";
            }
            else
            {
                viewModel.MasterName = "Все мастера";
            }

            // Получаем данные
            var appointments = await query
                .OrderByDescending(a => a.AppointmentDate)
                .ToListAsync();

            // Преобразуем в RevenueItem
            viewModel.RevenueItems = appointments.Select(a => new RevenueItem
            {
                Id = a.Id,
                ClientName = a.ClientName,
                ClientPhone = a.ClientPhone,
                MasterName = a.Master?.FullName ?? "—",
                ServiceName = a.Service?.Name ?? "—",
                Price = a.Service?.Price ?? 0,
                AppointmentDate = a.AppointmentDate ?? a.CreatedAt,
                Status = a.Status.ToString()
            }).ToList();

            viewModel.TotalRevenue = viewModel.RevenueItems.Sum(i => i.Price);

            return View(viewModel);
        }

        // POST: /Revenue/ExportToWord
        [HttpPost]
        public async Task<IActionResult> ExportToWord(int? masterId, string period = "month")
        {
            // Получаем те же данные, что и в Index
            var (startDate, endDate) = GetDateRange(period);

            var query = _context.AppointmentRequests
                .Include(a => a.Master)
                .Include(a => a.Service)
                .Where(a => a.Status == RequestStatus.Confirmed)
                .Where(a => a.AppointmentDate >= startDate && a.AppointmentDate <= endDate);

            if (masterId.HasValue && masterId.Value > 0)
            {
                query = query.Where(a => a.MasterId == masterId.Value);
            }

            var appointments = await query
                .OrderByDescending(a => a.AppointmentDate)
                .ToListAsync();

            var revenueItems = appointments.Select(a => new RevenueItem
            {
                Id = a.Id,
                ClientName = a.ClientName,
                ClientPhone = a.ClientPhone,
                MasterName = a.Master?.FullName ?? "—",
                ServiceName = a.Service?.Name ?? "—",
                Price = a.Service?.Price ?? 0,
                AppointmentDate = a.AppointmentDate ?? a.CreatedAt,
                Status = a.Status.ToString()
            }).ToList();

            var totalRevenue = revenueItems.Sum(i => i.Price);

            // Формируем HTML для Word
            string masterName = "всех мастеров";
            if (masterId.HasValue && masterId.Value > 0)
            {
                var master = await _context.Masters.FindAsync(masterId.Value);
                masterName = master?.FullName ?? "мастера";
            }

            string periodText = GetPeriodText(period, startDate, endDate);

            var html = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Отчет по выручке</title>
    <style>
        body {{ 
            font-family: 'Calibri', sans-serif; 
            margin: 20px 10px 20px 10px;
        }}
        h1 {{ 
            color: #2d1b4e; 
            border-bottom: 2px solid #b47aff; 
            padding-bottom: 10px;
            margin-bottom: 20px;
        }}
        h2 {{ 
            color: #8a5cd9; 
            margin-top: 20px;
            margin-bottom: 15px;
            font-size: 18px;
        }}
        .info {{ 
            margin: 15px 0; 
            padding: 10px 15px; 
            background: #f3e8ff; 
            border-radius: 8px;
            font-size: 14px;
        }}
        table {{ 
            width: 100%; 
            border-collapse: collapse; 
            margin-top: 15px;
            font-size: 13px;
        }}
        th, td {{ 
            border: 1px solid #ddd; 
            padding: 8px 10px; 
            text-align: left; 
        }}
        th {{ 
            background: linear-gradient(135deg, #b47aff, #8a5cd9); 
            color: white; 
            font-weight: bold;
        }}
        tr:nth-child(even) {{ 
            background: #f9f5ff; 
        }}
        .total {{ 
            font-size: 16px; 
            font-weight: bold; 
            margin-top: 20px; 
            text-align: right;
            padding-right: 15px;
        }}
        .total-amount {{ 
            color: #b47aff; 
            font-size: 22px; 
        }}
        .footer {{ 
            margin-top: 30px; 
            text-align: center; 
            color: #999; 
            font-size: 11px;
            border-top: 1px solid #eee;
            padding-top: 15px;
        }}
    </style>
</head>
<body>
    <h1>📊 Отчет по выручке</h1>
    
    <div class='info'>
        <strong>👨‍🎨 Мастер:</strong> {masterName}<br/>
        <strong>📅 Период:</strong> {periodText}<br/>
        <strong>🕒 Дата формирования:</strong> {DateTime.Now:dd.MM.yyyy HH:mm}
    </div>

    <h2>✅ Выполненные записи</h2>
    
    <table>
        <thead>
            <tr>
                <th>№</th>
                <th>Клиент</th>
                <th>Телефон</th>
                <th>Услуга</th>
                <th>Дата записи</th>
                <th>Стоимость</th>
            </tr>
        </thead>
        <tbody>";

            int counter = 1;
            foreach (var item in revenueItems)
            {
                html += $@"
            <tr>
                <td style='text-align:center'>{counter++}</td>
                <td>{item.ClientName}</td>
                <td>{item.ClientPhone}</td>
                <td>{item.ServiceName}</td>
                <td>{item.AppointmentDate:dd.MM.yyyy HH:mm}</td>
                <td style='text-align:right'>{item.Price:N0} ₽</td>
            </tr>";
            }

            if (!revenueItems.Any())
            {
                html += "<tr><td colspan='6' style='text-align:center'>❌ Нет записей за выбранный период</td></tr>";
            }

            html += $@"
        </tbody>
        <tfoot>
            <tr style='background:#f3e8ff; font-weight:bold'>
                <td colspan='5' style='text-align:right'>ИТОГО:</td>
                <td style='text-align:right; color:#b47aff'>{totalRevenue:N0} ₽</td>
            </tr>
        </tfoot>
    </table>

    <div class='total'>
        <strong>Общая выручка:</strong> <span class='total-amount'>{totalRevenue:N0} ₽</span>
    </div>

    <div class='footer'>
        Отчет сгенерирован автоматически в системе Lis Blanc<br/>
        * Учтены только подтвержденные записи
    </div>
</body>
</html>";

            // Возвращаем как Word документ
            return Content(html, "application/msword");
        }

        private (DateTime startDate, DateTime endDate) GetDateRange(string period)
        {
            DateTime startDate;
            DateTime endDate = DateTime.Today;

            switch (period)
            {
                case "day":
                    startDate = DateTime.Today;
                    endDate = DateTime.Today.AddDays(1).AddTicks(-1);
                    break;
                case "month":
                    startDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                    endDate = startDate.AddMonths(1).AddDays(-1);
                    break;
                case "halfyear":
                    startDate = DateTime.Today.AddMonths(-6);
                    break;
                case "year":
                    startDate = new DateTime(DateTime.Today.Year, 1, 1);
                    endDate = new DateTime(DateTime.Today.Year, 12, 31);
                    break;
                case "all":
                    startDate = new DateTime(2000, 1, 1);
                    break;
                default:
                    startDate = DateTime.Today.AddMonths(-1);
                    break;
            }

            return (startDate, endDate);
        }

        private string GetPeriodText(string period, DateTime startDate, DateTime endDate)
        {
            return period switch
            {
                "day" => $"день {startDate:dd.MM.yyyy}",
                "month" => $"месяц {startDate:MMMM yyyy}",
                "halfyear" => $"последние 6 месяцев ({startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy})",
                "year" => $"год {startDate:yyyy}",
                "all" => $"всё время (с {startDate:dd.MM.yyyy})",
                _ => $"{startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}"
            };
        }
    }
}
