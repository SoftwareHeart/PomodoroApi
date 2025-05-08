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
    }
}