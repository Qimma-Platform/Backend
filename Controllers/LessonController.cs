using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ELearningPlatform.Core.Entities;
using ELearningPlatform.Infrastructure.Data;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace ELearningPlatform.Controllers
{
    [Route("api/course")]
    [ApiController]
    [Authorize]
    public class LessonController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        // Injected IWebHostEnvironment to handle saving the video files
        public LessonController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [HttpGet("{courseId}")]
        public async Task<IActionResult> GetCourse(int courseId)
        {
            var course = await _context.Courses.FindAsync(courseId);
            if (course == null) return NotFound();
            return Ok(course);
        }

        [HttpGet("{courseId}/lessons")]
        public async Task<IActionResult> GetLessons(int courseId)
        {
            var lessons = await _context.Lessons.Where(l => l.CourseId == courseId).ToListAsync();
            return Ok(lessons);
        }

        [HttpPost("upload-video")]
        [DisableRequestSizeLimit]
        [RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = 524288000)]
        public async Task<IActionResult> UploadVideo(IFormFile file)
        {
            var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
            if (role != "Teacher" && role != "1") return Forbid();

            if (file == null || file.Length == 0) return BadRequest("No file uploaded.");

            var uploadsFolder = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "videos");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return Ok(new { videoUrl = $"/videos/{uniqueFileName}" });
        }

        [HttpPost("{courseId}/lessons")]
        public async Task<IActionResult> AddLesson(int courseId, [FromBody] Lesson lessonDto)
        {
            var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
            if (role != "Teacher" && role != "1") return Forbid();

            var course = await _context.Courses.FindAsync(courseId);
            if (course == null) return NotFound("Course not found");

            var lesson = new Lesson
            {
                Title = lessonDto.Title,
                Description = lessonDto.Description,
                VideoUrl = lessonDto.VideoUrl,
                CourseId = courseId
            };

            _context.Lessons.Add(lesson);
            await _context.SaveChangesAsync();

            try
            {
                var enrolledStudentIds = await _context.CourseEnrollments
                    .Where(ce => ce.CourseId == courseId)
                    .Select(ce => ce.StudentId)
                    .ToListAsync();

                var notifications = new List<Notification>();

                foreach (var studentId in enrolledStudentIds)
                {
                    notifications.Add(new Notification
                    {
                        UserId = studentId,
                        Message = $"NEW_LESSON||{lesson.Id}||{lesson.Title}||{course.Id}||{course.Title}",
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    });
                }

                if (notifications.Any())
                {
                    _context.Notifications.AddRange(notifications);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending lesson notifications: {ex.Message}");
            }

            return Ok(lesson);
        }

        [HttpPut("{courseId}/lessons/{lessonId}")]
        public async Task<IActionResult> UpdateLesson(int courseId, int lessonId, [FromBody] Lesson lessonDto)
        {
            var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
            if (role != "Teacher" && role != "1") return Forbid();

            var lesson = await _context.Lessons.FirstOrDefaultAsync(l => l.Id == lessonId && l.CourseId == courseId);
            if (lesson == null) return NotFound("Lesson not found");

            lesson.Title = lessonDto.Title;
            lesson.Description = lessonDto.Description;
            lesson.VideoUrl = lessonDto.VideoUrl;

            await _context.SaveChangesAsync();
            return Ok(lesson);
        }

        [HttpDelete("{courseId}/lessons/{lessonId}")]
        public async Task<IActionResult> DeleteLesson(int courseId, int lessonId)
        {
            var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
            if (role != "Teacher" && role != "1") return Forbid();

            var lesson = await _context.Lessons.FirstOrDefaultAsync(l => l.Id == lessonId && l.CourseId == courseId);
            if (lesson == null) return NotFound("Lesson not found");

            _context.Lessons.Remove(lesson);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Lesson deleted successfully" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCourse(int id)
        {
            var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
            if (role != "Teacher" && role != "1") return Forbid();

            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound();

            _context.Courses.Remove(course);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Course deleted successfully" });
        }
    }
}