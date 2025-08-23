using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PomodoroApi.Models.DTO;
using PomodoroApi.Services;
using System.Security.Claims;

namespace PomodoroApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UserController> _logger;

        public UserController(IUserService userService, ILogger<UserController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterUserDto model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _userService.RegisterAsync(model);

                if (!result.Succeeded)
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    return BadRequest(ModelState);
                }

                return Ok(new { message = "Kullanıcı başarıyla kaydedildi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kullanıcı kaydı sırasında hata oluştu");
                return StatusCode(500, new { message = "Kullanıcı kaydı sırasında bir hata oluştu" });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginUserDto model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _userService.LoginAsync(model);

                if (result == null)
                {
                    return Unauthorized(new { message = "Kullanıcı adı veya şifre hatalı" });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kullanıcı girişi sırasında hata oluştu");
                return StatusCode(500, new { message = "Kullanıcı girişi sırasında bir hata oluştu" });
            }
        }

        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "Kullanıcı kimliği alınamadı" });
                }

                var profile = await _userService.GetProfileAsync(userId);

                if (profile == null)
                {
                    return NotFound(new { message = "Kullanıcı bulunamadı" });
                }

                return Ok(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kullanıcı profili getirilirken hata oluştu");
                return StatusCode(500, new { message = "Kullanıcı profili getirilirken bir hata oluştu" });
            }
        }
    }
}