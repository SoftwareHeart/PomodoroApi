namespace PomodoroApi.Models
{
    public class PomodoroSession
    {
        public int Id { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int Duration { get; set; } // dakika cinsinden
        public string TaskName { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public string UserId { get; set; } = "defaultUser"; // İleriki aşamalarda kimlik doğrulama eklenecek
    }
}