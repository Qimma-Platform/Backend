using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ELearningPlatform.Core.Entities;
using ELearningPlatform.Infrastructure.Data;
using System.Security.Claims;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ELearningPlatform.Controllers
{
    [Route("api/schedule")]
    [ApiController]
    [Authorize]
    public class ScheduleController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ScheduleController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("student")]
        public async Task<IActionResult> GetStudentSchedule()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int studentId)) return Unauthorized();

            var enrolledCourseIds = await _context.CourseEnrollments
                .Where(ce => ce.StudentId == studentId)
                .Select(ce => ce.CourseId)
                .ToListAsync();

            var events = await _context.ScheduleEvents
                .Include(e => e.Course)
                .Where(e => enrolledCourseIds.Contains(e.CourseId) && e.EventDate >= DateTime.UtcNow.Date)
                .OrderBy(e => e.EventDate)
                .Select(e => new {
                    e.Id,
                    e.CourseId,
                    CourseTitle = e.Course!.Title,
                    e.Title,
                    e.Description,
                    e.EventDate,
                    e.EventType
                }).ToListAsync();

            return Ok(events);
        }

        [HttpGet("teacher")]
        public async Task<IActionResult> GetTeacherSchedule()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int teacherId)) return Unauthorized();

            var courses = await _context.Courses
                .Where(c => c.InstructorId == teacherId)
                .Select(c => c.Id)
                .ToListAsync();

            var events = await _context.ScheduleEvents
                .Include(e => e.Course)
                .Where(e => courses.Contains(e.CourseId) && e.EventDate >= DateTime.UtcNow.Date)
                .OrderBy(e => e.EventDate)
                .Select(e => new {
                    e.Id,
                    e.CourseId,
                    CourseTitle = e.Course!.Title,
                    e.Title,
                    e.Description,
                    e.EventDate,
                    e.EventType
                }).ToListAsync();

            return Ok(events);
        }

        [HttpPost]
        public async Task<IActionResult> CreateEvent([FromBody] ScheduleEventDto dto)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int teacherId)) return Unauthorized();

            var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == dto.CourseId && c.InstructorId == teacherId);
            if (course == null) return Forbid();

            var newEvent = new ScheduleEvent
            {
                CourseId = dto.CourseId,
                Title = dto.Title,
                Description = dto.Description,
                EventDate = dto.EventDate,
                EventType = dto.EventType
            };

            _context.ScheduleEvents.Add(newEvent);
            await _context.SaveChangesAsync();

            try
            {
                var enrolledStudentIds = await _context.CourseEnrollments
                    .Where(ce => ce.CourseId == dto.CourseId)
                    .Select(ce => ce.StudentId)
                    .ToListAsync();

                var notifications = new List<Notification>();

                foreach (var studentId in enrolledStudentIds)
                {
                    notifications.Add(new Notification
                    {
                        UserId = studentId,
                        Message = $"تم إضافة موعد جديد ({dto.EventType}) بعنوان '{dto.Title}' لكورس '{course.Title}'",
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
                Console.WriteLine($"Error sending schedule notifications: {ex.Message}");
            }

            return Ok(newEvent);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var ev = await _context.ScheduleEvents.Include(e => e.Course).FirstOrDefaultAsync(e => e.Id == id);
            if (ev == null) return NotFound();

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int teacherId)) return Unauthorized();

            if (ev.Course!.InstructorId != teacherId) return Forbid();

            _context.ScheduleEvents.Remove(ev);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Deleted" });
        }
    }

    public class ScheduleEventDto
    {
        public int CourseId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime EventDate { get; set; }
        public string EventType { get; set; } = "Live";
    }
}