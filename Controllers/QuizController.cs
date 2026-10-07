using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ELearningPlatform.Core.Entities;
using ELearningPlatform.Infrastructure.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Collections.Generic;

namespace ELearningPlatform.Controllers
{
    [Route("api/quiz")]
    [ApiController]
    [Authorize]
    public class QuizController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public QuizController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost("create")]
        public IActionResult CreateQuiz([FromBody] QuizCreateModel model)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int teacherId)) return Unauthorized();

            var existingTasks = _context.CourseTasks.Where(t => t.LessonId == model.LessonId).ToList();
            if (existingTasks.Any())
            {
                _context.CourseTasks.RemoveRange(existingTasks);
            }

            foreach (var q in model.Questions)
            {
                var task = new CourseTask
                {
                    LessonId = model.LessonId,
                    Title = "Quiz Question",
                    Description = "Lesson Quiz",
                    Question = q.Text,
                    ImageUrl = q.ImageUrl,
                    Options = q.Options != null ? string.Join("||", q.Options) : string.Empty,
                    CorrectOptionIndex = q.CorrectIndex,
                    XPReward = 50
                };
                _context.CourseTasks.Add(task);
            }

            _context.SaveChanges();

            try
            {
                var lessonInfo = _context.Lessons.FirstOrDefault(l => l.Id == model.LessonId);
                if (lessonInfo != null)
                {
                    var courseInfo = _context.Courses.FirstOrDefault(c => c.Id == lessonInfo.CourseId);

                    // فلترة: إرسال الإشعار للطلاب فقط، وتجاهل المعلم صاحب الكورس
                    var enrolledStudentIds = _context.CourseEnrollments
                        .Where(ce => ce.CourseId == lessonInfo.CourseId && ce.StudentId != teacherId)
                        .Select(ce => ce.StudentId)
                        .ToList();

                    var notifications = new List<Notification>();

                    foreach (var sId in enrolledStudentIds)
                    {
                        notifications.Add(new Notification
                        {
                            UserId = sId,
                            // إرسال كود الإشعار ليتم ترجمته في الفرونت إند
                            Message = $"NEW_QUIZ||{lessonInfo.Title}||{courseInfo?.Title}",
                            CreatedAt = DateTime.UtcNow,
                            IsRead = false
                        });
                    }

                    if (notifications.Any())
                    {
                        _context.Notifications.AddRange(notifications);
                        _context.SaveChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return Ok(new { message = "Quiz saved successfully" });
        }

        [HttpGet("lesson/{lessonId}")]
        public IActionResult GetQuizByLesson(int lessonId)
        {
            var tasks = _context.CourseTasks.Where(t => t.LessonId == lessonId).ToList();
            var result = tasks.Select(t => new
            {
                id = t.Id,
                text = t.Question,
                imageUrl = t.ImageUrl,
                options = !string.IsNullOrEmpty(t.Options) ? t.Options.Split(new string[] { "||" }, StringSplitOptions.None) : new string[0],
                correctIndex = t.CorrectOptionIndex
            }).ToList();

            return Ok(new { questions = result });
        }

        [HttpDelete("lesson/{lessonId}")]
        public IActionResult DeleteQuiz(int lessonId)
        {
            var tasks = _context.CourseTasks.Where(t => t.LessonId == lessonId).ToList();
            if (!tasks.Any()) return Ok();

            _context.CourseTasks.RemoveRange(tasks);
            _context.SaveChanges();
            return Ok(new { message = "Quiz deleted successfully" });
        }

        [HttpPost("submit-score")]
        public IActionResult SubmitQuizScore([FromBody] QuizScoreModel model)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId)) return Unauthorized();

            var previousAttempts = _context.QuizAttempts
                .Where(q => q.StudentId == userId && q.LessonId == model.LessonId)
                .ToList();

            var attempt = new QuizAttempt
            {
                StudentId = userId,
                LessonId = model.LessonId,
                Score = model.Score,
                TotalQuestions = model.TotalQuestions,
                AttemptNumber = previousAttempts.Count + 1,
                AttemptDate = DateTime.UtcNow
            };

            _context.QuizAttempts.Add(attempt);
            var user = _context.Users.FirstOrDefault(u => u.Id == userId);
            bool isFullMark = model.Score == model.TotalQuestions;
            bool hasPreviousFullMark = previousAttempts.Any(a => a.Score == a.TotalQuestions);

            if (isFullMark && !hasPreviousFullMark && user != null)
            {
                user.TotalXP += 50;

                var xpNotification = new Notification
                {
                    UserId = userId,
                    Message = "QUIZ_FULLMARK||50",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                };
                _context.Notifications.Add(xpNotification);

                var lessonInfo = _context.Lessons.Include(l => l.Course).FirstOrDefault(l => l.Id == model.LessonId);
                if (lessonInfo != null && lessonInfo.Course != null)
                {
                    var notifyTeacher = new Notification
                    {
                        UserId = lessonInfo.Course.InstructorId,
                        Message = $"STUDENT_FULLMARK||{user.Username}||{lessonInfo.Title}",
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    };
                    _context.Notifications.Add(notifyTeacher);
                }
            }

            _context.SaveChanges();

            return Ok(new
            {
                message = "Score submitted successfully",
                newTotalXp = user?.TotalXP,
                xpEarned = (isFullMark && !hasPreviousFullMark) ? 50 : 0
            });
        }

        [HttpGet("course/{courseId}/students")]
        [Authorize]
        public IActionResult GetEnrolledStudents(int courseId)
        {
            var teacherIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(teacherIdString) || !int.TryParse(teacherIdString, out int teacherId)) return Unauthorized();

            var students = _context.CourseEnrollments
                .Include(ce => ce.Student)
                .Where(ce => ce.CourseId == courseId && ce.Student != null && ce.Student.Id != teacherId)
                .Select(ce => new
                {
                    id = ce.StudentId,
                    name = ce.Student!.Username,
                    profileImage = ce.Student!.ProfileImage,
                    phoneNumber = ce.Student!.PhoneNumber
                })
                .Distinct()
                .ToList();

            return Ok(students);
        }

        [HttpGet("course/{courseId}/student/{studentId}/results")]
        [Authorize]
        public IActionResult GetStudentResults(int courseId, int studentId)
        {
            var teacherIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(teacherIdString) || !int.TryParse(teacherIdString, out int teacherId)) return Unauthorized();

            var results = _context.QuizAttempts
                .Include(q => q.Lesson)
                .Where(q => q.StudentId == studentId && q.Lesson.CourseId == courseId)
                .OrderByDescending(q => q.AttemptDate)
                .Select(q => new
                {
                    lessonTitle = q.Lesson.Title,
                    attemptNumber = q.AttemptNumber,
                    score = q.Score,
                    totalQuestions = q.TotalQuestions,
                    date = q.AttemptDate
                })
                .ToList();

            return Ok(results);
        }
    }

    public class QuizScoreModel
    {
        public int LessonId { get; set; }
        public int Score { get; set; }
        public int TotalQuestions { get; set; }
    }

    public class QuizCreateModel
    {
        public int LessonId { get; set; }
        public List<QuestionModel> Questions { get; set; } = new();
    }

    public class QuestionModel
    {
        public string Text { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public List<string> Options { get; set; } = new();
        public int CorrectIndex { get; set; }
    }
}