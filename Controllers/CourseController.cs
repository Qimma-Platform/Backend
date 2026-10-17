using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using ELearningPlatform.Core.DTOs;
using ELearningPlatform.Core.Interfaces;
using ELearningPlatform.Core.Entities;
using ELearningPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using ELearningPlatform.Entities;

namespace ELearningPlatform.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CourseController : ControllerBase
    {
        private readonly ICourseService _courseService;
        private readonly ApplicationDbContext _context;

        public CourseController(ICourseService courseService, ApplicationDbContext context)
        {
            _courseService = courseService;
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> CreateCourse([FromBody] CreateCourseDto request)
        {
            try
            {
                var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");

                if (string.IsNullOrEmpty(userIdString))
                    return Unauthorized(new { message = "Invalid token: User ID not found" });

                if (role != "Teacher" && role != "1" && role != "Instructor")
                {
                    return StatusCode(403, new { message = $"Forbidden: Your role is '{role}', but Teacher is required." });
                }

                int instructorId = int.Parse(userIdString);

                if (request.Title != null && request.Title.StartsWith(" "))
                    return BadRequest(new { message = "Course title cannot start with a space." });
                if (request.Description != null && request.Description.StartsWith(" "))
                    return BadRequest(new { message = "Course description cannot start with a space." });
                if (!request.IsFree && request.Price <= 0)
                    return BadRequest(new { message = "Course price must be greater than zero for paid courses." });
                if (request.DiscountPercentage < 0 || request.DiscountPercentage > 100)
                    return BadRequest(new { message = "Discount percentage must be between 0 and 100." });

                var course = await _courseService.CreateCourseAsync(request, instructorId);
                return Ok(course);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCourse(int id, [FromBody] UpdateCourseDto request)
        {
            try
            {
                var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");

                if (string.IsNullOrEmpty(userIdString))
                    return Unauthorized(new { message = "Invalid token: User ID not found" });

                if (role != "Teacher" && role != "1" && role != "Instructor")
                {
                    return StatusCode(403, new { message = $"Forbidden: Your role is '{role}', but Teacher is required." });
                }

                if (request.Title != null && request.Title.StartsWith(" "))
                    return BadRequest(new { message = "Course title cannot start with a space." });
                if (request.Description != null && request.Description.StartsWith(" "))
                    return BadRequest(new { message = "Course description cannot start with a space." });
                if (request.IsFree.HasValue && !request.IsFree.Value && request.Price.HasValue && request.Price.Value <= 0)
                    return BadRequest(new { message = "Course price must be greater than zero for paid courses." });
                if (request.DiscountPercentage.HasValue && (request.DiscountPercentage.Value < 0 || request.DiscountPercentage.Value > 100))
                    return BadRequest(new { message = "Discount percentage must be between 0 and 100." });

                int instructorId = int.Parse(userIdString);
                var course = await _courseService.UpdateCourseAsync(id, request, instructorId);
                if (course == null)
                    return NotFound(new { message = "Course not found or you are not authorized to update this course." });

                return Ok(course);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllCourses()
        {
            var courses = await _courseService.GetAllCoursesAsync();
            return Ok(courses);
        }

        [HttpPost("enroll/{courseId}")]
        public async Task<IActionResult> Enroll(int courseId)
        {
            var studentId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var studentName = User.FindFirstValue(ClaimTypes.Name);

            var result = await _courseService.EnrollInCourseAsync(studentId, courseId);
            if (!result) return BadRequest(new { message = "Already enrolled or course not found" });
            var course = await _context.Courses.FindAsync(courseId);
            if (course != null)
            {
                var student = await _context.Users.FindAsync(studentId);
                var studentDisplay = student?.Username ?? studentName ?? "طالب";
                bool isFree = course.IsFree || course.Price == 0;
                string enrollTypeAr = isFree ? "مجاني" : $"مدفوع ({course.Price} ج.م)";
                string enrollTypeEn = isFree ? "Free" : $"Paid ({course.Price} EGP)";

                var teacherNotif = new Notification
                {
                    UserId = course.InstructorId,
                    Message = $"طالب جديد انضم! لقد قام الطالب '{studentDisplay}' بالاشتراك في كورس '{course.Title}'.",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                };
                _context.Notifications.Add(teacherNotif);

                var adminIds = await _context.Users.Where(u => (int)u.Role == 2).Select(u => u.Id).ToListAsync();
                foreach (var aId in adminIds)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = aId,
                        Message = $"ENROLLMENT||اشتراك جديد ({enrollTypeAr}): قام الطالب '{studentDisplay}' بالاشتراك في كورس '{course.Title}'||New Enrollment ({enrollTypeEn}): Student '{studentDisplay}' enrolled in course '{course.Title}'",
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    });
                }

                if (!isFree && course.Price > 0)
                {
                    decimal actualPaid = course.Price - (course.Price * course.DiscountPercentage / 100m);
                    var inst = await _context.Users.FindAsync(course.InstructorId);
                    if (inst != null)
                    {
                        inst.WalletBalance += actualPaid * 0.8m;
                    }
                    _context.PaymentTransactions.Add(new ELearningPlatform.Core.Entities.PaymentTransaction
                    {
                        StudentId = studentId,
                        StudentName = studentDisplay,
                        StudentEmail = student?.Email ?? "",
                        StudentPhone = student?.PhoneNumber,
                        CourseId = course.Id,
                        CourseTitle = course.Title,
                        InstructorId = course.InstructorId,
                        InstructorName = inst?.Username ?? "Instructor",
                        AmountPaid = actualPaid,
                        PaymentStatus = "PAID",
                        EnrolledAt = DateTime.UtcNow
                    });
                }

                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "Enrolled successfully" });
        }

        [HttpGet("my-courses")]
        public async Task<IActionResult> GetMyCourses()
        {
            var studentId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var courses = await _courseService.GetStudentCoursesAsync(studentId);
            return Ok(courses);
        }

        [HttpGet("instructor-courses")]
        public async Task<IActionResult> GetInstructorCourses()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized();

            int instructorId = int.Parse(userIdString);
            var courses = await _courseService.GetInstructorCoursesAsync(instructorId);
            return Ok(courses);
        }

        [HttpPost("lesson/{lessonId}/complete")]
        [Authorize]
        public async Task<IActionResult> MarkLessonAsCompleted(int lessonId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId) && int.TryParse(userId, out int studentId))
            {
                var existingAttempt = await _context.QuizAttempts
                    .FirstOrDefaultAsync(qa => qa.StudentId == studentId && qa.LessonId == lessonId);

                if (existingAttempt == null)
                {
                    _context.QuizAttempts.Add(new ELearningPlatform.Core.Entities.QuizAttempt
                    {
                        StudentId = studentId,
                        LessonId = lessonId,
                        Score = 1,
                        TotalQuestions = 1,
                        AttemptDate = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();
                }
            }
            return Ok(new { message = "Lesson marked as completed successfully" });
        }

        [HttpPost("{courseId}/rate")]
        public async Task<IActionResult> RateCourse(int courseId, [FromBody] int rating)
        {
            var studentIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(studentIdString))
            {
                return Unauthorized();
            }

            var course = await _context.Courses.FindAsync(courseId);
            if (course == null) return NotFound(new { message = "Course not found" });

            var existingRating = await _context.CourseRatings
                .FirstOrDefaultAsync(r => r.CourseId == courseId && r.StudentId == studentIdString);

            if (existingRating != null)
            {
                var totalSumWithoutOld = (course.Rating ?? 0) * course.TotalRatingsCount - existingRating.Rating;

                existingRating.Rating = rating;
                existingRating.CreatedAt = DateTime.UtcNow;
                _context.CourseRatings.Update(existingRating);

                course.Rating = (totalSumWithoutOld + rating) / course.TotalRatingsCount;
            }
            else
            {
                var newRating = new CourseRating
                {
                    CourseId = courseId,
                    StudentId = studentIdString,
                    Rating = rating,
                    CreatedAt = DateTime.UtcNow
                };
                await _context.CourseRatings.AddAsync(newRating);

                course.Rating = ((course.Rating ?? 0) * course.TotalRatingsCount + rating) / (course.TotalRatingsCount + 1);
                course.TotalRatingsCount += 1;
            }

            course.Rating = Math.Round((decimal)course.Rating, 1);

            _context.Courses.Update(course);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Rating updated successfully", newRating = course.Rating });
        }
    }
}