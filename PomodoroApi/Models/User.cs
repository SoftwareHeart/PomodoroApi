namespace PomodoroApi.Models
{
    public class User
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<PomodoroSession> Sessions { get; set; } = new List<PomodoroSession>();
    }
}