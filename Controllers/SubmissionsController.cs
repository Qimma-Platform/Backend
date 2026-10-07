using System.Security.Claims;
using System.Threading.Tasks;
using ELearningPlatform.Core.Entities;
using ELearningPlatform.Core.Interfaces;
using ELearningPlatform.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace ELearningPlatform.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // لازم يكون عامل Login
    public class SubmissionsController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAIGradingService _aiGradingService;
        private readonly IHubContext<NotificationHub> _hubContext;

        public SubmissionsController(
            IUnitOfWork unitOfWork,
            IAIGradingService aiGradingService,
            IHubContext<NotificationHub> hubContext)
        {
            _unitOfWork = unitOfWork;
            _aiGradingService = aiGradingService;
            _hubContext = hubContext;
        }

        public class SubmitTaskDto
        {
            public int TaskId { get; set; }
            public required string Content { get; set; }
        }

        [HttpPost("submit")]
        [Authorize(Roles = "Student")] // مسموح للطلبة بس
        public async Task<IActionResult> SubmitTask([FromBody] SubmitTaskDto request)
        {
            // بنجيب الـ ID بتاع الطالب من التوكن بتاعه
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int studentId)) return Unauthorized();

            var task = await _unitOfWork.Tasks.GetByIdAsync(request.TaskId);
            if (task == null) return NotFound("Task not found.");

            // 1. تسجيل الإجابة في الداتابيز
            var submission = new Submission
            {
                Content = request.Content,
                StudentId = studentId,
                CourseTaskId = task.Id
            };

            await _unitOfWork.Submissions.AddAsync(submission);

            // 2. بنكلم الـ AI يقيم الإجابة
            var aiResult = await _aiGradingService.GradeSubmissionAsync(task.Description, request.Content);

            submission.AIScore = aiResult.Score;
            submission.AIFeedback = aiResult.Feedback;

            // 3. بنزود الـ XP لو الدرجة عالية
            if (aiResult.Score >= 80)
            {
                var student = await _unitOfWork.Users.GetByIdAsync(studentId);
                if (student != null)
                {
                    student.TotalXP += task.XPReward;
                    _unitOfWork.Users.Update(student);
                }
            }

            var message = $"Great job! AI graded your task. Score: {aiResult.Score}/100. XP Earned: {(aiResult.Score >= 80 ? task.XPReward : 0)}";

            // حفظ الإشعار في الداتابيز
            var notification = new Notification { UserId = studentId, Message = message };
            await _unitOfWork.Notifications.AddAsync(notification);

            // بنعمل Save لكل ده مرة واحدة
            await _unitOfWork.CompleteAsync();

            // 4. بنبعت الإشعار للطالب في نفس اللحظة وهو فاتح الشاشة
            await _hubContext.Clients.User(studentId.ToString())
                .SendAsync("ReceiveNotification", message, aiResult.Feedback);

            return Ok(new
            {
                message = "Submission successful!",
                score = aiResult.Score,
                feedback = aiResult.Feedback
            });
        }
    }
}