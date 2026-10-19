using System;
using System.Collections.Generic;

namespace ELearningPlatform.Core.DTOs
{
    public class AdminDashboardStatsDto
    {
        public int TotalStudents { get; set; }
        public int TotalInstructors { get; set; }
        public int TotalCourses { get; set; }
        public int TotalEnrollments { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TotalPaidEnrollments { get; set; }
        public int TotalFreeEnrollments { get; set; }
        public int PaidCoursesCount { get; set; }
        public int FreeCoursesCount { get; set; }
        public List<string> ChartLabels { get; set; } = new List<string>();
        public List<decimal> RevenueChartData { get; set; } = new List<decimal>();
        public List<int> EnrollmentChartData { get; set; } = new List<int>();
        public List<AdminCourseDto> TopCourses { get; set; } = new List<AdminCourseDto>();
        public List<AdminUserDto> TopInstructors { get; set; } = new List<AdminUserDto>();
        public List<AdminAuditLogDto> RecentActivities { get; set; } = new List<AdminAuditLogDto>();
    }

    public class AdminUserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? ProfileImage { get; set; }
        public string? Specialization { get; set; }
        public string? Bio { get; set; }
        public string? GradeLevel { get; set; }
        public int Role { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public int TotalXP { get; set; }
        public int EnrolledCoursesCount { get; set; }
        public int AssignedCoursesCount { get; set; }
        public bool IsEmailConfirmed { get; set; }
    }

    public class AdminCreateUserDto
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Specialization { get; set; }
        public string? Bio { get; set; }
        public string? GradeLevel { get; set; }
        public int Role { get; set; } // 0 = Student, 1 = Instructor
    }

    public class AdminUpdateUserDto
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Specialization { get; set; }
        public string? Bio { get; set; }
        public string? GradeLevel { get; set; }
        public int? TotalXP { get; set; }
        public string? NewPassword { get; set; }
    }

    public class AdminCourseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? CoverImageUrl { get; set; }
        public int InstructorId { get; set; }
        public string InstructorName { get; set; } = string.Empty;
        public bool IsFree { get; set; }
        public decimal Price { get; set; }
        public decimal DiscountPercentage { get; set; }
        public decimal? Rating { get; set; }
        public int TotalRatingsCount { get; set; }
        public int EnrolledStudentsCount { get; set; }
        public int ModulesCount { get; set; }
    }

    public class AdminCreateCourseDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? CoverImageUrl { get; set; }
        public int InstructorId { get; set; }
        public bool IsFree { get; set; } = true;
        public decimal Price { get; set; } = 0;
        public decimal DiscountPercentage { get; set; } = 0;
    }

    public class AdminUpdateCourseDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? CoverImageUrl { get; set; }
        public int? InstructorId { get; set; }
        public bool IsFree { get; set; }
        public decimal Price { get; set; }
        public decimal DiscountPercentage { get; set; }
    }

    public class AdminAssignInstructorDto
    {
        public int InstructorId { get; set; }
    }

    public class AdminCourseEnrolleeDto
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? ProfileImage { get; set; }
        public int TotalXP { get; set; }
        public DateTime EnrolledAt { get; set; }
    }

    public class AdminEnrollStudentDto
    {
        public int StudentId { get; set; }
    }

    public class AdminAuditLogDto
    {
        public int Id { get; set; }
        public int AdminId { get; set; }
        public string AdminUsername { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Entity { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public class AdminPaidStudentDto
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? ProfileImage { get; set; }
        public int CourseId { get; set; }
        public string CourseTitle { get; set; } = string.Empty;
        public string InstructorName { get; set; } = string.Empty;
        public decimal AmountPaid { get; set; }
        public DateTime EnrolledAt { get; set; }
        public string PaymentStatus { get; set; } = "Completed";
    }

    public class AdminProfileDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? ProfileImage { get; set; }
        public string RoleName { get; set; } = "Admin";
    }

    public class AdminUpdateProfileDto
    {
        public string Username { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? ProfileImage { get; set; }
        public string? CurrentPassword { get; set; }
        public string? NewPassword { get; set; }
    }

    public class AdminNotificationDto
    {
        public int Id { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AdminTeacherEarningsDto
    {
        public int TeacherId { get; set; }
        public string TeacherName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? ProfileImage { get; set; }
        public int CoursesCount { get; set; }
        public int PaidStudentsCount { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal TeacherDues { get; set; }
        public decimal PlatformDues { get; set; }
        public decimal WalletBalance { get; set; }
    }
}
