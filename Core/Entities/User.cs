using System;
using System.Collections.Generic;
using ELearningPlatform.Core.Enums;

namespace ELearningPlatform.Core.Entities
{
    public class User
    {
        public int Id { get; set; }
        public required string Username { get; set; }
        public required string Email { get; set; }
        public required string PasswordHash { get; set; }
        public UserRole Role { get; set; }
        public int TotalXP { get; set; } = 0;
        public string? ProfileImage { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Bio { get; set; }
        public string? Specialization { get; set; }
        public string? CoverImage { get; set; }
        public decimal WalletBalance { get; set; } = 0;
        public string? GradeLevel { get; set; }

        public bool IsEmailConfirmed { get; set; } = false;
        public string? OtpCode { get; set; }
        public DateTime? OtpExpiry { get; set; }

        public ICollection<CourseEnrollment> Enrollments { get; set; } = new List<CourseEnrollment>();
        public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }
}