using ELearningPlatform.Core.Enums;

namespace ELearningPlatform.Core.DTOs
{
    public class RegisterDto
    {
        public required string Username { get; set; }
        public required string Email { get; set; }
        public required string Password { get; set; }
        public UserRole Role { get; set; }
        public string? AdminSecretKey { get; set; }
    }

    public class LoginDto
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
        public bool RememberMe { get; set; }
    }

    public class VerifyOtpDto
    {
        public required string Email { get; set; }
        public required string Otp { get; set; }
    }

    public class AuthResponseDto
    {
        public required string Token { get; set; }
        public required string Username { get; set; }
        public required string Role { get; set; }
    }

    public class ResetPasswordDto
    {
        public string Email { get; set; } = string.Empty;
        public string? Token { get; set; }
        public string NewPassword { get; set; } = string.Empty;
    }

    public class ForgotPasswordDto
    {
        public string Email { get; set; } = string.Empty;
    }
}