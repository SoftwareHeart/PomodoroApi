using System.ComponentModel.DataAnnotations;

namespace PomodoroApi.Models.DTO
{
    public class LoginUserDto
    {
        [Required(ErrorMessage = "Kullanıcı adı zorunludur")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Şifre zorunludur")]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }
}
