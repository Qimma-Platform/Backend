using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ELearningPlatform.Core.DTOs;
using ELearningPlatform.Core.Entities;
using ELearningPlatform.Core.Enums;
using ELearningPlatform.Infrastructure.Data;

namespace ELearningPlatform.Controllers
{
    [Route("api/admin")]
    [ApiController]
    [Authorize]
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
            EnsureAuditTableExists();
        }

        private void EnsureAuditTableExists()
        {
            try
            {
                _context.Database.ExecuteSqlRaw(@"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AuditLogs')
                    BEGIN
                        CREATE TABLE AuditLogs (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            AdminId INT NOT NULL,
                            AdminUsername NVARCHAR(100) NOT NULL,
                            Action NVARCHAR(50) NOT NULL,
                            Entity NVARCHAR(50) NOT NULL,
                            Details NVARCHAR(MAX) NOT NULL,
                            Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                        );
                    END
                ");

                var adminUser = _context.Users.FirstOrDefault(u => u.Email == "admin@fluencyfirst.com");
                if (adminUser == null)
                {
                    adminUser = new User
                    {
                        Username = "Super Admin",
                        Email = "admin@fluencyfirst.com",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@12345"),
                        Role = UserRole.Admin,
                        IsEmailConfirmed = true,
                        TotalXP = 0
                    };
                    _context.Users.Add(adminUser);
                    _context.SaveChanges();
                }

                if (!_context.Notifications.Any(n => n.UserId == adminUser.Id))
                {
                    _context.Notifications.AddRange(
                        new Notification
                        {
                            UserId = adminUser.Id,
                            Message = "SYSTEM||مرحباً بك في لوحة تحكم المشرف العام! تم تجهيز نظام الإشعارات الفوري.||Welcome to Admin Dashboard! Real-time notifications active.",
                            CreatedAt = DateTime.UtcNow.AddHours(-3),
                            IsRead = false
                        },
                        new Notification
                        {
                            UserId = adminUser.Id,
                            Message = "ENROLLMENT_PAID||اشتراك كورس جديد (مدفوع): قام الطالب 'عمر خالد' بالاشتراك في كورس 'English Grammar Mastery' (350 ج.م)||Paid Enrollment: Student 'Omar Khaled' enrolled in 'English Grammar Mastery' (350 EGP)",
                            CreatedAt = DateTime.UtcNow.AddHours(-1),
                            IsRead = false
                        },
                        new Notification
                        {
                            UserId = adminUser.Id,
                            Message = "NEW_USER||انضمام معلم جديد: قام المعلم 'د. أحمد محمود' بإنشاء حساب جديد على المنصة||New Instructor Joined: 'Dr. Ahmed Mahmoud'",
                            CreatedAt = DateTime.UtcNow.AddMinutes(-20),
                            IsRead = false
                        }
                    );
                    _context.SaveChanges();
                }
            }
            catch { }
        }

        private bool IsAdmin()
        {
            var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
            return role == "2" || role == "Admin";
        }

        private int GetAdminId()
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("id") ?? User.FindFirstValue(ClaimTypes.Sid);
            return int.TryParse(idClaim, out int id) ? id : 0;
        }

        private string GetAdminUsername()
        {
            return User.FindFirstValue(ClaimTypes.Name) ?? "Admin";
        }

        private async Task LogActionAsync(string action, string entity, string details)
        {
            try
            {
                var log = new AuditLog
                {
                    AdminId = GetAdminId(),
                    AdminUsername = GetAdminUsername(),
                    Action = action,
                    Entity = entity,
                    Details = details,
                    Timestamp = DateTime.UtcNow
                };
                _context.AuditLogs.Add(log);
                await _context.SaveChangesAsync();
            }
            catch { }
        }

        private async Task NotifyAdminsAsync(string message)
        {
            try
            {
                var adminIds = await _context.Users.Where(u => (int)u.Role == 2).Select(u => u.Id).ToListAsync();
                foreach (var aId in adminIds)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = aId,
                        Message = message,
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    });
                }
                await _context.SaveChangesAsync();
            }
            catch { }
        }

        // ==========================================
        // 1. DASHBOARD OVERVIEW & STATS
        // ==========================================
        [HttpGet("stats")]
        public async Task<IActionResult> GetDashboardStats([FromQuery] string? timeframe)
        {
            if (!IsAdmin()) return Forbid();

            var totalStudents = await _context.Users.CountAsync(u => (int)u.Role == 0);
            var totalInstructors = await _context.Users.CountAsync(u => (int)u.Role == 1);
            var totalCourses = await _context.Courses.CountAsync();
            var totalEnrollments = await _context.CourseEnrollments.CountAsync();

            var paidCourses = await _context.CourseEnrollments
                .Include(ce => ce.Course)
                .Where(ce => ce.Course != null && (!ce.Course.IsFree || ce.Course.Price > 0))
                .Select(ce => new { ce.Course!.Price, ce.EnrolledAt })
                .ToListAsync();

            decimal totalRevenue = paidCourses.Sum(p => p.Price);
            var totalPaidEnrollments = paidCourses.Count;
            var totalFreeEnrollments = totalEnrollments - totalPaidEnrollments;
            if (totalFreeEnrollments < 0) totalFreeEnrollments = 0;

            var paidCoursesCount = await _context.Courses.CountAsync(c => !c.IsFree && c.Price > 0);
            var freeCoursesCount = totalCourses - paidCoursesCount;

            // Generate monthly chart series (last 6 months)
            var now = DateTime.UtcNow;
            var chartLabels = new List<string>();
            var revenueChartData = new List<decimal>();
            var enrollmentChartData = new List<int>();

            var allEnrollments = await _context.CourseEnrollments
                .Include(ce => ce.Course)
                .Select(ce => new { ce.EnrolledAt, Price = ce.Course != null && !ce.Course.IsFree ? ce.Course.Price : 0 })
                .ToListAsync();

            for (int i = 5; i >= 0; i--)
            {
                var monthDate = now.AddMonths(-i);
                var label = monthDate.ToString("MMM yyyy");
                chartLabels.Add(label);

                var monthEnrollments = allEnrollments
                    .Where(e => e.EnrolledAt.Year == monthDate.Year && e.EnrolledAt.Month == monthDate.Month)
                    .ToList();

                enrollmentChartData.Add(monthEnrollments.Count);
                revenueChartData.Add(monthEnrollments.Sum(e => e.Price));
            }

            // Top performing courses
            var topCourses = await _context.Courses
                .Include(c => c.Instructor)
                .OrderByDescending(c => _context.CourseEnrollments.Count(ce => ce.CourseId == c.Id))
                .Take(5)
                .Select(c => new AdminCourseDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    CoverImageUrl = c.CoverImageUrl,
                    InstructorId = c.InstructorId,
                    InstructorName = c.Instructor != null ? c.Instructor.Username : "Unassigned",
                    IsFree = c.IsFree,
                    Price = c.Price,
                    DiscountPercentage = c.DiscountPercentage,
                    Rating = c.Rating,
                    TotalRatingsCount = c.TotalRatingsCount,
                    EnrolledStudentsCount = _context.CourseEnrollments.Count(ce => ce.CourseId == c.Id),
                    ModulesCount = _context.Modules.Count(m => m.CourseId == c.Id)
                }).ToListAsync();

            // Top instructors
            var topInstructors = await _context.Users
                .Where(u => (int)u.Role == 1)
                .OrderByDescending(u => _context.Courses.Where(c => c.InstructorId == u.Id).SelectMany(c => c.Enrollments).Count())
                .Take(5)
                .Select(u => new AdminUserDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    Email = u.Email,
                    ProfileImage = u.ProfileImage,
                    Specialization = u.Specialization,
                    Role = (int)u.Role,
                    RoleName = "Instructor",
                    AssignedCoursesCount = _context.Courses.Count(c => c.InstructorId == u.Id),
                    EnrolledCoursesCount = _context.Courses.Where(c => c.InstructorId == u.Id).SelectMany(c => c.Enrollments).Count(),
                    IsEmailConfirmed = u.IsEmailConfirmed
                }).ToListAsync();

            List<AdminAuditLogDto> recentActivities = new List<AdminAuditLogDto>();
            try
            {
                recentActivities = await _context.AuditLogs
                    .OrderByDescending(l => l.Timestamp)
                    .Take(10)
                    .Select(l => new AdminAuditLogDto
                    {
                        Id = l.Id,
                        AdminId = l.AdminId,
                        AdminUsername = l.AdminUsername,
                        Action = l.Action,
                        Entity = l.Entity,
                        Details = l.Details,
                        Timestamp = l.Timestamp
                    }).ToListAsync();
            }
            catch { }

            var stats = new AdminDashboardStatsDto
            {
                TotalStudents = totalStudents,
                TotalInstructors = totalInstructors,
                TotalCourses = totalCourses,
                TotalEnrollments = totalEnrollments,
                TotalRevenue = totalRevenue,
                TotalPaidEnrollments = totalPaidEnrollments,
                TotalFreeEnrollments = totalFreeEnrollments,
                PaidCoursesCount = paidCoursesCount,
                FreeCoursesCount = freeCoursesCount,
                ChartLabels = chartLabels,
                RevenueChartData = revenueChartData,
                EnrollmentChartData = enrollmentChartData,
                TopCourses = topCourses,
                TopInstructors = topInstructors,
                RecentActivities = recentActivities
            };

            return Ok(stats);
        }

        // ==========================================
        // 1.1 PAID STUDENTS & REVENUE RECORDS
        // ==========================================
        [HttpGet("paid-students")]
        public async Task<IActionResult> GetPaidStudents([FromQuery] string? search)
        {
            if (!IsAdmin()) return Forbid();

            var query = _context.CourseEnrollments
                .Include(ce => ce.Student)
                .Include(ce => ce.Course)
                .ThenInclude(c => c!.Instructor)
                .Where(ce => ce.Course != null && (!ce.Course.IsFree || ce.Course.Price > 0));

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(ce => 
                    (ce.Student != null && (ce.Student.Username.ToLower().Contains(s) || ce.Student.Email.ToLower().Contains(s) || (ce.Student.PhoneNumber != null && ce.Student.PhoneNumber.Contains(s)))) ||
                    (ce.Course != null && ce.Course.Title.ToLower().Contains(s)));
            }

            var list = await query
                .OrderByDescending(ce => ce.EnrolledAt)
                .Select(ce => new AdminPaidStudentDto
                {
                    StudentId = ce.StudentId,
                    StudentName = ce.Student != null ? ce.Student.Username : "Student",
                    Email = ce.Student != null ? ce.Student.Email : "",
                    PhoneNumber = ce.Student != null ? ce.Student.PhoneNumber : "",
                    ProfileImage = ce.Student != null ? ce.Student.ProfileImage : null,
                    CourseId = ce.CourseId,
                    CourseTitle = ce.Course != null ? ce.Course.Title : "Course",
                    InstructorName = (ce.Course != null && ce.Course.Instructor != null) ? ce.Course.Instructor.Username : "Instructor",
                    AmountPaid = ce.Course != null ? ce.Course.Price : 0,
                    EnrolledAt = ce.EnrolledAt,
                    PaymentStatus = "Completed"
                }).ToListAsync();

            list.ForEach(p => p.EnrolledAt = DateTime.SpecifyKind(p.EnrolledAt, DateTimeKind.Utc));

            return Ok(list);
        }

        // ==========================================
        // 1.2 ADMIN ACCOUNT PROFILE
        // ==========================================
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            if (!IsAdmin()) return Forbid();

            var adminId = GetAdminId();
            var user = await _context.Users.FindAsync(adminId);
            if (user == null)
            {
                // Fallback to first admin
                user = await _context.Users.FirstOrDefaultAsync(u => (int)u.Role == 2);
            }
            if (user == null) return NotFound();

            return Ok(new AdminProfileDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                RoleName = "Admin"
            });
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] AdminUpdateProfileDto dto)
        {
            if (!IsAdmin()) return Forbid();

            var adminId = GetAdminId();
            var user = await _context.Users.FindAsync(adminId);
            if (user == null)
            {
                user = await _context.Users.FirstOrDefaultAsync(u => (int)u.Role == 2);
            }
            if (user == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(dto.Username)) user.Username = dto.Username.Trim();
            user.PhoneNumber = dto.PhoneNumber?.Trim();

            if (!string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword.Trim());
            }

            await _context.SaveChangesAsync();
            await LogActionAsync("UPDATE", "Profile", $"Admin updated their profile credentials ({user.Username})");

            return Ok(new { message = "Profile updated successfully.", username = user.Username });
        }

        // ==========================================
        // 2. STUDENT MANAGEMENT (CRUD)
        // ==========================================
        [HttpGet("students")]
        public async Task<IActionResult> GetStudents([FromQuery] string? search)
        {
            if (!IsAdmin()) return Forbid();

            var query = _context.Users
                .AsNoTracking()
                .Where(u => (int)u.Role == 0);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(u => u.Username.ToLower().Contains(s) || u.Email.ToLower().Contains(s) || (u.PhoneNumber != null && u.PhoneNumber.Contains(s)));
            }

            var students = await query
                .OrderByDescending(u => u.Id)
                .Select(u => new AdminUserDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    Email = u.Email,
                    PhoneNumber = u.PhoneNumber,
                    ProfileImage = u.ProfileImage,
                    Role = (int)u.Role,
                    RoleName = "Student",
                    TotalXP = u.TotalXP,
                    EnrolledCoursesCount = _context.CourseEnrollments.Count(ce => ce.StudentId == u.Id),
                    IsEmailConfirmed = u.IsEmailConfirmed
                }).ToListAsync();

            return Ok(students);
        }

        [HttpPost("students")]
        public async Task<IActionResult> CreateStudent([FromBody] AdminCreateUserDto dto)
        {
            if (!IsAdmin()) return Forbid();

            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(new { message = "Username, Email, and Password are required." });

            var emailExists = await _context.Users.AnyAsync(u => u.Email.ToLower() == dto.Email.Trim().ToLower());
            if (emailExists)
                return BadRequest(new { message = "Email already registered." });

            var student = new User
            {
                Username = dto.Username.Trim(),
                Email = dto.Email.Trim().ToLower(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                PhoneNumber = dto.PhoneNumber?.Trim(),
                Role = UserRole.Student,
                IsEmailConfirmed = true,
                TotalXP = 0
            };

            _context.Users.Add(student);
            await _context.SaveChangesAsync();

            await LogActionAsync("CREATE", "Student", $"Created student '{student.Username}' ({student.Email})");
            await NotifyAdminsAsync($"NEW_USER||انضمام طالب جديد: تم إنشاء حساب للطالب '{student.Username}' ({student.Email}) بواسطة الإدارة||New Student Joined: '{student.Username}' ({student.Email})");

            return Ok(new { message = "Student created successfully.", id = student.Id });
        }

        [HttpPut("students/{id}")]
        public async Task<IActionResult> UpdateStudent(int id, [FromBody] AdminUpdateUserDto dto)
        {
            if (!IsAdmin()) return Forbid();

            var student = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && (int)u.Role == 0);
            if (student == null) return NotFound(new { message = "Student not found." });

            if (!string.IsNullOrWhiteSpace(dto.Username)) student.Username = dto.Username.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Email))
            {
                var emailExists = await _context.Users.AnyAsync(u => u.Email.ToLower() == dto.Email.Trim().ToLower() && u.Id != id);
                if (emailExists) return BadRequest(new { message = "Email already in use." });
                student.Email = dto.Email.Trim().ToLower();
            }

            student.PhoneNumber = dto.PhoneNumber?.Trim();
            if (dto.TotalXP.HasValue) student.TotalXP = dto.TotalXP.Value;

            if (!string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                student.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            }

            await _context.SaveChangesAsync();
            await LogActionAsync("UPDATE", "Student", $"Updated student '{student.Username}' (ID: {student.Id})");

            return Ok(new { message = "Student updated successfully." });
        }

        [HttpDelete("students/{id}")]
        public async Task<IActionResult> DeleteStudent(int id)
        {
            if (!IsAdmin()) return Forbid();

            var student = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && (int)u.Role == 0);
            if (student == null) return NotFound(new { message = "Student not found." });

            // Remove related enrollments, submissions, etc.
            var enrollments = _context.CourseEnrollments.Where(ce => ce.StudentId == id);
            _context.CourseEnrollments.RemoveRange(enrollments);

            var submissions = _context.Submissions.Where(s => s.StudentId == id);
            _context.Submissions.RemoveRange(submissions);

            var quizAttempts = _context.QuizAttempts.Where(qa => qa.StudentId == id);
            _context.QuizAttempts.RemoveRange(quizAttempts);

            var assignmentSubs = _context.AssignmentSubmissions.Where(a => a.StudentId == id);
            _context.AssignmentSubmissions.RemoveRange(assignmentSubs);

            _context.Users.Remove(student);
            await _context.SaveChangesAsync();

            await LogActionAsync("DELETE", "Student", $"Deleted student '{student.Username}' (ID: {id})");

            return Ok(new { message = "Student deleted successfully." });
        }

        // ==========================================
        // 3. INSTRUCTOR MANAGEMENT (CRUD)
        // ==========================================
        [HttpGet("instructors")]
        public async Task<IActionResult> GetInstructors([FromQuery] string? search)
        {
            if (!IsAdmin()) return Forbid();

            var query = _context.Users
                .AsNoTracking()
                .Where(u => (int)u.Role == 1);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(u => u.Username.ToLower().Contains(s) || u.Email.ToLower().Contains(s) || (u.Specialization != null && u.Specialization.ToLower().Contains(s)));
            }

            var instructors = await query
                .OrderByDescending(u => u.Id)
                .Select(u => new AdminUserDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    Email = u.Email,
                    PhoneNumber = u.PhoneNumber,
                    ProfileImage = u.ProfileImage,
                    Specialization = u.Specialization,
                    Bio = u.Bio,
                    Role = (int)u.Role,
                    RoleName = "Instructor",
                    AssignedCoursesCount = _context.Courses.Count(c => c.InstructorId == u.Id),
                    IsEmailConfirmed = u.IsEmailConfirmed
                }).ToListAsync();

            return Ok(instructors);
        }

        [HttpPost("instructors")]
        public async Task<IActionResult> CreateInstructor([FromBody] AdminCreateUserDto dto)
        {
            if (!IsAdmin()) return Forbid();

            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(new { message = "Username, Email, and Password are required." });

            var emailExists = await _context.Users.AnyAsync(u => u.Email.ToLower() == dto.Email.Trim().ToLower());
            if (emailExists)
                return BadRequest(new { message = "Email already registered." });

            var instructor = new User
            {
                Username = dto.Username.Trim(),
                Email = dto.Email.Trim().ToLower(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                PhoneNumber = dto.PhoneNumber?.Trim(),
                Specialization = dto.Specialization?.Trim(),
                Bio = dto.Bio?.Trim(),
                Role = UserRole.Instructor,
                IsEmailConfirmed = true
            };

            _context.Users.Add(instructor);
            await _context.SaveChangesAsync();

            await LogActionAsync("CREATE", "Instructor", $"Created instructor '{instructor.Username}' ({instructor.Email})");
            await NotifyAdminsAsync($"NEW_USER||انضمام معلم جديد: تم إنشاء حساب للمعلم '{instructor.Username}' ({instructor.Email}) بواسطة الإدارة||New Instructor Joined: '{instructor.Username}' ({instructor.Email})");

            return Ok(new { message = "Instructor created successfully.", id = instructor.Id });
        }

        [HttpPut("instructors/{id}")]
        public async Task<IActionResult> UpdateInstructor(int id, [FromBody] AdminUpdateUserDto dto)
        {
            if (!IsAdmin()) return Forbid();

            var instructor = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && (int)u.Role == 1);
            if (instructor == null) return NotFound(new { message = "Instructor not found." });

            if (!string.IsNullOrWhiteSpace(dto.Username)) instructor.Username = dto.Username.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Email))
            {
                var emailExists = await _context.Users.AnyAsync(u => u.Email.ToLower() == dto.Email.Trim().ToLower() && u.Id != id);
                if (emailExists) return BadRequest(new { message = "Email already in use." });
                instructor.Email = dto.Email.Trim().ToLower();
            }

            instructor.PhoneNumber = dto.PhoneNumber?.Trim();
            instructor.Specialization = dto.Specialization?.Trim();
            instructor.Bio = dto.Bio?.Trim();

            if (!string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                instructor.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            }

            await _context.SaveChangesAsync();
            await LogActionAsync("UPDATE", "Instructor", $"Updated instructor '{instructor.Username}' (ID: {instructor.Id})");

            return Ok(new { message = "Instructor updated successfully." });
        }

        [HttpDelete("instructors/{id}")]
        public async Task<IActionResult> DeleteInstructor(int id)
        {
            if (!IsAdmin()) return Forbid();

            var instructor = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && (int)u.Role == 1);
            if (instructor == null) return NotFound(new { message = "Instructor not found." });

            var assignedCoursesCount = await _context.Courses.CountAsync(c => c.InstructorId == id);
            if (assignedCoursesCount > 0)
            {
                return BadRequest(new { message = $"Cannot delete instructor because they are assigned to {assignedCoursesCount} courses. Please reassign the courses first." });
            }

            _context.Users.Remove(instructor);
            await _context.SaveChangesAsync();

            await LogActionAsync("DELETE", "Instructor", $"Deleted instructor '{instructor.Username}' (ID: {id})");

            return Ok(new { message = "Instructor deleted successfully." });
        }

        // ==========================================
        // 4. COURSE MANAGEMENT (CRUD & ASSIGNMENT)
        // ==========================================
        [HttpGet("courses")]
        public async Task<IActionResult> GetCourses([FromQuery] string? search)
        {
            if (!IsAdmin()) return Forbid();

            var query = _context.Courses
                .Include(c => c.Instructor)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(c => c.Title.ToLower().Contains(s) || (c.Instructor != null && c.Instructor.Username.ToLower().Contains(s)));
            }

            var courses = await query
                .OrderByDescending(c => c.Id)
                .Select(c => new AdminCourseDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    Description = c.Description,
                    CoverImageUrl = c.CoverImageUrl,
                    InstructorId = c.InstructorId,
                    InstructorName = c.Instructor != null ? c.Instructor.Username : "Unassigned",
                    IsFree = c.IsFree,
                    Price = c.Price,
                    DiscountPercentage = c.DiscountPercentage,
                    Rating = c.Rating,
                    TotalRatingsCount = c.TotalRatingsCount,
                    EnrolledStudentsCount = _context.CourseEnrollments.Count(ce => ce.CourseId == c.Id),
                    ModulesCount = _context.Modules.Count(m => m.CourseId == c.Id)
                }).ToListAsync();

            return Ok(courses);
        }

        [HttpPost("courses")]
        public async Task<IActionResult> CreateCourse([FromBody] AdminCreateCourseDto dto)
        {
            if (!IsAdmin()) return Forbid();

            if (string.IsNullOrWhiteSpace(dto.Title))
                return BadRequest(new { message = "Course title is required." });

            var instructor = await _context.Users.FirstOrDefaultAsync(u => u.Id == dto.InstructorId && (int)u.Role == 1);
            if (instructor == null)
                return BadRequest(new { message = "Selected instructor does not exist." });

            var course = new Course
            {
                Title = dto.Title.Trim(),
                Description = dto.Description?.Trim(),
                CoverImageUrl = dto.CoverImageUrl,
                InstructorId = dto.InstructorId,
                IsFree = dto.IsFree,
                Price = dto.IsFree ? 0 : dto.Price,
                DiscountPercentage = dto.DiscountPercentage
            };

            _context.Courses.Add(course);
            await _context.SaveChangesAsync();

            await LogActionAsync("CREATE", "Course", $"Created course '{course.Title}' assigned to '{instructor.Username}'");

            return Ok(new { message = "Course created successfully.", id = course.Id });
        }

        [HttpPut("courses/{id}")]
        public async Task<IActionResult> UpdateCourse(int id, [FromBody] AdminUpdateCourseDto dto)
        {
            if (!IsAdmin()) return Forbid();

            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound(new { message = "Course not found." });

            if (!string.IsNullOrWhiteSpace(dto.Title)) course.Title = dto.Title.Trim();
            course.Description = dto.Description?.Trim();
            if (!string.IsNullOrWhiteSpace(dto.CoverImageUrl)) course.CoverImageUrl = dto.CoverImageUrl;

            if (dto.InstructorId.HasValue && dto.InstructorId.Value != course.InstructorId)
            {
                var instructor = await _context.Users.FirstOrDefaultAsync(u => u.Id == dto.InstructorId.Value && (int)u.Role == 1);
                if (instructor == null) return BadRequest(new { message = "Instructor not found." });
                course.InstructorId = dto.InstructorId.Value;
            }

            course.IsFree = dto.IsFree;
            course.Price = dto.IsFree ? 0 : dto.Price;
            course.DiscountPercentage = dto.DiscountPercentage;

            await _context.SaveChangesAsync();
            await LogActionAsync("UPDATE", "Course", $"Updated course '{course.Title}' (ID: {course.Id})");

            return Ok(new { message = "Course updated successfully." });
        }

        [HttpPut("courses/{id}/assign-instructor")]
        public async Task<IActionResult> AssignInstructor(int id, [FromBody] AdminAssignInstructorDto dto)
        {
            if (!IsAdmin()) return Forbid();

            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound(new { message = "Course not found." });

            var instructor = await _context.Users.FirstOrDefaultAsync(u => u.Id == dto.InstructorId && (int)u.Role == 1);
            if (instructor == null) return BadRequest(new { message = "Instructor not found." });

            course.InstructorId = dto.InstructorId;
            await _context.SaveChangesAsync();

            await LogActionAsync("ASSIGN", "Course", $"Assigned course '{course.Title}' to instructor '{instructor.Username}'");

            return Ok(new { message = $"Course assigned to instructor {instructor.Username} successfully." });
        }

        [HttpDelete("courses/{id}")]
        public async Task<IActionResult> DeleteCourse(int id)
        {
            if (!IsAdmin()) return Forbid();

            var course = await _context.Courses
                .Include(c => c.Modules)
                .ThenInclude(m => m.Tasks)
                .Include(c => c.Enrollments)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null) return NotFound(new { message = "Course not found." });

            // Remove lessons, assignments, ratings, etc.
            var lessons = _context.Lessons.Where(l => l.CourseId == id);
            _context.Lessons.RemoveRange(lessons);

            var ratings = _context.CourseRatings.Where(r => r.CourseId == id);
            _context.CourseRatings.RemoveRange(ratings);

            var assignments = _context.Assignments.Where(a => a.CourseId == id);
            _context.Assignments.RemoveRange(assignments);

            _context.Courses.Remove(course);
            await _context.SaveChangesAsync();

            await LogActionAsync("DELETE", "Course", $"Deleted course '{course.Title}' (ID: {id})");

            return Ok(new { message = "Course deleted successfully." });
        }

        // ==========================================
        // 5. COURSE ENROLLEES MANAGEMENT
        // ==========================================
        [HttpGet("courses/{courseId}/enrollees")]
        public async Task<IActionResult> GetCourseEnrollees(int courseId)
        {
            if (!IsAdmin()) return Forbid();

            var course = await _context.Courses.FindAsync(courseId);
            if (course == null) return NotFound(new { message = "Course not found." });

            var enrollees = await _context.CourseEnrollments
                .Include(ce => ce.Student)
                .Where(ce => ce.CourseId == courseId && ce.Student != null)
                .OrderByDescending(ce => ce.EnrolledAt)
                .Select(ce => new AdminCourseEnrolleeDto
                {
                    StudentId = ce.StudentId,
                    StudentName = ce.Student!.Username,
                    Email = ce.Student.Email,
                    PhoneNumber = ce.Student.PhoneNumber,
                    ProfileImage = ce.Student.ProfileImage,
                    TotalXP = ce.Student.TotalXP,
                    EnrolledAt = ce.EnrolledAt
                }).ToListAsync();

            enrollees.ForEach(e => e.EnrolledAt = DateTime.SpecifyKind(e.EnrolledAt, DateTimeKind.Utc));

            return Ok(enrollees);
        }

        [HttpPost("courses/{courseId}/enroll")]
        public async Task<IActionResult> EnrollStudent(int courseId, [FromBody] AdminEnrollStudentDto dto)
        {
            if (!IsAdmin()) return Forbid();

            var course = await _context.Courses.FindAsync(courseId);
            if (course == null) return NotFound(new { message = "Course not found." });

            var student = await _context.Users.FirstOrDefaultAsync(u => u.Id == dto.StudentId && (int)u.Role == 0);
            if (student == null) return NotFound(new { message = "Student not found." });

            var alreadyEnrolled = await _context.CourseEnrollments.AnyAsync(ce => ce.CourseId == courseId && ce.StudentId == dto.StudentId);
            if (alreadyEnrolled) return BadRequest(new { message = "Student is already enrolled in this course." });

            var enrollment = new CourseEnrollment
            {
                CourseId = courseId,
                StudentId = dto.StudentId,
                EnrolledAt = DateTime.UtcNow
            };

            _context.CourseEnrollments.Add(enrollment);
            await _context.SaveChangesAsync();

            await LogActionAsync("ENROLL", "Enrollment", $"Enrolled student '{student.Username}' into course '{course.Title}'");

            bool isFree = course.IsFree || course.Price == 0;
            string typeStrAr = isFree ? "مجاني" : $"مدفوع ({course.Price} ج.م)";
            string typeStrEn = isFree ? "Free" : $"Paid ({course.Price} EGP)";
            await NotifyAdminsAsync($"ENROLLMENT||اشتراك جديد ({typeStrAr}): تم تسجيل الطالب '{student.Username}' في كورس '{course.Title}' بواسطة المشرف||Course Enrollment ({typeStrEn}): Student '{student.Username}' enrolled in '{course.Title}' by Admin");

            return Ok(new { message = "Student enrolled successfully." });
        }

        [HttpDelete("courses/{courseId}/enroll/{studentId}")]
        public async Task<IActionResult> UnenrollStudent(int courseId, int studentId)
        {
            if (!IsAdmin()) return Forbid();

            var enrollment = await _context.CourseEnrollments
                .Include(ce => ce.Student)
                .Include(ce => ce.Course)
                .FirstOrDefaultAsync(ce => ce.CourseId == courseId && ce.StudentId == studentId);

            if (enrollment == null) return NotFound(new { message = "Enrollment not found." });

            string studentName = enrollment.Student?.Username ?? studentId.ToString();
            string courseTitle = enrollment.Course?.Title ?? courseId.ToString();

            _context.CourseEnrollments.Remove(enrollment);
            await _context.SaveChangesAsync();

            await LogActionAsync("UNENROLL", "Enrollment", $"Removed student '{studentName}' from course '{courseTitle}'");

            return Ok(new { message = "Student unenrolled successfully." });
        }

        // ==========================================
        // 6. AUDIT LOGS
        // ==========================================
        [HttpGet("logs")]
        public async Task<IActionResult> GetAuditLogs([FromQuery] int limit = 50)
        {
            if (!IsAdmin()) return Forbid();

            var logs = await _context.AuditLogs
                .OrderByDescending(l => l.Timestamp)
                .Take(limit)
                .Select(l => new AdminAuditLogDto
                {
                    Id = l.Id,
                    AdminId = l.AdminId,
                    AdminUsername = l.AdminUsername,
                    Action = l.Action,
                    Entity = l.Entity,
                    Details = l.Details,
                    Timestamp = l.Timestamp
                }).ToListAsync();

            logs.ForEach(l => l.Timestamp = DateTime.SpecifyKind(l.Timestamp, DateTimeKind.Utc));

            return Ok(logs);
        }

        // ==========================================
        // 7. ADMIN NOTIFICATIONS (Req 5)
        // ==========================================
        [HttpGet("notifications")]
        public async Task<IActionResult> GetAdminNotifications()
        {
            if (!IsAdmin()) return Forbid();

            var adminId = GetAdminId();
            var notifications = await _context.Notifications
                .Where(n => n.UserId == adminId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(50)
                .Select(n => new AdminNotificationDto
                {
                    Id = n.Id,
                    Message = n.Message,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                }).ToListAsync();

            notifications.ForEach(n => n.CreatedAt = DateTime.SpecifyKind(n.CreatedAt, DateTimeKind.Utc));

            return Ok(notifications);
        }

        [HttpPost("notifications/{id}/read")]
        public async Task<IActionResult> MarkNotificationAsRead(int id)
        {
            if (!IsAdmin()) return Forbid();

            var adminId = GetAdminId();
            var notif = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == adminId);
            if (notif != null)
            {
                notif.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "Marked as read." });
        }

        [HttpPost("notifications/read-all")]
        public async Task<IActionResult> MarkAllNotificationsAsRead()
        {
            if (!IsAdmin()) return Forbid();

            var adminId = GetAdminId();
            var unread = await _context.Notifications.Where(n => n.UserId == adminId && !n.IsRead).ToListAsync();
            foreach (var n in unread)
            {
                n.IsRead = true;
            }
            await _context.SaveChangesAsync();

            return Ok(new { message = "All notifications marked as read." });
        }

        [HttpDelete("notifications/{id}")]
        public async Task<IActionResult> DeleteAdminNotification(int id)
        {
            if (!IsAdmin()) return Forbid();

            var adminId = GetAdminId();
            var notif = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == adminId);
            if (notif != null)
            {
                _context.Notifications.Remove(notif);
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "Notification deleted." });
        }

        [HttpDelete("notifications/all")]
        public async Task<IActionResult> DeleteAllAdminNotifications()
        {
            if (!IsAdmin()) return Forbid();

            var adminId = GetAdminId();
            var notifs = await _context.Notifications.Where(n => n.UserId == adminId).ToListAsync();
            if (notifs.Any())
            {
                _context.Notifications.RemoveRange(notifs);
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "All notifications cleared." });
        }
    }
}
