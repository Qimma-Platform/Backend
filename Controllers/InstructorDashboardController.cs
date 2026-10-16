using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ELearningPlatform.Infrastructure.Data;
using System.Linq;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using ELearningPlatform.Core.Entities;

namespace ELearningPlatform.Controllers
{
    [Route("api/instructor/dashboard")]
    [ApiController]
    [Authorize]
    public class InstructorDashboardController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public InstructorDashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetDashboardStats([FromQuery] string timeframe = "7days", [FromQuery] string? date = null)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int instructorId)) return Unauthorized();

            var instructor = await _context.Users.FindAsync(instructorId);
            if (instructor == null) return NotFound();

            var now = DateTime.UtcNow;
            var currentMonthStart = new DateTime(now.Year, now.Month, 1);
            var lastMonthStart = currentMonthStart.AddMonths(-1);

            // 1. Get Course IDs, Ratings, and Types for the instructor (lightweight projection)
            var courses = await _context.Courses
                .AsNoTracking()
                .Where(c => c.InstructorId == instructorId)
                .Select(c => new { c.Id, c.Rating, c.IsFree, c.Price })
                .ToListAsync();

            var courseIds = courses.Select(c => c.Id).ToList();
            int paidCoursesCount = courses.Count(c => !c.IsFree && c.Price > 0);
            int freeCoursesCount = courses.Count(c => c.IsFree || c.Price == 0);

            double instructorRating = 0;
            var ratedCourses = courses.Where(c => (c.Rating ?? 0m) > 0).ToList();
            if (ratedCourses.Any())
            {
                instructorRating = Math.Round((double)ratedCourses.Average(c => c.Rating ?? 0m), 1);
            }

            // 2. High-Performance Metrics (Single Queries)
            int totalStudentsCount = 0;
            int totalLessonsCount = 0;
            int totalQuizzesCount = 0;

            if (courseIds.Count > 0)
            {
                totalStudentsCount = await _context.CourseEnrollments
                    .Where(e => courseIds.Contains(e.CourseId))
                    .Select(e => e.StudentId)
                    .Distinct()
                    .CountAsync();

                totalLessonsCount = await _context.Lessons
                    .Where(l => courseIds.Contains(l.CourseId))
                    .CountAsync();

                totalQuizzesCount = await _context.QuizAttempts
                    .Where(qa => qa.Lesson != null && courseIds.Contains(qa.Lesson.CourseId))
                    .Select(qa => qa.LessonId)
                    .Distinct()
                    .CountAsync();
            }

            var enrollments = courseIds.Count == 0 ? new List<EnrollmentMetricDto>() : await _context.CourseEnrollments
                .AsNoTracking()
                .Where(e => courseIds.Contains(e.CourseId))
                .Select(e => new EnrollmentMetricDto
                {
                    EnrolledAt = e.EnrolledAt,
                    StudentId = e.StudentId,
                    StudentUsername = e.Student != null ? e.Student.Username : "طالب",
                    CourseTitle = e.Course != null ? e.Course.Title : "كورس",
                    CourseIsFree = e.Course != null && e.Course.IsFree,
                    CoursePrice = e.Course != null ? e.Course.Price : 0m,
                    CourseDiscount = e.Course != null ? e.Course.DiscountPercentage : 0m
                })
                .ToListAsync();

            var allAttempts = courseIds.Count == 0 ? new List<QuizAttempt>() : await _context.QuizAttempts
                .AsNoTracking()
                .Include(q => q.Student)
                .Include(q => q.Lesson)
                .Where(q => q.Lesson != null && courseIds.Contains(q.Lesson.CourseId))
                .OrderByDescending(q => q.AttemptDate)
                .ToListAsync();

            // Deduplicate: Each student completing a quiz for a lesson only appears once (latest attempt)
            var deduplicatedAttempts = allAttempts
                .GroupBy(q => new { q.StudentId, q.LessonId })
                .Select(g => g.OrderByDescending(q => q.AttemptDate).First())
                .OrderByDescending(q => q.AttemptDate)
                .Take(50)
                .ToList();

            var recentActivities = deduplicatedAttempts.Select(q => (dynamic)new
            {
                type = "quiz",
                messageAr = $"أنهى {(q.Student != null ? q.Student.Username : "طالب")} اختبار لدرس {(q.Lesson != null ? q.Lesson.Title : "غير معروف")} بدرجة {q.Score}",
                messageEn = $"{(q.Student != null ? q.Student.Username : "Student")} completed {(q.Lesson != null ? q.Lesson.Title : "Unknown")} quiz with score {q.Score}",
                date = DateTime.SpecifyKind(q.AttemptDate, DateTimeKind.Utc)
            }).ToList();

            // 3. Calculate Real-Time Revenue & Growth (Preserve payments from PaymentTransactions even if course canceled - Req 8)
            var persistentPayments = await _context.PaymentTransactions
                .AsNoTracking()
                .Where(p => p.InstructorId == instructorId)
                .ToListAsync();

            decimal totalRevenue = 0m;
            decimal monthlyRevenue = 0m;

            if (persistentPayments.Any())
            {
                totalRevenue = persistentPayments.Sum(p => p.AmountPaid * 0.8m);
                monthlyRevenue = persistentPayments.Where(p => p.EnrolledAt >= currentMonthStart).Sum(p => p.AmountPaid * 0.8m);
            }
            else
            {
                totalRevenue = enrollments
                    .Where(e => !e.CourseIsFree)
                    .Sum(e => (e.CoursePrice * (1m - (e.CourseDiscount / 100m))) * 0.8m);

                monthlyRevenue = enrollments
                    .Where(e => !e.CourseIsFree && e.EnrolledAt >= currentMonthStart)
                    .Sum(e => (e.CoursePrice * (1m - (e.CourseDiscount / 100m))) * 0.8m);
            }

            var studentsThisMonth = enrollments.Where(e => e.EnrolledAt >= currentMonthStart).Select(e => e.StudentId).Distinct().Count();
            var studentsLastMonth = enrollments.Where(e => e.EnrolledAt >= lastMonthStart && e.EnrolledAt < currentMonthStart).Select(e => e.StudentId).Distinct().Count();

            double studentGrowth = 0;
            if (studentsLastMonth > 0)
            {
                studentGrowth = Math.Round(((double)(studentsThisMonth - studentsLastMonth) / studentsLastMonth) * 100, 1);
            }
            else if (studentsThisMonth > 0)
            {
                studentGrowth = 100;
            }

            var recentTransactions = persistentPayments.Any()
                ? persistentPayments
                    .OrderByDescending(p => p.EnrolledAt)
                    .Take(100)
                    .Select(p => (dynamic)new
                    {
                        studentName = p.StudentName,
                        courseTitle = p.CourseTitle,
                        amount = Math.Round(p.AmountPaid * 0.8m, 2),
                        date = DateTime.SpecifyKind(p.EnrolledAt, DateTimeKind.Utc)
                    })
                    .ToList()
                : enrollments
                    .OrderByDescending(e => e.EnrolledAt)
                    .Take(100)
                    .Select(e => (dynamic)new
                    {
                        studentName = e.StudentUsername,
                        courseTitle = e.CourseTitle,
                        amount = e.CourseIsFree ? 0 : Math.Round((e.CoursePrice * (1m - (e.CourseDiscount / 100m))) * 0.8m, 2),
                        date = DateTime.SpecifyKind(e.EnrolledAt, DateTimeKind.Utc)
                    })
                    .ToList();

            // 4. Dynamic Real Chart Data Generation (New Enrollments & Course Completions)
            var lessonCountsPerCourse = courseIds.Count == 0 ? new Dictionary<int, int>() : await _context.Lessons
                .Where(l => courseIds.Contains(l.CourseId))
                .GroupBy(l => l.CourseId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            var studentCourseCompletions = allAttempts
                .GroupBy(q => new { q.StudentId, CourseId = q.Lesson!.CourseId })
                .Where(g => lessonCountsPerCourse.ContainsKey(g.Key.CourseId) &&
                            lessonCountsPerCourse[g.Key.CourseId] > 0 &&
                            g.Select(x => x.LessonId).Distinct().Count() >= lessonCountsPerCourse[g.Key.CourseId])
                .Select(g => g.Max(x => x.AttemptDate))
                .ToList();

            // If no full course completions yet in db, use completed quiz milestones as completions
            var completionsDates = studentCourseCompletions.Count > 0
                ? studentCourseCompletions
                : deduplicatedAttempts.Select(a => a.AttemptDate).ToList();

            var chartLabels = new List<string>();
            var newEnrollmentsData = new List<int>();
            var courseCompletionsData = new List<int>();

            if (!string.IsNullOrWhiteSpace(date) && DateTime.TryParse(date, out var specificDate))
            {
                var sDate = specificDate.Date;
                for (int hour = 0; hour < 24; hour += 4)
                {
                    var slotStart = sDate.AddHours(hour);
                    var slotEnd = slotStart.AddHours(4);
                    chartLabels.Add($"{hour:D2}:00 - {hour + 4:D2}:00");

                    int slotEnrollments = enrollments.Count(e => e.EnrolledAt >= slotStart && e.EnrolledAt < slotEnd);
                    int slotCompletions = completionsDates.Count(d => d >= slotStart && d < slotEnd);
                    newEnrollmentsData.Add(slotEnrollments);
                    courseCompletionsData.Add(slotCompletions);
                }
            }
            else if (timeframe == "all")
            {
                for (int i = 23; i >= 0; i--)
                {
                    var targetMonth = now.AddMonths(-i);
                    chartLabels.Add(targetMonth.ToString("MMM yyyy"));

                    int monthlyEnrollments = enrollments.Count(e => e.EnrolledAt.Year == targetMonth.Year && e.EnrolledAt.Month == targetMonth.Month);
                    int monthlyCompletions = completionsDates.Count(d => d.Year == targetMonth.Year && d.Month == targetMonth.Month);
                    newEnrollmentsData.Add(monthlyEnrollments);
                    courseCompletionsData.Add(monthlyCompletions);
                }
            }
            else if (timeframe == "1year")
            {
                for (int i = 11; i >= 0; i--)
                {
                    var targetMonth = now.AddMonths(-i);
                    chartLabels.Add(targetMonth.ToString("MMM yyyy"));

                    int monthlyEnrollments = enrollments.Count(e => e.EnrolledAt.Year == targetMonth.Year && e.EnrolledAt.Month == targetMonth.Month);
                    int monthlyCompletions = completionsDates.Count(d => d.Year == targetMonth.Year && d.Month == targetMonth.Month);
                    newEnrollmentsData.Add(monthlyEnrollments);
                    courseCompletionsData.Add(monthlyCompletions);
                }
            }
            else if (timeframe == "30days")
            {
                for (int i = 29; i >= 0; i--)
                {
                    var targetDate = now.AddDays(-i).Date;
                    chartLabels.Add(targetDate.ToString("dd MMM"));

                    int dailyEnrollments = enrollments.Count(e => e.EnrolledAt.Date == targetDate);
                    int dailyCompletions = completionsDates.Count(d => d.Date == targetDate);
                    newEnrollmentsData.Add(dailyEnrollments);
                    courseCompletionsData.Add(dailyCompletions);
                }
            }
            else // 7days default
            {
                for (int i = 6; i >= 0; i--)
                {
                    var targetDate = now.AddDays(-i).Date;
                    chartLabels.Add(targetDate.ToString("dd MMM"));

                    int dailyEnrollments = enrollments.Count(e => e.EnrolledAt.Date == targetDate);
                    int dailyCompletions = completionsDates.Count(d => d.Date == targetDate);
                    newEnrollmentsData.Add(dailyEnrollments);
                    courseCompletionsData.Add(dailyCompletions);
                }
            }

            // 5. Calculate Top Courses for Instructor (Your Top Courses widget)
            var topCourses = new List<object>();
            if (courseIds.Count > 0)
            {
                var instructorCourseDetails = await _context.Courses
                    .AsNoTracking()
                    .Where(c => c.InstructorId == instructorId)
                    .Select(c => new
                    {
                        c.Id,
                        c.Title,
                        c.CoverImageUrl,
                        c.Rating,
                        TotalStudents = _context.CourseEnrollments.Count(ce => ce.CourseId == c.Id),
                        TotalLessons = _context.Lessons.Count(l => l.CourseId == c.Id)
                    })
                    .OrderByDescending(c => c.TotalStudents)
                    .ThenByDescending(c => c.Rating)
                    .Take(100)
                    .ToListAsync();

                var calculatedCourses = new List<(int Id, string Title, int StudentsCount, int CompletionPercentage, string? CoverImageUrl)>();

                foreach (var c in instructorCourseDetails)
                {
                    int completionPercentage = 0;
                    if (c.TotalStudents > 0)
                    {
                        var completedAttemptsCount = await _context.QuizAttempts
                            .Where(qa => qa.Lesson != null && qa.Lesson.CourseId == c.Id)
                            .Select(qa => new { qa.StudentId, qa.LessonId })
                            .Distinct()
                            .CountAsync();

                        var totalAssignmentsCount = await _context.Assignments
                            .Where(a => a.CourseId == c.Id)
                            .CountAsync();

                        var completedAssignmentsCount = totalAssignmentsCount == 0 ? 0 : await _context.AssignmentSubmissions
                            .Where(asub => asub.Assignment != null && asub.Assignment.CourseId == c.Id)
                            .Select(asub => new { asub.StudentId, asub.AssignmentId })
                            .Distinct()
                            .CountAsync();

                        int totalActivitiesPerStudent = c.TotalLessons + totalAssignmentsCount;
                        int totalCompletedActivities = completedAttemptsCount + completedAssignmentsCount;
                        int maxPossibleActivities = c.TotalStudents * totalActivitiesPerStudent;

                        if (maxPossibleActivities > 0)
                        {
                            double ratio = (double)totalCompletedActivities / maxPossibleActivities;
                            completionPercentage = (int)Math.Min(100, Math.Round(ratio * 100));
                        }
                    }

                    calculatedCourses.Add((c.Id, c.Title, c.TotalStudents, completionPercentage, c.CoverImageUrl));
                }

                // Automatically sort from highest to lowest by completion percentage, then by enrolled students count
                topCourses = calculatedCourses
                    .OrderByDescending(c => c.CompletionPercentage)
                    .ThenByDescending(c => c.StudentsCount)
                    .ThenBy(c => c.Title)
                    .Select(c => (object)new
                    {
                        id = c.Id,
                        title = c.Title,
                        studentsCount = c.StudentsCount,
                        completionPercentage = c.CompletionPercentage,
                        coverImageUrl = c.CoverImageUrl
                    })
                    .ToList();
            }

            return Ok(new
            {
                totalStudents = totalStudentsCount,
                totalQuizzes = totalQuizzesCount,
                totalLessons = totalLessonsCount,
                activeCourses = courses.Count,
                paidCoursesCount = paidCoursesCount,
                freeCoursesCount = freeCoursesCount,
                totalRevenue = totalRevenue,
                monthlyRevenue = monthlyRevenue,
                studentGrowth = studentGrowth,
                instructorRating = instructorRating,
                recentTransactions = recentTransactions,
                activityFeed = recentActivities,
                topCourses = topCourses,
                chartData = new
                {
                    labels = chartLabels,
                    newEnrollments = newEnrollmentsData,
                    courseCompletions = courseCompletionsData,
                    ongoing = newEnrollmentsData,
                    submitted = courseCompletionsData
                }
            });
        }
    }

    public class EnrollmentMetricDto
    {
        public DateTime EnrolledAt { get; set; }
        public int StudentId { get; set; }
        public string StudentUsername { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public bool CourseIsFree { get; set; }
        public decimal CoursePrice { get; set; }
        public decimal CourseDiscount { get; set; }
    }
}