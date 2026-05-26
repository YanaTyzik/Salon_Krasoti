using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LisBlanc.AdminPanel.Data;
using LisBlanc.AdminPanel.Models;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.IO;

namespace LisBlanc.AdminPanel.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class ScheduleController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ScheduleController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Schedule
        public async Task<IActionResult> Index(DateTime? date)
        {
            // Если дата не выбрана, показываем сегодня
            DateTime selectedDate = date ?? DateTime.Today;

            // Получаем всех активных мастеров
            var masters = await _context.Masters
                .Where(m => m.IsActive)
                .ToListAsync();

            // Получаем слоты на выбранную дату (с 00:00 до 23:59)
            var startOfDay = selectedDate.Date;
            var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

            // Загружаем слоты с мастерами и заявками (а через заявки - услуги)
            var slots = await _context.ScheduleSlots
                .Include(s => s.Master)
                .Include(s => s.AppointmentRequest)
                    .ThenInclude(ar => ar.Service)
                .Where(s => s.StartTime >= startOfDay && s.StartTime <= endOfDay)
                .ToListAsync();

            // Создаем список временных слотов (с 9:00 до 21:00 с шагом 30 минут)
            var timeSlots = new List<string>();
            for (int hour = 9; hour <= 21; hour++)
            {
                timeSlots.Add($"{hour:D2}:00");
                if (hour < 21)
                {
                    timeSlots.Add($"{hour:D2}:30");
                }
            }

            var viewModel = new ScheduleViewModel
            {
                SelectedDate = selectedDate,
                Masters = masters,
                Slots = slots,
                TimeSlots = timeSlots
            };

            return View(viewModel);
        }


        // GET: Schedule/CreateBlock
        public async Task<IActionResult> CreateBlock()
        {
            // Получаем список активных мастеров для выпадающего списка
            ViewBag.Masters = await _context.Masters
                .Where(m => m.IsActive)
                .Select(m => new {
                    m.Id,
                    FullName = m.LastName + " " + m.FirstName + " " + (m.MiddleName ?? "")
                })
                .ToListAsync();

            return View();
        }

        // POST: Schedule/CreateBlock
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBlock(int masterId, DateTime startDate, DateTime endDate, SlotStatus status, string reason)
        {
            // Проверяем, что даты корректны
            if (endDate < startDate)
            {
                ModelState.AddModelError("", "Дата окончания не может быть раньше даты начала");
                ViewBag.Masters = await _context.Masters
                    .Where(m => m.IsActive)
                    .Select(m => new { m.Id, FullName = m.LastName + " " + m.FirstName + " " + (m.MiddleName ?? "") })
                    .ToListAsync();
                return View();
            }

            var master = await _context.Masters.FindAsync(masterId);
            if (master == null)
            {
                return NotFound();
            }

            // ========== НАЧАЛО: ПРОВЕРКА НА СУЩЕСТВУЮЩИЕ ЗАПИСИ КЛИЕНТОВ ==========
            var startDateTime = startDate.Date;
            var endDateTime = endDate.Date.AddDays(1).AddTicks(-1);

            // Находим все подтвержденные записи клиентов за выбранный период
            var existingBookings = await _context.AppointmentRequests
                .Include(a => a.Master)
                .Include(a => a.Service)
                .Where(a => a.MasterId == masterId
                            && a.Status == RequestStatus.Confirmed
                            && a.AppointmentDate >= startDateTime
                            && a.AppointmentDate <= endDateTime)
                .ToListAsync();

            if (existingBookings.Any())
            {
                // Формируем сообщение со списком конфликтующих записей
                var conflicts = string.Join("<br/>", existingBookings.Select(b =>
                    $"• {b.AppointmentDate:dd.MM.yyyy HH:mm} - {b.ClientName} ({b.Service?.Name ?? "услуга"})"));

                TempData["Error"] = $"❌ НЕЛЬЗЯ добавить блокировку! У мастера {master.FullName} есть записи клиентов в этот период:<br/>{conflicts}<br/><br/>✏️ Сначала отмените эти записи.";

                ViewBag.Masters = await _context.Masters
                    .Where(m => m.IsActive)
                    .Select(m => new { m.Id, FullName = m.LastName + " " + m.FirstName + " " + (m.MiddleName ?? "") })
                    .ToListAsync();
                return View();
            }
            // ========== КОНЕЦ ПРОВЕРКИ ==========

            // Получаем тип блокировки для сообщения
            string blockType = status switch
            {
                SlotStatus.DayOff => "выходной",
                SlotStatus.SickLeave => "больничный",
                SlotStatus.Vacation => "отпуск",
                _ => "блокировка"
            };

            // Создаем слоты для каждого дня в диапазоне
            var currentDate = startDate.Date;
            var endDateOnly = endDate.Date;
            int slotsAdded = 0;
            int slotsSkipped = 0;

            while (currentDate <= endDateOnly)
            {
                // Создаем слот на весь день (с 00:00 до 23:59)
                var slot = new ScheduleSlot
                {
                    MasterId = masterId,
                    StartTime = currentDate,
                    EndTime = currentDate.AddDays(1).AddTicks(-1),
                    Status = status,
                    AppointmentRequestId = null
                };

                // Проверяем, нет ли уже конфликтующих слотов
                var existingSlot = await _context.ScheduleSlots
                    .FirstOrDefaultAsync(s => s.MasterId == masterId &&
                        s.StartTime <= slot.EndTime &&
                        s.EndTime >= slot.StartTime);

                if (existingSlot == null)
                {
                    _context.ScheduleSlots.Add(slot);
                    slotsAdded++;
                }
                else
                {
                    slotsSkipped++;
                }

                currentDate = currentDate.AddDays(1);
            }

            await _context.SaveChangesAsync();

            // Формируем сообщение о результате
            if (slotsAdded > 0)
            {
                TempData["Success"] = $"✅ {blockType.ToUpper()} добавлен для мастера {master.FullName} на период с {startDate:dd.MM.yyyy} по {endDate:dd.MM.yyyy}. Добавлено дней: {slotsAdded}";
            }
            if (slotsSkipped > 0)
            {
                TempData["Warning"] = $"⚠️ На {slotsSkipped} дней уже были блокировки, они пропущены.";
            }

            return RedirectToAction(nameof(Index));
        }
        // GET: Schedule/PrintWord
        public async Task<IActionResult> PrintWord(DateTime? date)
        {
            DateTime selectedDate = date ?? DateTime.Today;

            var masters = await _context.Masters
                .Where(m => m.IsActive)
                .ToListAsync();

            var startOfDay = selectedDate.Date;
            var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

            var slots = await _context.ScheduleSlots
                .Include(s => s.Master)
                .Include(s => s.AppointmentRequest)
                    .ThenInclude(ar => ar.Service)
                .Where(s => s.StartTime >= startOfDay && s.StartTime <= endOfDay)
                .ToListAsync();

            // Временные слоты с 9:00 до 21:00 (шаг 30 мин)
            var timeSlots = new List<string>();
            for (int hour = 9; hour <= 21; hour++)
            {
                timeSlots.Add($"{hour:D2}:00");
                if (hour < 21) timeSlots.Add($"{hour:D2}:30");
            }

            using (MemoryStream stream = new MemoryStream())
            {
                using (WordprocessingDocument wordDocument = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
                {
                    MainDocumentPart mainPart = wordDocument.AddMainDocumentPart();
                    mainPart.Document = new Document();
                    Body body = mainPart.Document.AppendChild(new Body());

                    // Заголовок
                    Paragraph headerPara = new Paragraph(
                        new ParagraphProperties(new Justification() { Val = JustificationValues.Center }),
                        new Run(new Text("САЛОН КРАСОТЫ LIS BLANC"))
                        {
                            RunProperties = new RunProperties(new Bold(), new FontSize() { Val = "32" })
                        });
                    body.AppendChild(headerPara);

                    Paragraph titlePara = new Paragraph(
                        new ParagraphProperties(new Justification() { Val = JustificationValues.Center }),
                        new Run(new Text("РАСПИСАНИЕ РАБОТЫ МАСТЕРОВ"))
                        {
                            RunProperties = new RunProperties(new Bold(), new FontSize() { Val = "28" })
                        });
                    body.AppendChild(titlePara);

                    Paragraph datePara = new Paragraph(
                        new ParagraphProperties(new Justification() { Val = JustificationValues.Center }),
                        new Run(new Text(selectedDate.ToString("dd MMMM yyyy (dddd)")))
                        {
                            RunProperties = new RunProperties(new FontSize() { Val = "24" })
                        });
                    body.AppendChild(datePara);

                    body.AppendChild(new Paragraph(new Run(new Break())));

                    // Таблица
                    Table table = new Table();

                    // Стили таблицы - ИСПРАВЛЕНО
                    TableProperties tblProps = new TableProperties(
                        new TableBorders(
                            new TopBorder() { Val = BorderValues.Single, Size = 1 },
                            new BottomBorder() { Val = BorderValues.Single, Size = 1 },
                            new LeftBorder() { Val = BorderValues.Single, Size = 1 },
                            new RightBorder() { Val = BorderValues.Single, Size = 1 },
                            new InsideHorizontalBorder() { Val = BorderValues.Single, Size = 1 },
                            new InsideVerticalBorder() { Val = BorderValues.Single, Size = 1 }),
                        new TableWidth() { Width = "5000", Type = TableWidthUnitValues.Dxa }); // Dxa вместо Twips
                    table.AppendChild(tblProps);

                    // Заголовок таблицы
                    TableRow headerRow = new TableRow();
                    headerRow.AppendChild(CreateTableCell("Мастер / Специализация", true, 2500));
                    foreach (var timeSlot in timeSlots)
                    {
                        headerRow.AppendChild(CreateTableCell(timeSlot, true, 800));
                    }
                    table.AppendChild(headerRow);

                    // Строки для каждого мастера
                    foreach (var master in masters)
                    {
                        TableRow row = new TableRow();
                        row.AppendChild(CreateTableCell($"{master.FullName}\n({master.Specialization})", false, 2500));

                        var processedSlots = new HashSet<int>();
                        int timeIndex = 0;

                        while (timeIndex < timeSlots.Count)
                        {
                            var currentTimeSlot = timeSlots[timeIndex];
                            var slotDateTime = selectedDate.Date.Add(TimeSpan.Parse(currentTimeSlot));
                            var slotEndTime = slotDateTime.AddMinutes(30);

                            var occupiedSlot = slots.FirstOrDefault(s =>
                                s.MasterId == master.Id &&
                                !processedSlots.Contains(s.Id) &&
                                s.StartTime <= slotEndTime &&
                                s.EndTime > slotDateTime);

                            if (occupiedSlot != null)
                            {
                                processedSlots.Add(occupiedSlot.Id);
                                int durationMinutes = (int)(occupiedSlot.EndTime - occupiedSlot.StartTime).TotalMinutes;
                                int colSpan = (int)Math.Ceiling(durationMinutes / 30.0);

                                string slotText = occupiedSlot.Status switch
                                {
                                    SlotStatus.Booked => occupiedSlot.AppointmentRequest?.ClientName ?? "Клиент",
                                    SlotStatus.DayOff => "ВЫХОДНОЙ",
                                    SlotStatus.SickLeave => "БОЛЬНИЧНЫЙ",
                                    SlotStatus.Vacation => "ОТПУСК",
                                    _ => ""
                                };

                                TableCell cell = CreateTableCell(slotText, false, 800 * colSpan);
                                cell.Append(new TableCellProperties(new GridSpan() { Val = colSpan }));
                                row.AppendChild(cell);

                                timeIndex += colSpan;
                            }
                            else
                            {
                                row.AppendChild(CreateTableCell("—", false, 800));
                                timeIndex++;
                            }
                        }

                        table.AppendChild(row);
                    }

                    body.AppendChild(table);
                    body.AppendChild(new Paragraph(new Run(new Break())));

                    // Легенда
                    Paragraph legendPara = new Paragraph(
                        new ParagraphProperties(new Justification() { Val = JustificationValues.Left }),
                        new Run(new Text("Условные обозначения: "))
                        {
                            RunProperties = new RunProperties(new Bold())
                        });
                    body.AppendChild(legendPara);
                    body.AppendChild(new Paragraph(new Run(new Text("— свободно"))));
                    body.AppendChild(new Paragraph(new Run(new Text("• Занято клиентом"))));
                    body.AppendChild(new Paragraph(new Run(new Text("ВЫХОДНОЙ"))));
                    body.AppendChild(new Paragraph(new Run(new Text("БОЛЬНИЧНЫЙ"))));
                    body.AppendChild(new Paragraph(new Run(new Text("ОТПУСК"))));

                    body.AppendChild(new Paragraph(new Run(new Break())));

                    Paragraph footerPara = new Paragraph(
                        new ParagraphProperties(new Justification() { Val = JustificationValues.Center }),
                        new Run(new Text($"Документ сформирован {DateTime.Now:dd.MM.yyyy HH:mm:ss}"))
                        {
                            RunProperties = new RunProperties(new FontSize() { Val = "20" })
                        });
                    body.AppendChild(footerPara);
                }

                stream.Position = 0;
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"Расписание_{selectedDate:yyyyMMdd}.docx");
            }
        }

        private TableCell CreateTableCell(string text, bool isHeader, int width)
        {
            TableCell cell = new TableCell();
            // ИСПРАВЛЕНО: используем TableWidthUnitValues.Dxa
            cell.Append(new TableCellProperties(new TableCellWidth() { Width = width.ToString(), Type = TableWidthUnitValues.Dxa }));

            Paragraph para = new Paragraph(new Run(new Text(text)));
            if (isHeader)
            {
                para.ParagraphProperties = new ParagraphProperties(new Justification() { Val = JustificationValues.Center });
                RunProperties runProps = new RunProperties(new Bold());
                para.GetFirstChild<Run>().RunProperties = runProps;
            }

            cell.Append(para);
            return cell;
        }

        // POST: Schedule/DeleteSlot/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSlot(int id)
        {
            var slot = await _context.ScheduleSlots.FindAsync(id);
            if (slot != null)
            {
                // Не даем удалить слоты, связанные с подтвержденными заявками
                if (slot.Status == SlotStatus.Booked)
                {
                    TempData["Error"] = "❌ Нельзя удалить слот с подтвержденной записью клиента";
                    return RedirectToAction(nameof(Index));
                }

                _context.ScheduleSlots.Remove(slot);
                await _context.SaveChangesAsync();
                TempData["Success"] = "✅ Блокировка успешно удалена";
            }

            return RedirectToAction(nameof(Index));
        }

    }
}