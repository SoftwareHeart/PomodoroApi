using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PomodoroApi.Data;
using PomodoroApi.Models;

namespace PomodoroApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PomodoroController : ControllerBase
    {
        private readonly PomodoroDbContext _context;

        public PomodoroController(PomodoroDbContext context)
        {
            _context = context;
        }

        // GET: api/Pomodoro
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PomodoroSession>>> GetSessions()
        {
            return await _context.PomodoroSessions.ToListAsync();
        }

        // GET: api/Pomodoro/statistics
        [HttpGet("statistics")]
        public async Task<ActionResult<object>> GetStatistics(string userId = "defaultUser")
        {
            if (string.IsNullOrEmpty(userId))
            {
                userId = "defaultUser";
            }

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

        // GET: api/Pomodoro/5
        [HttpGet("{id}")]
        public async Task<ActionResult<PomodoroSession>> GetSession(int id)
        {
            var session = await _context.PomodoroSessions.FindAsync(id);

            if (session == null)
            {
                return NotFound();
            }

            return session;
        }

        // POST: api/Pomodoro
        [HttpPost]
        public async Task<ActionResult<PomodoroSession>> CreateSession(PomodoroSession session)
        {
            session.StartTime = DateTime.Now;
            session.IsCompleted = false;

            _context.PomodoroSessions.Add(session);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetSession), new { id = session.Id }, session);
        }

        // PUT: api/Pomodoro/5/complete
        [HttpPut("{id}/complete")]
        public async Task<IActionResult> CompleteSession(int id)
        {
            var session = await _context.PomodoroSessions.FindAsync(id);

            if (session == null)
            {
                return NotFound();
            }

            session.EndTime = DateTime.Now;
            session.IsCompleted = true;

            _context.Entry(session).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/Pomodoro/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSession(int id)
        {
            var session = await _context.PomodoroSessions.FindAsync(id);

            if (session == null)
            {
                return NotFound();
            }

            _context.PomodoroSessions.Remove(session);
            await _context.SaveChangesAsync();

            return NoContent();
        }


        // PomodoroController.cs'ye eklenecek
        [HttpGet("weekly-stats")]
        public async Task<ActionResult<object>> GetWeeklyStats(string userId = "defaultUser")
        {
            if (string.IsNullOrEmpty(userId))
            {
                userId = "defaultUser";
            }

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