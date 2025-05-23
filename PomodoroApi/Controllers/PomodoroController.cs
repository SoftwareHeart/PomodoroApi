using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using PomodoroApi.Data;
using PomodoroApi.Models;

namespace PomodoroApi.Controllers
{
    [Authorize] // Varsayılan olarak tüm controller yetkilendirme gerektirir
    [Route("api/[controller]")]
    [ApiController]
    public class PomodoroController : ControllerBase
    {
        private readonly PomodoroDbContext _context;
        private readonly ILogger<PomodoroController> _logger;

        public PomodoroController(PomodoroDbContext context, ILogger<PomodoroController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/Pomodoro
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PomodoroSession>>> GetSessions()
        {
            try
            {
                // Mevcut kullanıcının kimliğini al
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Kullanıcı kimliği alınamadı");
                    return Unauthorized(new { message = "Kullanıcı kimliği alınamadı" });
                }

                _logger.LogInformation($"Kullanıcı {userId} için görevler getiriliyor");

                // Yalnızca kullanıcıya ait oturumları getir
                return await _context.PomodoroSessions
                    .Where(s => s.UserId == userId)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Görevler getirilirken hata oluştu");
                return StatusCode(500, new { message = "Görevler getirilirken bir hata oluştu" });
            }
        }

        // GET: api/Pomodoro/statistics
        [HttpGet("statistics")]
        public async Task<ActionResult<object>> GetStatistics()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("İstatistikler için kullanıcı kimliği alınamadı");
                    return Unauthorized(new { message = "Kullanıcı kimliği alınamadı" });
                }

                _logger.LogInformation($"Kullanıcı {userId} için istatistikler getiriliyor");

                var completedSessions = await _context.PomodoroSessions
                    .Where(s => s.UserId == userId && s.IsCompleted)
                    .ToListAsync();

                _logger.LogInformation($"Toplam {completedSessions.Count} tamamlanmış oturum bulundu");

                // Bugünkü oturumları filtrele
                var todaySessions = completedSessions
                    .Where(s => s.EndTime.HasValue && s.EndTime.Value.Date == DateTime.Today)
                    .ToList();

                _logger.LogInformation($"Bugün {todaySessions.Count} oturum tamamlandı");

                var statistics = new
                {
                    TotalCompletedSessions = completedSessions.Count,
                    TotalMinutesWorked = completedSessions.Sum(s => s.Duration),
                    AverageSessionDuration = completedSessions.Any() ? completedSessions.Average(s => s.Duration) : 0,
                    CompletedToday = todaySessions.Count, // Her oturum = 1 pomodoro
                    MinutesToday = todaySessions.Sum(s => s.Duration),
                    DebugInfo = new // Debug için
                    {
                        TotalSessionsInDb = completedSessions.Count,
                        TodaySessionsCount = todaySessions.Count,
                        TodaySessionIds = todaySessions.Select(s => s.Id).ToList()
                    }
                };

                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "İstatistikler getirilirken hata oluştu");
                return StatusCode(500, new { message = "İstatistikler getirilirken bir hata oluştu" });
            }
        }


        // GET: api/Pomodoro/5
        [HttpGet("{id}")]
        public async Task<ActionResult<PomodoroSession>> GetSession(int id)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Görev için kullanıcı kimliği alınamadı");
                    return Unauthorized(new { message = "Kullanıcı kimliği alınamadı" });
                }

                var session = await _context.PomodoroSessions
                    .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

                if (session == null)
                {
                    _logger.LogWarning($"Kullanıcı {userId} için ID:{id} görev bulunamadı");
                    return NotFound(new { message = "Görev bulunamadı veya bu göreve erişim yetkiniz yok" });
                }

                return session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Görev ID:{id} getirilirken hata oluştu");
                return StatusCode(500, new { message = "Görev getirilirken bir hata oluştu" });
            }
        }

        // POST: api/Pomodoro
        [HttpPost]
        public async Task<ActionResult<PomodoroSession>> CreateSession(PomodoroSession session)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Görev eklemek için kullanıcı kimliği alınamadı");
                    return Unauthorized(new { message = "Kullanıcı kimliği alınamadı" });
                }

                // Kullanıcı ID'sini mevcut kimlikle ayarla (güvenlik için)
                session.UserId = userId;
                session.StartTime = DateTime.Now;
                session.IsCompleted = false;

                _logger.LogInformation($"Kullanıcı {userId} için yeni görev ekleniyor");

                _context.PomodoroSessions.Add(session);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetSession), new { id = session.Id }, session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Görev eklenirken hata oluştu");
                return StatusCode(500, new { message = "Görev eklenirken bir hata oluştu" });
            }
        }

        // PUT: api/Pomodoro/5/complete
        [HttpPut("{id}/complete")]
        public async Task<IActionResult> CompleteSession(int id)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Görev tamamlamak için kullanıcı kimliği alınamadı");
                    return Unauthorized(new { message = "Kullanıcı kimliği alınamadı" });
                }

                var session = await _context.PomodoroSessions
                    .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

                if (session == null)
                {
                    _logger.LogWarning($"Kullanıcı {userId} için tamamlanacak ID:{id} görev bulunamadı");
                    return NotFound(new { message = "Görev bulunamadı veya bu göreve erişim yetkiniz yok" });
                }

                session.EndTime = DateTime.Now;
                session.IsCompleted = true;

                _logger.LogInformation($"Kullanıcı {userId} için ID:{id} görev tamamlanıyor");

                _context.Entry(session).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Görev ID:{id} tamamlanırken hata oluştu");
                return StatusCode(500, new { message = "Görev tamamlanırken bir hata oluştu" });
            }
        }

        // DELETE: api/Pomodoro/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSession(int id)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Görev silmek için kullanıcı kimliği alınamadı");
                    return Unauthorized(new { message = "Kullanıcı kimliği alınamadı" });
                }

                var session = await _context.PomodoroSessions
                    .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

                if (session == null)
                {
                    _logger.LogWarning($"Kullanıcı {userId} için silinecek ID:{id} görev bulunamadı");
                    return NotFound(new { message = "Görev bulunamadı veya bu göreve erişim yetkiniz yok" });
                }

                _logger.LogInformation($"Kullanıcı {userId} için ID:{id} görev siliniyor");

                _context.PomodoroSessions.Remove(session);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Görev ID:{id} silinirken hata oluştu");
                return StatusCode(500, new { message = "Görev silinirken bir hata oluştu" });
            }
        }

        // GET: api/Pomodoro/weekly-stats
        [HttpGet("weekly-stats")]
        public async Task<ActionResult<object>> GetWeeklyStats()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Haftalık istatistikler için kullanıcı kimliği alınamadı");
                    return Unauthorized(new { message = "Kullanıcı kimliği alınamadı" });
                }

                _logger.LogInformation($"Kullanıcı {userId} için haftalık istatistikler getiriliyor");

                // Son 7 günün tarihlerini hesapla
                var today = DateTime.Today;
                var last7Days = Enumerable.Range(0, 7)
                    .Select(i => today.AddDays(-i))
                    .Reverse()
                    .ToList();

                var weeklyStats = new List<object>();

                foreach (var date in last7Days)
                {
                    // Bu tarih için tamamlanan pomodorolar
                    var completedSessions = await _context.PomodoroSessions
                        .Where(s => s.UserId == userId &&
                                   s.IsCompleted &&
                                   s.EndTime.HasValue &&
                                   s.EndTime.Value.Date == date)
                        .ToListAsync();

                    // Türkçe gün ismini al
                    string dayName = GetTurkishDayName(date.DayOfWeek);

                    weeklyStats.Add(new
                    {
                        date = date.ToString("yyyy-MM-dd"),
                        name = dayName, // Örn: "Pzt", "Sal", vs.
                        tamamlanan = completedSessions.Count, // Her oturum = 1 pomodoro
                        dakika = completedSessions.Sum(s => s.Duration)
                    });

                    _logger.LogInformation($"{date:yyyy-MM-dd} ({dayName}): {completedSessions.Count} pomodoro, {completedSessions.Sum(s => s.Duration)} dakika");
                }

                return Ok(weeklyStats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Haftalık istatistikler getirilirken hata oluştu");
                return StatusCode(500, new { message = "Haftalık istatistikler getirilirken bir hata oluştu" });
            }
        }
        [HttpGet("calendar-data")]
        public async Task<ActionResult<object>> GetCalendarData([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Takvim verileri için kullanıcı kimliği alınamadı");
                    return Unauthorized(new { message = "Kullanıcı kimliği alınamadı" });
                }

                // Varsayılan tarih aralığı: Son 90 gün
                var end = endDate ?? DateTime.Today;
                var start = startDate ?? end.AddDays(-90);

                _logger.LogInformation($"Kullanıcı {userId} için {start:yyyy-MM-dd} - {end:yyyy-MM-dd} takvim verileri getiriliyor");

                // Belirtilen tarih aralığındaki tamamlanmış oturumları getir
                var sessions = await _context.PomodoroSessions
                    .Where(s => s.UserId == userId &&
                               s.IsCompleted &&
                               s.EndTime.HasValue &&
                               s.EndTime.Value.Date >= start.Date &&
                               s.EndTime.Value.Date <= end.Date)
                    .ToListAsync();

                // Debug için loglama ekle
                _logger.LogInformation($"Toplam {sessions.Count} tamamlanmış oturum bulundu");

                // Günlük bazda grupla - HER OTURUM AYRI POMODORO
                var dailyData = sessions
                    .GroupBy(s => s.EndTime.Value.Date)
                    .Select(g => new
                    {
                        date = g.Key.ToString("yyyy-MM-dd"),
                        pomodoros = g.Count(), // Her oturum = 1 pomodoro
                        minutes = g.Sum(s => s.Duration),
                        sessions = g.Select(s => new
                        {
                            id = s.Id,
                            taskName = s.TaskName,
                            duration = s.Duration,
                            startTime = s.StartTime,
                            endTime = s.EndTime
                        }).ToList()
                    })
                    .OrderBy(x => x.date)
                    .ToList();

                // Debug için günlük verileri logla
                foreach (var day in dailyData)
                {
                    _logger.LogInformation($"Tarih: {day.date}, Pomodoro: {day.pomodoros}, Dakika: {day.minutes}");
                }

                // Tarih aralığındaki tüm günleri doldur (boş günler için 0 değerleri)
                var allDays = new List<object>();
                for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
                {
                    var dateString = date.ToString("yyyy-MM-dd");
                    var existingData = dailyData.FirstOrDefault(d => d.date == dateString);

                    if (existingData != null)
                    {
                        allDays.Add(existingData);
                    }
                    else
                    {
                        allDays.Add(new
                        {
                            date = dateString,
                            pomodoros = 0,
                            minutes = 0,
                            sessions = new List<object>()
                        });
                    }
                }

                return Ok(new
                {
                    startDate = start.ToString("yyyy-MM-dd"),
                    endDate = end.ToString("yyyy-MM-dd"),
                    data = allDays,
                    totalSessions = sessions.Count // Debug için
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Takvim verileri getirilirken hata oluştu");
                return StatusCode(500, new { message = "Takvim verileri getirilirken bir hata oluştu" });
            }
        }
        // GET: api/Pomodoro/monthly-stats
        [HttpGet("monthly-stats")]
        public async Task<ActionResult<object>> GetMonthlyStats([FromQuery] int year, [FromQuery] int month)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Aylık istatistikler için kullanıcı kimliği alınamadı");
                    return Unauthorized(new { message = "Kullanıcı kimliği alınamadı" });
                }

                _logger.LogInformation($"Kullanıcı {userId} için {year}/{month} aylık istatistikleri getiriliyor");

                // Ayın başı ve sonu
                var monthStart = new DateTime(year, month, 1);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);

                // Bu aydaki tamamlanmış oturumları getir
                var monthlySessions = await _context.PomodoroSessions
                    .Where(s => s.UserId == userId &&
                               s.IsCompleted &&
                               s.EndTime.HasValue &&
                               s.EndTime.Value.Date >= monthStart.Date &&
                               s.EndTime.Value.Date <= monthEnd.Date)
                    .ToListAsync();

                // İstatistikleri hesapla
                var totalPomodoros = monthlySessions.Count;
                var totalMinutes = monthlySessions.Sum(s => s.Duration);
                var activeDays = monthlySessions
                    .GroupBy(s => s.EndTime.Value.Date)
                    .Count();
                var averagePerDay = activeDays > 0 ? (double)totalPomodoros / activeDays : 0;

                // En verimli gün
                var dailyGroups = monthlySessions
                    .GroupBy(s => s.EndTime.Value.Date)
                    .Select(g => new
                    {
                        date = g.Key,
                        pomodoros = g.Count(),
                        minutes = g.Sum(s => s.Duration)
                    })
                    .OrderByDescending(x => x.pomodoros)
                    .FirstOrDefault();

                return Ok(new
                {
                    month = month,
                    year = year,
                    totalPomodoros = totalPomodoros,
                    totalMinutes = totalMinutes,
                    totalHours = Math.Round((double)totalMinutes / 60, 1),
                    activeDays = activeDays,
                    averagePerDay = Math.Round(averagePerDay, 1),
                    mostProductiveDay = dailyGroups != null ? new
                    {
                        date = dailyGroups.date.ToString("yyyy-MM-dd"),
                        pomodoros = dailyGroups.pomodoros,
                        minutes = dailyGroups.minutes
                    } : null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Aylık istatistikler getirilirken hata oluştu");
                return StatusCode(500, new { message = "Aylık istatistikler getirilirken bir hata oluştu" });
            }
        }

        // GET: api/Pomodoro/daily-detail
        [HttpGet("daily-detail")]
        public async Task<ActionResult<object>> GetDailyDetail([FromQuery] DateTime date)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Günlük detay için kullanıcı kimliği alınamadı");
                    return Unauthorized(new { message = "Kullanıcı kimliği alınamadı" });
                }

                _logger.LogInformation($"Kullanıcı {userId} için {date:yyyy-MM-dd} günlük detayları getiriliyor");

                // Belirtilen gündeki tamamlanmış oturumları getir
                var dailySessions = await _context.PomodoroSessions
                    .Where(s => s.UserId == userId &&
                               s.IsCompleted &&
                               s.EndTime.HasValue &&
                               s.EndTime.Value.Date == date.Date)
                    .OrderBy(s => s.StartTime)
                    .ToListAsync();

                _logger.LogInformation($"{date:yyyy-MM-dd} tarihinde {dailySessions.Count} oturum bulundu");

                var totalPomodoros = dailySessions.Count; // Her oturum = 1 pomodoro
                var totalMinutes = dailySessions.Sum(s => s.Duration);

                // Görev bazında grupla
                var taskGroups = dailySessions
                    .GroupBy(s => s.TaskName)
                    .Select(g => new
                    {
                        taskName = g.Key,
                        pomodoros = g.Count(), // Her oturum = 1 pomodoro
                        minutes = g.Sum(s => s.Duration),
                        sessions = g.Select(s => new
                        {
                            id = s.Id,
                            startTime = s.StartTime,
                            endTime = s.EndTime,
                            duration = s.Duration
                        }).ToList()
                    })
                    .OrderByDescending(x => x.pomodoros)
                    .ToList();

                return Ok(new
                {
                    date = date.ToString("yyyy-MM-dd"),
                    totalPomodoros = totalPomodoros,
                    totalMinutes = totalMinutes,
                    totalHours = Math.Round((double)totalMinutes / 60, 1),
                    tasks = taskGroups,
                    sessions = dailySessions.Select(s => new
                    {
                        id = s.Id,
                        taskName = s.TaskName,
                        duration = s.Duration,
                        startTime = s.StartTime,
                        endTime = s.EndTime
                    }).ToList(),
                    debugInfo = new // Debug için
                    {
                        rawSessionCount = dailySessions.Count,
                        isCompletedFilter = dailySessions.All(s => s.IsCompleted),
                        endTimeFilter = dailySessions.All(s => s.EndTime.HasValue)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Günlük detay getirilirken hata oluştu");
                return StatusCode(500, new { message = "Günlük detay getirilirken bir hata oluştu" });
            }
        }
        // Gün adlarını Türkçe olarak döndüren yardımcı metod
        private string GetTurkishDayName(DayOfWeek dayOfWeek)
        {
            switch (dayOfWeek)
            {
                case DayOfWeek.Monday: return "Pzt";
                case DayOfWeek.Tuesday: return "Sal";
                case DayOfWeek.Wednesday: return "Çar";
                case DayOfWeek.Thursday: return "Per";
                case DayOfWeek.Friday: return "Cum";
                case DayOfWeek.Saturday: return "Cmt";
                case DayOfWeek.Sunday: return "Paz";
                default: return string.Empty;
            }
        }
    }
}