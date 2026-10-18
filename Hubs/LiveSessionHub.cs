using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Collections.Generic;
using System;
using ELearningPlatform.Infrastructure.Data; 
using ELearningPlatform.Core.Entities;      

namespace ELearningPlatform.Hubs
{
    public class LiveSessionHub : Hub
    {
        private static ConcurrentDictionary<string, SessionInfo> ActiveSessions = new();
        private static ConcurrentDictionary<string, string> UserConnections = new();

        private readonly ApplicationDbContext _context;

        public LiveSessionHub(ApplicationDbContext context)
        {
            _context = context;
        }

        public class SessionInfo
        {
            public string CourseId { get; set; } = string.Empty;
            public string InstructorName { get; set; } = string.Empty;
            public string CourseTitle { get; set; } = string.Empty;
            public string MeetUrl { get; set; } = string.Empty;
        }

        public async Task RegisterStudent(string[] enrolledCourseIds)
        {
            foreach (var courseId in enrolledCourseIds)
            {
                if (ActiveSessions.TryGetValue(courseId, out var session))
                {
                    await Clients.Caller.SendAsync("SessionStarted", session.CourseId, session.InstructorName, session.CourseTitle, session.MeetUrl);
                }
            }
        }

        public async Task JoinCourseGroup(string courseId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Course_{courseId}");
            UserConnections[Context.ConnectionId] = courseId;
        }

        public async Task LeaveCourseGroup(string courseId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Course_{courseId}");
            UserConnections.TryRemove(Context.ConnectionId, out _);
        }

        public async Task StartLiveSession(string courseId, string instructorName, string courseTitle, string meetUrl)
        {
            var session = new SessionInfo { CourseId = courseId, InstructorName = instructorName, CourseTitle = courseTitle, MeetUrl = meetUrl };
            ActiveSessions[courseId] = session;

            await Clients.All.SendAsync("SessionStarted", courseId, instructorName, courseTitle, meetUrl);
            try
            {
                if (int.TryParse(courseId, out int cId))
                {
                    var enrolledStudentIds = await _context.CourseEnrollments
                        .Where(ce => ce.CourseId == cId)
                        .Select(ce => ce.StudentId)
                        .ToListAsync();

                    var notifications = new List<Notification>();

                    foreach (var studentId in enrolledStudentIds)
                    {
                        notifications.Add(new Notification
                        {
                            UserId = studentId,
                            Message = $"LIVE_SESSION||بدأ المعلم {instructorName} بثاً مباشراً لكورس '{courseTitle}'. انضم الآن!||Instructor {instructorName} started a live session for course '{courseTitle}'. Join now!",
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending live session notifications: {ex.Message}");
            }
        }

        public async Task EndLiveSession(string courseId)
        {
            ActiveSessions.TryRemove(courseId, out _);
            await Clients.All.SendAsync("SessionEnded", courseId);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (UserConnections.TryGetValue(Context.ConnectionId, out string courseId))
            {
                UserConnections.TryRemove(Context.ConnectionId, out _);
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}