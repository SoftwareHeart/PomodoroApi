using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using PomodoroApi.Models;
using PomodoroApi.Services;

namespace PomodoroApi.Controllers
{
    [Authorize] // Varsayılan olarak tüm controller yetkilendirme gerektirir
    [Route("api/[controller]")]
    [ApiController]
    public class PomodoroController : ControllerBase
    {
        private readonly IPomodoroService _pomodoroService;
        private readonly ILogger<PomodoroController> _logger;

        public PomodoroController(IPomodoroService pomodoroService, ILogger<PomodoroController> logger)
        {
            _pomodoroService = pomodoroService;
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

                var sessions = await _pomodoroService.GetUserSessionsAsync(userId);
                return Ok(sessions);
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

                var statistics = await _pomodoroService.GetStatisticsAsync(userId);
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

                var weeklyStats = await _pomodoroService.GetWeeklyStatsAsync(userId);
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

                var calendarData = await _pomodoroService.GetCalendarDataAsync(userId, startDate, endDate);
                return Ok(calendarData);
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

                var monthlyStats = await _pomodoroService.GetMonthlyStatsAsync(userId, year, month);
                return Ok(monthlyStats);
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

                var dailyDetail = await _pomodoroService.GetDailyDetailAsync(userId, date);
                return Ok(dailyDetail);
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

                var session = await _pomodoroService.GetSessionByIdAsync(id, userId);

                if (session == null)
                {
                    return NotFound(new { message = "Görev bulunamadı veya bu göreve erişim yetkiniz yok" });
                }

                return Ok(session);
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

                var createdSession = await _pomodoroService.CreateSessionAsync(session, userId);
                return CreatedAtAction(nameof(GetSession), new { id = createdSession.Id }, createdSession);
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

                var result = await _pomodoroService.CompleteSessionAsync(id, userId);

                if (!result)
                {
                    return NotFound(new { message = "Görev bulunamadı, bu göreve erişim yetkiniz yok veya görev zaten tamamlanmış" });
                }

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

                var result = await _pomodoroService.DeleteSessionAsync(id, userId);

                if (!result)
                {
                    return NotFound(new { message = "Görev bulunamadı veya bu göreve erişim yetkiniz yok" });
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Görev ID:{id} silinirken hata oluştu");
                return StatusCode(500, new { message = "Görev silinirken bir hata oluştu" });
            }
        }
    }
}