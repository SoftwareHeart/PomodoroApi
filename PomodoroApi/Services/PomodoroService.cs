using Microsoft.EntityFrameworkCore;
using PomodoroApi.Data;
using PomodoroApi.Models;

namespace PomodoroApi.Services
{
    public class PomodoroService : IPomodoroService
    {
        private readonly PomodoroDbContext _context;
        private readonly ILogger<PomodoroService> _logger;

        public PomodoroService(PomodoroDbContext context, ILogger<PomodoroService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<PomodoroSession>> GetUserSessionsAsync(string userId)
        {
            try
            {
                _logger.LogInformation($"Kullanıcı {userId} için görevler getiriliyor");

                return await _context.PomodoroSessions
                    .Where(s => s.UserId == userId)
                    .OrderByDescending(s => s.StartTime)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Görevler getirilirken hata oluştu");
                throw;
            }
        }

        public async Task<PomodoroSession?> GetSessionByIdAsync(int id, string userId)
        {
            try
            {
                var session = await _context.PomodoroSessions
                    .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

                if (session == null)
                {
                    _logger.LogWarning($"Kullanıcı {userId} için ID:{id} görev bulunamadı");
                }

                return session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Görev ID:{id} getirilirken hata oluştu");
                throw;
            }
        }

        public async Task<PomodoroSession> CreateSessionAsync(PomodoroSession session, string userId)
        {
            try
            {
                // Kullanıcı ID'sini mevcut kimlikle ayarla (güvenlik için)
                session.UserId = userId;
                session.StartTime = DateTime.Now;
                session.IsCompleted = false;
                session.EndTime = null; // Yeni session'lar için null

                _logger.LogInformation($"Kullanıcı {userId} için yeni görev ekleniyor: {session.TaskName}");

                _context.PomodoroSessions.Add(session);
                await _context.SaveChangesAsync();

                return session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Görev eklenirken hata oluştu");
                throw;
            }
        }

        public async Task<bool> CompleteSessionAsync(int id, string userId)
        {
            try
            {
                var session = await _context.PomodoroSessions
                    .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

                if (session == null)
                {
                    _logger.LogWarning($"Kullanıcı {userId} için tamamlanacak ID:{id} görev bulunamadı");
                    return false;
                }

                // Eğer zaten tamamlanmışsa false döndür
                if (session.IsCompleted)
                {
                    _logger.LogWarning($"Görev ID:{id} zaten tamamlanmış");
                    return false;
                }

                session.EndTime = DateTime.Now;
                session.IsCompleted = true;

                _logger.LogInformation($"Kullanıcı {userId} için ID:{id} görev tamamlanıyor");

                _context.Entry(session).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Görev ID:{id} tamamlanırken hata oluştu");
                throw;
            }
        }

        public async Task<bool> DeleteSessionAsync(int id, string userId)
        {
            try
            {
                var session = await _context.PomodoroSessions
                    .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

                if (session == null)
                {
                    _logger.LogWarning($"Kullanıcı {userId} için silinecek ID:{id} görev bulunamadı");
                    return false;
                }

                _logger.LogInformation($"Kullanıcı {userId} için ID:{id} görev siliniyor");

                _context.PomodoroSessions.Remove(session);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Görev ID:{id} silinirken hata oluştu");
                throw;
            }
        }

        public async Task<object> GetStatisticsAsync(string userId)
        {
            try
            {
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

                return statistics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "İstatistikler getirilirken hata oluştu");
                throw;
            }
        }

        public async Task<object> GetWeeklyStatsAsync(string userId)
        {
            try
            {
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

                return weeklyStats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Haftalık istatistikler getirilirken hata oluştu");
                throw;
            }
        }

        public async Task<object> GetCalendarDataAsync(string userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
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

                return new
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
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Takvim verileri getirilirken hata oluştu");
                throw;
            }
        }

        public async Task<object> GetMonthlyStatsAsync(string userId, int year, int month)
        {
            try
            {
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

                return new
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
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Aylık istatistikler getirilirken hata oluştu");
                throw;
            }
        }

        public async Task<object> GetDailyDetailAsync(string userId, DateTime date)
        {
            try
            {
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

                return new
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
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Günlük detay getirilirken hata oluştu");
                throw;
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
