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
                    .OrderByDescending(s => s.StartTime)
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

                // Tüm oturumları al
                var allSessions = await _context.PomodoroSessions
                    .Where(s => s.UserId == userId)
                    .ToListAsync();

                // Tamamlanan oturumları filtrele
                var completedSessions = allSessions.Where(s => s.IsCompleted).ToList();

                _logger.LogInformation($"Toplam {completedSessions.Count} tamamlanmış oturum bulundu");

                // Bugünkü tamamlanan oturumları filtrele
                var todaySessions = completedSessions
                    .Where(s => s.EndTime.HasValue && s.EndTime.Value.Date == DateTime.Today)
                    .ToList();

                // Günlük ortalama hesapla (son 30 günde aktif olan günlerin ortalaması)
                var last30Days = DateTime.Today.AddDays(-30);
                var recentSessions = completedSessions
                    .Where(s => s.EndTime.HasValue && s.EndTime.Value.Date >= last30Days)
                    .ToList();

                var activeDaysInLast30 = recentSessions
                    .GroupBy(s => s.EndTime.Value.Date)
                    .Count();

                var averagePerDay = activeDaysInLast30 > 0
                    ? Math.Round((double)recentSessions.Count / activeDaysInLast30, 1)
                    : 0;

                var statistics = new
                {
                    // Frontend'in beklediği field adları
                    totalPomodoros = completedSessions.Count, // Her session = 1 pomodoro
                    totalMinutes = completedSessions.Sum(s => s.Duration), // Toplam dakika
                    totalTasks = allSessions.Count, // Toplam task sayısı (tamamlanan + tamamlanmayan)
                    completedTasks = completedSessions.Count, // Tamamlanan task sayısı
                    averagePerDay = averagePerDay, // Günlük ortalama

                    // Bugünkü veriler
                    todayPomodoros = todaySessions.Count,
                    todayMinutes = todaySessions.Sum(s => s.Duration),

                    // Ek istatistikler
                    averageSessionDuration = completedSessions.Any()
                        ? Math.Round(completedSessions.Average(s => s.Duration), 1)
                        : 0,

                    // En verimli gün (son 30 günde)
                    mostProductiveDay = recentSessions
                        .GroupBy(s => s.EndTime.Value.Date)
                        .OrderByDescending(g => g.Count())
                        .Select(g => new {
                            date = g.Key.ToString("yyyy-MM-dd"),
                            pomodoros = g.Count(),
                            minutes = g.Sum(s => s.Duration)
                        })
                        .FirstOrDefault()
                };

                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "İstatistikler getirilirken hata oluştu");
                return StatusCode(500, new { message = "İstatistikler getirilirken bir hata oluştu" });
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
                        day = dayName, // Frontend'in beklediği field adı
                        pomodoros = completedSessions.Count, // Her session = 1 pomodoro
                        minutes = completedSessions.Sum(s => s.Duration),
                        hours = Math.Round((double)completedSessions.Sum(s => s.Duration) / 60, 1)
                    });
                }

                return Ok(weeklyStats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Haftalık istatistikler getirilirken hata oluştu");
                return StatusCode(500, new { message = "Haftalık istatistikler getirilirken bir hata oluştu" });
            }
        }

        // GET: api/Pomodoro/calendar-data
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

                // Günlük bazda grupla - HER OTURUM AYRI POMODORO
                var dailyData = sessions
                    .GroupBy(s => s.EndTime.Value.Date)
                    .Select(g => new
                    {
                        date = g.Key.ToString("yyyy-MM-dd"),
                        pomodoros = g.Count(), // Her session = 1 pomodoro
                        minutes = g.Sum(s => s.Duration),
                        hours = Math.Round((double)g.Sum(s => s.Duration) / 60, 1),
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
                            hours = 0.0,
                            sessions = new List<object>()
                        });
                    }
                }

                return Ok(new
                {
                    startDate = start.ToString("yyyy-MM-dd"),
                    endDate = end.ToString("yyyy-MM-dd"),
                    data = allDays,
                    summary = new
                    {
                        totalPomodoros = sessions.Count,
                        totalMinutes = sessions.Sum(s => s.Duration),
                        totalHours = Math.Round((double)sessions.Sum(s => s.Duration) / 60, 1),
                        activeDays = dailyData.Count
                    }
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
                var totalPomodoros = monthlySessions.Count; // Her session = 1 pomodoro
                var totalMinutes = monthlySessions.Sum(s => s.Duration);
                var activeDays = monthlySessions
                    .GroupBy(s => s.EndTime.Value.Date)
                    .Count();
                var averagePerDay = activeDays > 0 ? Math.Round((double)totalPomodoros / activeDays, 1) : 0;

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

                // Görev bazında analiz
                var taskAnalysis = monthlySessions
                    .GroupBy(s => s.TaskName)
                    .Select(g => new
                    {
                        taskName = g.Key,
                        pomodoros = g.Count(),
                        minutes = g.Sum(s => s.Duration),
                        percentage = Math.Round((double)g.Count() / totalPomodoros * 100, 1)
                    })
                    .OrderByDescending(x => x.pomodoros)
                    .Take(5) // Top 5 görev
                    .ToList();

                return Ok(new
                {
                    month = month,
                    year = year,
                    totalPomodoros = totalPomodoros,
                    totalMinutes = totalMinutes,
                    totalHours = Math.Round((double)totalMinutes / 60, 1),
                    activeDays = activeDays,
                    averagePerDay = averagePerDay,
                    mostProductiveDay = dailyGroups != null ? new
                    {
                        date = dailyGroups.date.ToString("yyyy-MM-dd"),
                        pomodoros = dailyGroups.pomodoros,
                        minutes = dailyGroups.minutes,
                        hours = Math.Round((double)dailyGroups.minutes / 60, 1)
                    } : null,
                    topTasks = taskAnalysis
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

                var totalPomodoros = dailySessions.Count; // Her session = 1 pomodoro
                var totalMinutes = dailySessions.Sum(s => s.Duration);

                // Görev bazında grupla
                var taskGroups = dailySessions
                    .GroupBy(s => s.TaskName)
                    .Select(g => new
                    {
                        taskName = g.Key,
                        pomodoros = g.Count(), // Her session = 1 pomodoro
                        minutes = g.Sum(s => s.Duration),
                        hours = Math.Round((double)g.Sum(s => s.Duration) / 60, 1),
                        percentage = totalPomodoros > 0 ? Math.Round((double)g.Count() / totalPomodoros * 100, 1) : 0,
                        sessions = g.Select(s => new
                        {
                            id = s.Id,
                            startTime = s.StartTime.ToString("HH:mm"),
                            endTime = s.EndTime.HasValue ? s.EndTime.Value.ToString("HH:mm") : null,
                            duration = s.Duration
                        }).ToList()
                    })
                    .OrderByDescending(x => x.pomodoros)
                    .ToList();

                // Saatlik dağılım (hangi saatlerde ne kadar çalışılmış)
                var hourlyDistribution = dailySessions
                    .Where(s => s.StartTime != null)
                    .GroupBy(s => s.StartTime.Hour)
                    .Select(g => new
                    {
                        hour = g.Key,
                        pomodoros = g.Count(),
                        minutes = g.Sum(s => s.Duration)
                    })
                    .OrderBy(x => x.hour)
                    .ToList();

                return Ok(new
                {
                    date = date.ToString("yyyy-MM-dd"),
                    dayName = GetTurkishDayName(date.DayOfWeek),
                    totalPomodoros = totalPomodoros,
                    totalMinutes = totalMinutes,
                    totalHours = Math.Round((double)totalMinutes / 60, 1),
                    averageSessionDuration = totalPomodoros > 0 ? Math.Round((double)totalMinutes / totalPomodoros, 1) : 0,
                    tasks = taskGroups,
                    hourlyDistribution = hourlyDistribution,
                    sessions = dailySessions.Select(s => new
                    {
                        id = s.Id,
                        taskName = s.TaskName,
                        duration = s.Duration,
                        startTime = s.StartTime.ToString("HH:mm"),
                        endTime = s.EndTime.HasValue ? s.EndTime.Value.ToString("HH:mm") : null,
                        fullStartTime = s.StartTime,
                        fullEndTime = s.EndTime
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Günlük detay getirilirken hata oluştu");
                return StatusCode(500, new { message = "Günlük detay getirilirken bir hata oluştu" });
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
                session.EndTime = null; // Yeni session'lar için null

                _logger.LogInformation($"Kullanıcı {userId} için yeni görev ekleniyor: {session.TaskName}");

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

                // Eğer zaten tamamlanmışsa uyarı ver
                if (session.IsCompleted)
                {
                    _logger.LogWarning($"Görev ID:{id} zaten tamamlanmış");
                    return BadRequest(new { message = "Bu görev zaten tamamlanmış" });
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

        // Gün adlarını Türkçe olarak döndüren yardımcı metod
        private string GetTurkishDayName(DayOfWeek dayOfWeek)
        {
            return dayOfWeek switch
            {
                DayOfWeek.Monday => "Pazartesi",
                DayOfWeek.Tuesday => "Salı",
                DayOfWeek.Wednesday => "Çarşamba",
                DayOfWeek.Thursday => "Perşembe",
                DayOfWeek.Friday => "Cuma",
                DayOfWeek.Saturday => "Cumartesi",
                DayOfWeek.Sunday => "Pazar",
                _ => string.Empty
            };
        }
    }
}
