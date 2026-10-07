using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ELearningPlatform.Core.DTOs;
using ELearningPlatform.Core.Entities;
using ELearningPlatform.Core.Interfaces;
using ELearningPlatform.Infrastructure.Data;

namespace ELearningPlatform.Infrastructure.Services
{
    public class AssignmentService : IAssignmentService
    {
        private readonly ApplicationDbContext _context;

        public AssignmentService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<AssignmentDto> GetAssignmentByIdAsync(int id)
        {
            var a = await _context.Assignments
                .Include(x => x.Course)
                .Include(x => x.Questions)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (a == null) return null;

            return new AssignmentDto
            {
                Id = a.Id,
                Title = a.Title,
                Description = a.Description,
                DueDate = a.DueDate,
                TotalScore = a.TotalScore,
                CourseId = a.CourseId,
                CourseTitle = a.Course != null ? a.Course.Title : string.Empty,
                LessonId = a.LessonId,
                Questions = a.Questions.Select(q => new AssignmentQuestionDto
                {
                    Id = q.Id,
                    QuestionText = q.QuestionText,
                    Type = q.Type,
                    Options = q.Options,
                    Score = q.Score
                }).ToList()
            };
        }

        public async Task<AssignmentDto> CreateAssignmentAsync(int instructorId, CreateAssignmentDto dto)
        {
            var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == dto.CourseId && c.InstructorId == instructorId);
            if (course == null) throw new UnauthorizedAccessException();

            var assignment = new Assignment
            {
                Title = dto.Title,
                Description = dto.Description,
                DueDate = dto.DueDate,
                TotalScore = dto.TotalScore,
                CourseId = dto.CourseId,
                LessonId = dto.LessonId,
                CreatedAt = DateTime.UtcNow,
                Questions = dto.Questions.Select(q => new AssignmentQuestion
                {
                    QuestionText = q.QuestionText,
                    Type = q.Type,
                    Options = q.Options,
                    CorrectAnswer = q.CorrectAnswer,
                    Score = q.Score
                }).ToList()
            };

            _context.Assignments.Add(assignment);

            // 1. NOTIFY STUDENTS (Language-Agnostic Format)
            var enrolledStudents = await _context.CourseEnrollments
                .Where(ce => ce.CourseId == dto.CourseId)
                .Select(ce => ce.StudentId)
                .ToListAsync();

            var notifications = enrolledStudents.Select(studentId => new Notification
            {
                UserId = studentId,
                Message = $"NEW_ASSIGNMENT||{assignment.Title}||{course.Title}||{assignment.DueDate:yyyy-MM-dd}",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            });

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();

            return new AssignmentDto
            {
                Id = assignment.Id,
                Title = assignment.Title,
                Description = assignment.Description,
                DueDate = assignment.DueDate,
                TotalScore = assignment.TotalScore,
                CourseId = assignment.CourseId,
                CourseTitle = course.Title,
                LessonId = assignment.LessonId,
                Questions = assignment.Questions.Select(q => new AssignmentQuestionDto
                {
                    Id = q.Id,
                    QuestionText = q.QuestionText,
                    Type = q.Type,
                    Options = q.Options,
                    Score = q.Score
                }).ToList()
            };
        }

        public async Task<AssignmentSubmissionDto> SubmitAssignmentAsync(int studentId, SubmitAssignmentDto dto)
        {
            var assignment = await _context.Assignments
                .Include(a => a.Questions)
                .Include(a => a.Course)
                .FirstOrDefaultAsync(a => a.Id == dto.AssignmentId);

            if (assignment == null) throw new KeyNotFoundException();

            var submission = new AssignmentSubmission
            {
                StudentId = studentId,
                AssignmentId = dto.AssignmentId,
                SubmissionDate = DateTime.UtcNow,
                IsGraded = false,
                Answers = dto.Answers.Select(a => {
                    var question = assignment.Questions.FirstOrDefault(q => q.Id == a.AssignmentQuestionId);
                    int? score = null;

                    if (question != null && question.Type.ToLower() == "mcq")
                    {
                        score = string.Equals(a.AnswerText, question.CorrectAnswer, StringComparison.OrdinalIgnoreCase) ? question.Score : 0;
                    }

                    return new AssignmentAnswer
                    {
                        AssignmentQuestionId = a.AssignmentQuestionId,
                        AnswerText = a.AnswerText,
                        Score = score
                    };
                }).ToList()
            };

            _context.AssignmentSubmissions.Add(submission);

            // 2. NOTIFY TEACHER (Language-Agnostic Format)
            var student = await _context.Users.FindAsync(studentId);
            var teacherNotification = new Notification
            {
                UserId = assignment.Course.InstructorId,
                Message = $"ASSIGNMENT_SUBMITTED||{student?.Username ?? "Student"}||{assignment.Title}||{assignment.Course.Title}",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.Notifications.Add(teacherNotification);
            await _context.SaveChangesAsync();

            return await GetSubmissionDtoAsync(submission.Id);
        }

        public async Task<bool> GradeAssignmentAsync(int instructorId, int submissionId, GradeAssignmentDto dto)
        {
            var submission = await _context.AssignmentSubmissions
                .Include(s => s.Assignment)
                .ThenInclude(a => a.Course)
                .FirstOrDefaultAsync(s => s.Id == submissionId);

            if (submission == null || submission.Assignment.Course.InstructorId != instructorId)
                return false;

            submission.GivenScore = dto.Score;
            submission.IsGraded = true;

            // 3. NOTIFY STUDENT (Language-Agnostic Format)
            var studentNotification = new Notification
            {
                UserId = submission.StudentId,
                Message = $"ASSIGNMENT_GRADED||{submission.Assignment.Title}||{dto.Score}||{submission.Assignment.TotalScore}",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.Notifications.Add(studentNotification);

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<AssignmentDto>> GetAssignmentsByCourseAsync(int courseId)
        {
            return await _context.Assignments
                .Include(a => a.Course)
                .Where(a => a.CourseId == courseId)
                .Select(a => new AssignmentDto
                {
                    Id = a.Id,
                    Title = a.Title,
                    Description = a.Description,
                    DueDate = a.DueDate,
                    TotalScore = a.TotalScore,
                    CourseId = a.CourseId,
                    CourseTitle = a.Course.Title,
                    LessonId = a.LessonId
                }).ToListAsync();
        }

        public async Task<IEnumerable<AssignmentDto>> GetAssignmentsByLessonAsync(int lessonId)
        {
            return await _context.Assignments
                .Include(a => a.Course)
                .Where(a => a.LessonId == lessonId)
                .Select(a => new AssignmentDto
                {
                    Id = a.Id,
                    Title = a.Title,
                    Description = a.Description,
                    DueDate = a.DueDate,
                    TotalScore = a.TotalScore,
                    CourseId = a.CourseId,
                    CourseTitle = a.Course.Title,
                    LessonId = a.LessonId,
                    Questions = a.Questions.Select(q => new AssignmentQuestionDto
                    {
                        Id = q.Id,
                        QuestionText = q.QuestionText,
                        Type = q.Type,
                        Options = q.Options,
                        Score = q.Score
                    }).ToList()
                }).ToListAsync();
        }

        public async Task<(IEnumerable<AssignmentDto> Pending, IEnumerable<AssignmentSubmissionDto> Completed)> GetStudentAssignmentsAsync(int studentId)
        {
            var enrolledCourseIds = await _context.CourseEnrollments
                .Where(ce => ce.StudentId == studentId)
                .Select(ce => ce.CourseId)
                .ToListAsync();

            var allAssignments = await _context.Assignments
                .Include(a => a.Course)
                .Where(a => enrolledCourseIds.Contains(a.CourseId))
                .ToListAsync();

            var submissions = await _context.AssignmentSubmissions
                .Where(s => s.StudentId == studentId)
                .ToListAsync();

            var submittedAssignmentIds = submissions.Select(s => s.AssignmentId).ToHashSet();

            var pending = allAssignments
                .Where(a => !submittedAssignmentIds.Contains(a.Id))
                .Select(a => new AssignmentDto
                {
                    Id = a.Id,
                    Title = a.Title,
                    Description = a.Description,
                    DueDate = a.DueDate,
                    TotalScore = a.TotalScore,
                    CourseId = a.CourseId,
                    CourseTitle = a.Course.Title,
                    LessonId = a.LessonId
                }).ToList();

            var completed = new List<AssignmentSubmissionDto>();
            foreach (var sub in submissions)
            {
                completed.Add(await GetSubmissionDtoAsync(sub.Id));
            }

            return (pending, completed);
        }

        public async Task<IEnumerable<AssignmentSubmissionDto>> GetSubmissionsForGradingAsync(int instructorId, int courseId, int studentId)
        {
            var submissions = await _context.AssignmentSubmissions
                .Include(s => s.Assignment)
                .ThenInclude(a => a.Course)
                .Where(s => s.Assignment.CourseId == courseId &&
                            s.Assignment.Course.InstructorId == instructorId &&
                            s.StudentId == studentId)
                .ToListAsync();

            var dtos = new List<AssignmentSubmissionDto>();
            foreach (var sub in submissions)
            {
                dtos.Add(await GetSubmissionDtoAsync(sub.Id));
            }

            return dtos;
        }

        private async Task<AssignmentSubmissionDto> GetSubmissionDtoAsync(int submissionId)
        {
            var s = await _context.AssignmentSubmissions
                .Include(sub => sub.Assignment)
                .Include(sub => sub.Student)
                .Include(sub => sub.Answers)
                .ThenInclude(a => a.Question)
                .FirstOrDefaultAsync(sub => sub.Id == submissionId);

            if (s == null) return null;

            return new AssignmentSubmissionDto
            {
                Id = s.Id,
                AssignmentId = s.AssignmentId,
                AssignmentTitle = s.Assignment.Title,
                StudentId = s.StudentId,
                StudentName = s.Student.Username,
                GivenScore = s.GivenScore,
                TotalScore = s.Assignment.TotalScore,
                IsGraded = s.IsGraded,
                SubmissionDate = s.SubmissionDate,
                Answers = s.Answers.Select(a => new AssignmentAnswerDto
                {
                    Id = a.Id,
                    AssignmentQuestionId = a.AssignmentQuestionId,
                    QuestionText = a.Question.QuestionText,
                    QuestionType = a.Question.Type,
                    AnswerText = a.AnswerText,
                    Score = a.Score
                }).ToList()
            };
        }
    }
}