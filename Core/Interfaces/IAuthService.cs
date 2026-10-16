using System.Threading.Tasks;
using ELearningPlatform.Core.DTOs;

namespace ELearningPlatform.Core.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDto> RegisterAsync(RegisterDto request);
        Task<AuthResponseDto> LoginAsync(LoginDto request);
        Task<AuthResponseDto> VerifyOtpAsync(VerifyOtpDto request);
        Task<(bool Success, bool IsAdmin)> ResetPasswordAsync(ResetPasswordDto request);
        string GeneratePasswordResetToken(string email);
    }
}