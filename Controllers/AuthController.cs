using System;
using System.Threading.Tasks;
using ELearningPlatform.Core.DTOs;
using ELearningPlatform.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELearningPlatform.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IEmailService _emailService;

        public AuthController(IAuthService authService, IEmailService emailService)
        {
            _authService = authService;
            _emailService = emailService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto request)
        {
            try
            {
                var response = await _authService.RegisterAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"REGISTER ERROR: {ex.Message} --- Inner: {ex.InnerException?.Message}");
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto request)
        {
            try
            {
                var response = await _authService.VerifyOtpAsync(request);
                return Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto request)
        {
            try
            {
                var response = await _authService.LoginAsync(request);
                return Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto model)
        {
            try
            {
                string resetLink = $"http://localhost:4200/reset-password?email={model.Email}";

                string emailBody = $@"
                    <div style='font-family: Arial, sans-serif; padding: 20px; color: #333; text-align: center;'>
                        <h2 style='color: #4F46E5;'>Fluency First - Password Reset</h2>
                        <p>You recently requested to reset your password for your account.</p>
                        <p>Click the button below to set a new password:</p>
                        <a href='{resetLink}' style='background: #4F46E5; color: white; padding: 12px 24px; text-decoration: none; border-radius: 8px; display: inline-block; margin-top: 15px; font-weight: bold;'>Reset Password</a>
                        <p style='color: #777; font-size: 12px; margin-top: 20px;'>If you did not request a password reset, please ignore this email.</p>
                    </div>";

                await _emailService.SendEmailAsync(model.Email, "Fluency First - Password Reset", emailBody);

                return Ok(new { message = "Reset link sent successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error sending email.", error = ex.Message });
            }
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto request)
        {
            try
            {
                var result = await _authService.ResetPasswordAsync(request);
                if (!result) return BadRequest(new { message = "Email not found." });

                return Ok(new { message = "Password reset successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating password.", error = ex.Message });
            }
        }
    }
}