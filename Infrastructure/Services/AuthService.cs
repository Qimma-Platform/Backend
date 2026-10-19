using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ELearningPlatform.Core.DTOs;
using ELearningPlatform.Core.Entities;
using ELearningPlatform.Core.Enums;
using ELearningPlatform.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ELearningPlatform.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _config;
        private readonly IEmailService _emailService;

        public AuthService(IUnitOfWork unitOfWork, IConfiguration config, IEmailService emailService)
        {
            _unitOfWork = unitOfWork;
            _config = config;
            _emailService = emailService;
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterDto request)
        {
            var existingUsers = await _unitOfWork.Users.GetAllAsync();
            if (existingUsers.Any(u => u.Email == request.Email))
                throw new InvalidOperationException("A user with this email already exists.");

            // 1. Admin Secret Key Validation (Only applies to Admin role = 2)
            if ((int)request.Role == 2)
            {
                var requiredSecret = _config["AdminSecretKey"] ?? "KAAE3579";
                if (string.IsNullOrEmpty(request.AdminSecretKey) || request.AdminSecretKey != requiredSecret)
                {
                    throw new UnauthorizedAccessException("Invalid Admin Secret Key.");
                }
            }

            // 2. Generate OTP
            var otp = new Random().Next(100000, 999999).ToString();

            var user = new User
            {
                Username = request.Username,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = request.Role,
                GradeLevel = request.GradeLevel,
                IsEmailConfirmed = false,
                OtpCode = otp,
                OtpExpiry = DateTime.UtcNow.AddMinutes(15)
            };

            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.CompleteAsync();

            // Notify admins of new user registration
            try
            {
                var allUsers = await _unitOfWork.Users.GetAllAsync();
                var admins = allUsers.Where(u => (int)u.Role == 2).ToList();
                string roleStrAr = request.Role == UserRole.Instructor ? "معلم" : "طالب";
                string roleStrEn = request.Role == UserRole.Instructor ? "Instructor" : "Student";
                string notifMsg = $"NEW_USER||انضمام {roleStrAr} جديد: قام {roleStrAr} '{user.Username}' بإنشاء حساب جديد على المنصة ({user.Email})||New {roleStrEn} Joined: '{user.Username}' ({user.Email})";
                foreach (var admin in admins)
                {
                    await _unitOfWork.Notifications.AddAsync(new Notification
                    {
                        UserId = admin.Id,
                        Message = notifMsg,
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    });
                }
                await _unitOfWork.CompleteAsync();
            }
            catch { }

            // 3. Send OTP Email
            string emailBody = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px; color: #333; text-align: center;'>
                    <h2 style='color: #4F46E5;'>Welcome to Qimma!</h2>
                    <p>Your email confirmation code is:</p>
                    <h1 style='letter-spacing: 5px; color: #130E35; background: #F4F7FE; padding: 15px; border-radius: 10px; display: inline-block;'>{otp}</h1>
                    <p style='color: #777; font-size: 12px;'>This code expires in 15 minutes.</p>
                </div>";

            await _emailService.SendEmailAsync(request.Email, "Qimma - Confirm your email", emailBody);

            return new AuthResponseDto
            {
                Token = "OTP_REQUIRED",
                Username = user.Username,
                Role = ((int)user.Role).ToString()
            };
        }

        public async Task<AuthResponseDto> VerifyOtpAsync(VerifyOtpDto request)
        {
            var users = await _unitOfWork.Users.GetAllAsync();
            var user = users.FirstOrDefault(u => u.Email == request.Email);

            if (user == null || user.OtpCode != request.Otp || user.OtpExpiry < DateTime.UtcNow)
                throw new UnauthorizedAccessException("Invalid or expired OTP code.");

            user.IsEmailConfirmed = true;
            user.OtpCode = null;
            user.OtpExpiry = null;

            _unitOfWork.Users.Update(user);
            await _unitOfWork.CompleteAsync();

            return new AuthResponseDto
            {
                Token = GenerateJwtToken(user, false),
                Username = user.Username,
                Role = ((int)user.Role).ToString()
            };
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto request)
        {
            var users = await _unitOfWork.Users.GetAllAsync();
            var user = users.FirstOrDefault(u => u.Email == request.Email);

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                throw new UnauthorizedAccessException("Invalid credentials.");

            if (request.IsAdminPortal == true && user.Role != UserRole.Admin)
            {
                throw new UnauthorizedAccessException("PORTAL_ADMIN_ONLY");
            }

            if (request.IsAdminPortal != true && user.Role == UserRole.Admin)
            {
                throw new UnauthorizedAccessException("PORTAL_PUBLIC_ONLY");
            }

            if (!user.IsEmailConfirmed)
            {
                user.OtpCode = new Random().Next(100000, 999999).ToString();
                user.OtpExpiry = DateTime.UtcNow.AddMinutes(15);
                _unitOfWork.Users.Update(user);
                await _unitOfWork.CompleteAsync();

                string emailBody = $"<h2>Qimma</h2><p>Your new OTP is: <b>{user.OtpCode}</b></p>";
                await _emailService.SendEmailAsync(user.Email, "Qimma - Verify your email", emailBody);

                throw new UnauthorizedAccessException("EMAIL_NOT_CONFIRMED");
            }

            return new AuthResponseDto
            {
                Token = GenerateJwtToken(user, request.RememberMe),
                Username = user.Username,
                Role = ((int)user.Role).ToString()
            };
        }

        public async Task<(bool Success, bool IsAdmin)> ResetPasswordAsync(ResetPasswordDto request)
        {
            string email = request.Email;

            if (!string.IsNullOrWhiteSpace(request.Token))
            {
                try
                {
                    var keyString = _config["Jwt:Key"];
                    var tokenHandler = new JwtSecurityTokenHandler();
                    var validationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString!)),
                        ValidateIssuer = !string.IsNullOrEmpty(_config["Jwt:Issuer"]),
                        ValidIssuer = _config["Jwt:Issuer"],
                        ValidateAudience = !string.IsNullOrEmpty(_config["Jwt:Audience"]),
                        ValidAudience = _config["Jwt:Audience"],
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(5)
                    };

                    var principal = tokenHandler.ValidateToken(request.Token, validationParameters, out _);
                    var emailClaim = principal.FindFirst(ClaimTypes.Email)?.Value;
                    if (!string.IsNullOrEmpty(emailClaim))
                    {
                        email = emailClaim;
                    }
                }
                catch
                {
                    return (false, false);
                }
            }

            if (string.IsNullOrEmpty(email)) return (false, false);

            var users = await _unitOfWork.Users.GetAllAsync();
            var user = users.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

            if (user == null)
                return (false, false);

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

            _unitOfWork.Users.Update(user);
            await _unitOfWork.CompleteAsync();

            bool isAdmin = user.Role == UserRole.Admin || user.Role.ToString().Equals("Admin", StringComparison.OrdinalIgnoreCase);
            return (true, isAdmin);
        }

        public string GeneratePasswordResetToken(string email)
        {
            var keyString = _config["Jwt:Key"];
            if (string.IsNullOrEmpty(keyString)) throw new InvalidOperationException("JWT Key is missing.");

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Email, email),
                new Claim("purpose", "password_reset")
            };

            var users = _unitOfWork.Users.GetAllAsync().GetAwaiter().GetResult();
            var user = users.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (user != null)
            {
                claims.Add(new Claim(ClaimTypes.Role, user.Role.ToString()));
                claims.Add(new Claim("role", ((int)user.Role).ToString()));
            }

            var expiration = DateTime.UtcNow.AddHours(2);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: expiration,
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string GenerateJwtToken(User user, bool rememberMe)
        {
            var keyString = _config["Jwt:Key"];
            if (string.IsNullOrEmpty(keyString)) throw new InvalidOperationException("JWT Key is missing.");

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var roleString = ((int)user.Role).ToString();

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, roleString),
                new Claim("role", roleString)
            };

            var expiration = rememberMe ? DateTime.UtcNow.AddDays(30) : DateTime.UtcNow.AddHours(3);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: expiration,
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}