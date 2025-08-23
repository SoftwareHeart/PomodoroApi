using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using PomodoroApi.Models;
using PomodoroApi.Models.DTO;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PomodoroApi.Services
{
    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UserService> _logger;

        public UserService(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration configuration,
            ILogger<UserService> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<IdentityResult> RegisterAsync(RegisterUserDto model)
        {
            try
            {
                _logger.LogInformation($"Yeni kullanıcı kaydediliyor: {model.Username}");

                var user = new ApplicationUser 
                { 
                    UserName = model.Username, 
                    Email = model.Email 
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation($"Kullanıcı başarıyla kaydedildi: {model.Username}");
                }
                else
                {
                    _logger.LogWarning($"Kullanıcı kaydı başarısız: {model.Username}");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Kullanıcı kaydı sırasında hata oluştu: {model.Username}");
                throw;
            }
        }

        public async Task<object?> LoginAsync(LoginUserDto model)
        {
            try
            {
                _logger.LogInformation($"Kullanıcı giriş yapmaya çalışıyor: {model.Username}");

                var user = await _userManager.FindByNameAsync(model.Username) ??
                           await _userManager.FindByEmailAsync(model.Username);

                if (user == null)
                {
                    _logger.LogWarning($"Kullanıcı bulunamadı: {model.Username}");
                    return null;
                }

                var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);

                if (!result.Succeeded)
                {
                    _logger.LogWarning($"Hatalı şifre girişi: {model.Username}");
                    return null;
                }

                var token = GenerateJwtToken(user);

                _logger.LogInformation($"Kullanıcı başarıyla giriş yaptı: {model.Username}");

                return new
                {
                    token = token,
                    userId = user.Id,
                    username = user.UserName,
                    email = user.Email
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Kullanıcı girişi sırasında hata oluştu: {model.Username}");
                throw;
            }
        }

        public async Task<UserProfileDto?> GetProfileAsync(string userId)
        {
            try
            {
                _logger.LogInformation($"Kullanıcı profili getiriliyor: {userId}");

                var user = await _userManager.FindByIdAsync(userId);

                if (user == null)
                {
                    _logger.LogWarning($"Kullanıcı bulunamadı: {userId}");
                    return null;
                }

                return new UserProfileDto
                {
                    Id = user.Id,
                    Username = user.UserName,
                    Email = user.Email,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Kullanıcı profili getirilirken hata oluştu: {userId}");
                throw;
            }
        }

        private string GenerateJwtToken(ApplicationUser user)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.Email, user.Email)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtSettings:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.Now.AddDays(Convert.ToDouble(_configuration["JwtSettings:ExpireDays"]));

            var token = new JwtSecurityToken(
                _configuration["JwtSettings:Issuer"],
                _configuration["JwtSettings:Audience"],
                claims,
                expires: expires,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
