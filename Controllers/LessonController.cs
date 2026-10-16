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
            if (role != "Teacher" && role != "1" && role != "Admin" && role != "2") return Forbid();

            if (file == null || file.Length == 0) return BadRequest("No file uploaded.");

            var uploadsFolder = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "videos");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var rawFileName = Path.GetFileName(file.FileName);
            var cleanFileName = System.Text.RegularExpressions.Regex.Replace(rawFileName, @"\s+", "_");
            cleanFileName = System.Text.RegularExpressions.Regex.Replace(cleanFileName, @"[^a-zA-Z0-9_\.-]", "");
            var uniqueFileName = Guid.NewGuid().ToString("N") + "_" + cleanFileName;
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
            if (role != "Teacher" && role != "1" && role != "Admin" && role != "2") return Forbid();

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
            if (role != "Teacher" && role != "1" && role != "Admin" && role != "2") return Forbid();

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
            if (role != "Teacher" && role != "1" && role != "Admin" && role != "2") return Forbid();

            var lesson = await _context.Lessons.FirstOrDefaultAsync(l => l.Id == lessonId && l.CourseId == courseId);
            if (lesson == null) return NotFound("Lesson not found");

            // 1. Remove quiz attempts linked to this lesson
            var quizAttempts = await _context.QuizAttempts.Where(qa => qa.LessonId == lessonId).ToListAsync();
            if (quizAttempts.Any())
            {
                _context.QuizAttempts.RemoveRange(quizAttempts);
            }

            // 2. Remove assignments, questions, submissions, answers linked to this lesson
            var assignments = await _context.Assignments
                .Include(a => a.Submissions)
                    .ThenInclude(s => s.Answers)
                .Include(a => a.Questions)
                .Where(a => a.LessonId == lessonId)
                .ToListAsync();

            foreach (var a in assignments)
            {
                foreach (var s in a.Submissions)
                {
                    if (s.Answers != null && s.Answers.Any())
                    {
                        _context.AssignmentAnswers.RemoveRange(s.Answers);
                    }
                }
                if (a.Submissions != null && a.Submissions.Any())
                {
                    _context.AssignmentSubmissions.RemoveRange(a.Submissions);
                }
                if (a.Questions != null && a.Questions.Any())
                {
                    _context.AssignmentQuestions.RemoveRange(a.Questions);
                }
                _context.Assignments.Remove(a);
            }

            // 3. Remove local video file from disk if applicable
            if (!string.IsNullOrEmpty(lesson.VideoUrl) && lesson.VideoUrl.StartsWith("/videos/"))
            {
                try
                {
                    var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                    var relativePath = lesson.VideoUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                    var filePath = Path.Combine(webRoot, relativePath);
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }
                catch { }
            }

            _context.Lessons.Remove(lesson);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Lesson deleted successfully" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCourse(int id)
        {
            var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
            if (role != "Teacher" && role != "1" && role != "Admin" && role != "2") return Forbid();

            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound();

            var lessons = await _context.Lessons.Where(l => l.CourseId == id).ToListAsync();
            foreach (var l in lessons)
            {
                var quizAttempts = await _context.QuizAttempts.Where(qa => qa.LessonId == l.Id).ToListAsync();
                if (quizAttempts.Any()) _context.QuizAttempts.RemoveRange(quizAttempts);

                var assignments = await _context.Assignments
                    .Include(a => a.Submissions).ThenInclude(s => s.Answers)
                    .Include(a => a.Questions)
                    .Where(a => a.LessonId == l.Id)
                    .ToListAsync();

                foreach (var a in assignments)
                {
                    foreach (var s in a.Submissions)
                    {
                        if (s.Answers != null && s.Answers.Any()) _context.AssignmentAnswers.RemoveRange(s.Answers);
                    }
                    if (a.Submissions != null && a.Submissions.Any()) _context.AssignmentSubmissions.RemoveRange(a.Submissions);
                    if (a.Questions != null && a.Questions.Any()) _context.AssignmentQuestions.RemoveRange(a.Questions);
                    _context.Assignments.Remove(a);
                }
            }
            _context.Lessons.RemoveRange(lessons);

            _context.Courses.Remove(course);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Course deleted successfully" });
        }
    }
}