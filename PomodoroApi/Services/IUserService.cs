using PomodoroApi.Models;
using PomodoroApi.Models.DTO;
using Microsoft.AspNetCore.Identity;

namespace PomodoroApi.Services
{
    public interface IUserService
    {
        Task<IdentityResult> RegisterAsync(RegisterUserDto model);
        Task<object?> LoginAsync(LoginUserDto model);
        Task<UserProfileDto?> GetProfileAsync(string userId);
    }
}
