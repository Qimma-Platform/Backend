using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ELearningPlatform.Infrastructure.Data;
using ELearningPlatform.Core.Entities;

namespace ELearningPlatform.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public UserController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("profile")]
        [Authorize]
        public async Task<IActionResult> GetProfile()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            return Ok(new
            {
                user.Id,
                user.Username,
                user.Email,
                user.PhoneNumber,
                user.Bio,
                user.ProfileImage,
                CoverImage = string.IsNullOrWhiteSpace(user.CoverImage) ? "/assets/images/default-cover.jpg" : user.CoverImage,
                user.TotalXP,
                user.Role,
                user.Specialization
            });
        }

        [HttpPut("profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto model)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            user.Username = model.Username;
            user.PhoneNumber = model.PhoneNumber;
            user.Bio = model.Bio;
            user.Specialization = model.Specialization;

            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword.Trim());
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Profile updated" });
        }

        [HttpPost("upload-avatar")]
        [Authorize]
        public async Task<IActionResult> UploadAvatar(IFormFile file)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            if (file == null || file.Length == 0) return BadRequest(new { message = "No file uploaded." });
            if (file.Length > 1024 * 1024)
            {
                return BadRequest(new { message = "حجم الصورة يتجاوز الحد الأقصى المسموح به (1 ميجابايت) / Image size exceeds the 1 MB limit." });
            }

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "avatars");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            user.ProfileImage = $"/uploads/avatars/{uniqueFileName}";
            await _context.SaveChangesAsync();

            return Ok(new { profileImage = user.ProfileImage });
        }

        [HttpPost("upload-cover")]
        [Authorize]
        public async Task<IActionResult> UploadCover(IFormFile file)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            if (file == null || file.Length == 0) return BadRequest(new { message = "No file uploaded." });
            if (file.Length > 1024 * 1024)
            {
                return BadRequest(new { message = "حجم صورة الغلاف يتجاوز الحد الأقصى المسموح به (1 ميجابايت) / Cover image size exceeds the 1 MB limit." });
            }

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "covers");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            user.CoverImage = $"/uploads/covers/{uniqueFileName}";
            await _context.SaveChangesAsync();

            return Ok(new { coverImage = user.CoverImage });
        }

        [HttpGet("teacher/{id}")]
        [Authorize]
        public async Task<IActionResult> GetTeacherProfile(int id)
        {
            var teacher = await _context.Users
                .Where(u => u.Id == id)
                .Select(u => new {
                    u.Id,
                    u.Username,
                    u.ProfileImage,
                    CoverImage = string.IsNullOrWhiteSpace(u.CoverImage) ? "/assets/images/default-cover.jpg" : u.CoverImage,
                    u.Bio,
                    u.Specialization
                }).FirstOrDefaultAsync();

            if (teacher == null) return NotFound("Teacher not found");

            var courses = await _context.Courses
                .Where(c => c.InstructorId == id)
                .Select(c => new {
                    id = c.Id,
                    title = c.Title,
                    description = c.Description,
                    coverImageUrl = string.IsNullOrWhiteSpace(c.CoverImageUrl) ? "/assets/images/default-cover.jpg" : c.CoverImageUrl,
                    price = c.Price,
                    isFree = c.IsFree,
                    discountPercentage = c.DiscountPercentage,
                    rating = c.Rating,
                    totalRatingsCount = c.TotalRatingsCount,
                    instructorName = teacher.Username,
                    instructorId = teacher.Id
                }).ToListAsync();

            var teacherCourseIds = courses.Select(c => c.id).ToList();
            var ratings = await _context.CourseRatings
                .Where(r => teacherCourseIds.Contains(r.CourseId))
                .ToListAsync();

            double avgRating = 0;
            int totalRatingsCount = 0;
            if (ratings.Any())
            {
                avgRating = Math.Round(ratings.Average(r => r.Rating), 1);
                totalRatingsCount = ratings.Count;
            }
            else
            {
                var ratedCourses = courses.Where(c => c.rating.HasValue && c.rating.Value > 0).ToList();
                if (ratedCourses.Any())
                {
                    avgRating = Math.Round((double)ratedCourses.Average(c => c.rating!.Value), 1);
                    totalRatingsCount = ratedCourses.Sum(c => c.totalRatingsCount);
                }
            }

            return Ok(new { teacher, courses, rating = avgRating, totalRatingsCount });
        }

        [HttpGet("teachers")]
        [Authorize]
        public async Task<IActionResult> GetAllTeachers()
        {
            var users = await _context.Users.ToListAsync();

            var teachers = users
                .Where(u => u.Role.ToString() == "Teacher" || ((int)u.Role) == 1)
                .Select(u => new {
                    u.Id,
                    u.Username,
                    u.ProfileImage,
                    u.Specialization
                }).ToList();

            return Ok(teachers);
        }

        [HttpGet("students")]
        [Authorize]
        public async Task<IActionResult> GetAllStudents()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var roleString = User.FindFirstValue(ClaimTypes.Role);

            if (int.TryParse(userIdString, out int teacherId) && (roleString == "Teacher" || roleString == "1"))
            {
                var teacherCourseIds = await _context.Courses
                    .Where(c => c.InstructorId == teacherId)
                    .Select(c => c.Id)
                    .ToListAsync();

                var enrolledStudentIds = await _context.CourseEnrollments
                    .Where(ce => teacherCourseIds.Contains(ce.CourseId))
                    .Select(ce => ce.StudentId)
                    .Distinct()
                    .ToListAsync();

                var studentsForTeacher = await _context.Users
                    .AsNoTracking()
                    .Where(u => enrolledStudentIds.Contains(u.Id))
                    .Select(u => new {
                        u.Id,
                        name = u.Username,
                        u.Email,
                        phoneNumber = u.PhoneNumber,
                        u.ProfileImage,
                        u.TotalXP,
                        enrolledCoursesCount = _context.CourseEnrollments.Count(ce => teacherCourseIds.Contains(ce.CourseId) && ce.StudentId == u.Id)
                    }).ToListAsync();

                return Ok(studentsForTeacher);
            }

            var students = await _context.Users
                .AsNoTracking()
                .Where(u => u.Role.ToString() == "Student" || ((int)u.Role) == 0)
                .Select(u => new {
                    u.Id,
                    name = u.Username,
                    u.Email,
                    phoneNumber = u.PhoneNumber,
                    u.ProfileImage,
                    u.TotalXP,
                    enrolledCoursesCount = _context.CourseEnrollments.Count(ce => ce.StudentId == u.Id)
                }).ToListAsync();

            return Ok(students);
        }
    }

    public class UpdateProfileDto
    {
        public string Username { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public string Specialization { get; set; } = string.Empty;
        public string? NewPassword { get; set; }
    }
}