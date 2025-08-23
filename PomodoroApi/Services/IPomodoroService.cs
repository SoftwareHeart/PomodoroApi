using PomodoroApi.Models;

namespace PomodoroApi.Services
{
    public interface IPomodoroService
    {
        Task<IEnumerable<PomodoroSession>> GetUserSessionsAsync(string userId);
        Task<PomodoroSession?> GetSessionByIdAsync(int id, string userId);
        Task<PomodoroSession> CreateSessionAsync(PomodoroSession session, string userId);
        Task<bool> CompleteSessionAsync(int id, string userId);
        Task<bool> DeleteSessionAsync(int id, string userId);
        Task<object> GetStatisticsAsync(string userId);
        Task<object> GetWeeklyStatsAsync(string userId);
        Task<object> GetCalendarDataAsync(string userId, DateTime? startDate = null, DateTime? endDate = null);
        Task<object> GetMonthlyStatsAsync(string userId, int year, int month);
        Task<object> GetDailyDetailAsync(string userId, DateTime date);
    }
}
