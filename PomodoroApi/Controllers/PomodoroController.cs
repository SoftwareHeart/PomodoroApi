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

                var statistics = new
                {
                    TotalCompletedSessions = completedSessions.Count,
                    TotalMinutesWorked = completedSessions.Sum(s => s.Duration),
                    AverageSessionDuration = completedSessions.Any() ? completedSessions.Average(s => s.Duration) : 0,
                    CompletedToday = completedSessions.Count(s => s.EndTime.HasValue && s.EndTime.Value.Date == DateTime.Today),
                    MinutesToday = completedSessions
                        .Where(s => s.EndTime.HasValue && s.EndTime.Value.Date == DateTime.Today)
                        .Sum(s => s.Duration)
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
                        tamamlanan = completedSessions.Count,
                        dakika = completedSessions.Sum(s => s.Duration)
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