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
    public class CourseService : ICourseService
    {
        private readonly ApplicationDbContext _context;

        public CourseService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<CourseResponseDto> CreateCourseAsync(CreateCourseDto request, int instructorId)
        {
            var savedCoverUrl = SaveBase64Image(request.CoverImageUrl);

            var course = new Course
            {
                Title = request.Title,
                Description = request.Description,
                CoverImageUrl = savedCoverUrl,
                InstructorId = instructorId,
                IsFree = request.IsFree,
                Price = request.Price,
                DiscountPercentage = request.DiscountPercentage
            };

            _context.Courses.Add(course);
            await _context.SaveChangesAsync();

            var instructor = await _context.Users.FindAsync(instructorId);

            return new CourseResponseDto
            {
                Id = course.Id,
                Title = course.Title,
                Description = course.Description,
                CoverImageUrl = course.CoverImageUrl,
                InstructorName = instructor?.Username ?? string.Empty,
                IsFree = course.IsFree,
                Price = course.Price,
                DiscountPercentage = course.DiscountPercentage,
                Rating = (double)(course.Rating ?? 0m),
                TotalLessonsCount = 0,
                TotalStudentsCount = 0
            };
        }

        public async Task<IEnumerable<CourseResponseDto>> GetAllCoursesAsync()
        {
            return await _context.Courses
                .AsNoTracking()
                .Select(c => new CourseResponseDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    Description = c.Description,
                    CoverImageUrl = c.CoverImageUrl,
                    InstructorName = c.Instructor != null ? c.Instructor.Username : string.Empty,
                    IsFree = c.IsFree,
                    Price = c.Price,
                    DiscountPercentage = c.DiscountPercentage,
                    Rating = (double)(c.Rating ?? 0m),
                    TotalLessonsCount = _context.Lessons.Count(l => l.CourseId == c.Id),
                    TotalStudentsCount = _context.CourseEnrollments.Count(e => e.CourseId == c.Id)
                }).ToListAsync();
        }

        public async Task<bool> EnrollInCourseAsync(int studentId, int courseId)
        {
            var exists = await _context.CourseEnrollments
                .AnyAsync(ce => ce.StudentId == studentId && ce.CourseId == courseId);

            if (exists) return false;

            var enrollment = new CourseEnrollment
            {
                StudentId = studentId,
                CourseId = courseId,
                EnrolledAt = DateTime.UtcNow
            };

            _context.CourseEnrollments.Add(enrollment);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<CourseResponseDto>> GetStudentCoursesAsync(int studentId)
        {
            return await _context.CourseEnrollments
                .AsNoTracking()
                .Where(ce => ce.StudentId == studentId)
                .Select(ce => new CourseResponseDto
                {
                    Id = ce.Course!.Id,
                    Title = ce.Course.Title,
                    Description = ce.Course.Description,
                    CoverImageUrl = ce.Course.CoverImageUrl,
                    InstructorName = ce.Course.Instructor != null ? ce.Course.Instructor.Username : string.Empty,
                    IsFree = ce.Course.IsFree,
                    Price = ce.Course.Price,
                    DiscountPercentage = ce.Course.DiscountPercentage,
                    Rating = (double)(ce.Course.Rating ?? 0m),
                    TotalLessonsCount = _context.Lessons.Count(l => l.CourseId == ce.CourseId),
                    TotalStudentsCount = _context.CourseEnrollments.Count(e => e.CourseId == ce.CourseId)
                }).ToListAsync();
        }

        public async Task<IEnumerable<CourseResponseDto>> GetInstructorCoursesAsync(int instructorId)
        {
            return await _context.Courses
                .AsNoTracking()
                .Where(c => c.InstructorId == instructorId)
                .Select(c => new CourseResponseDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    Description = c.Description,
                    CoverImageUrl = c.CoverImageUrl,
                    InstructorName = c.Instructor != null ? c.Instructor.Username : string.Empty,
                    IsFree = c.IsFree,
                    Price = c.Price,
                    DiscountPercentage = c.DiscountPercentage,
                    Rating = (double)(c.Rating ?? 0m),
                    TotalLessonsCount = _context.Lessons.Count(l => l.CourseId == c.Id),
                    TotalStudentsCount = _context.CourseEnrollments.Count(e => e.CourseId == c.Id)
                }).ToListAsync();
        }

        public async Task<CourseResponseDto?> UpdateCourseAsync(int courseId, UpdateCourseDto request, int instructorId)
        {
            var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == courseId && c.InstructorId == instructorId);
            if (course == null) return null;

            if (!string.IsNullOrWhiteSpace(request.Title))
                course.Title = request.Title;

            if (!string.IsNullOrWhiteSpace(request.Description))
                course.Description = request.Description;

            if (request.CoverImageUrl != null)
            {
                course.CoverImageUrl = SaveBase64Image(request.CoverImageUrl);
            }

            if (request.IsFree.HasValue)
                course.IsFree = request.IsFree.Value;

            if (request.Price.HasValue)
                course.Price = request.Price.Value;

            if (request.DiscountPercentage.HasValue)
                course.DiscountPercentage = request.DiscountPercentage.Value;

            await _context.SaveChangesAsync();

            var instructor = await _context.Users.FindAsync(instructorId);

            return new CourseResponseDto
            {
                Id = course.Id,
                Title = course.Title,
                Description = course.Description,
                CoverImageUrl = course.CoverImageUrl,
                InstructorName = instructor?.Username ?? string.Empty,
                IsFree = course.IsFree,
                Price = course.Price,
                DiscountPercentage = course.DiscountPercentage,
                Rating = (double)(course.Rating ?? 0m),
                TotalLessonsCount = _context.Lessons.Count(l => l.CourseId == course.Id),
                TotalStudentsCount = _context.CourseEnrollments.Count(e => e.CourseId == course.Id)
            };
        }

        private static string? SaveBase64Image(string? base64Data)
        {
            if (string.IsNullOrWhiteSpace(base64Data)) return null;
            if (!base64Data.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
            {
                return base64Data;
            }

            try
            {
                var match = System.Text.RegularExpressions.Regex.Match(base64Data, @"^data:image\/(?<type>[a-zA-Z0-9\+\-]+);base64,(?<data>.+)$");
                if (!match.Success) return base64Data;

                var ext = match.Groups["type"].Value.ToLower();
                if (ext == "jpeg") ext = "jpg";
                else if (ext.Contains("+xml")) ext = "svg";

                var bytes = Convert.FromBase64String(match.Groups["data"].Value);
                var fileName = $"course_{Guid.NewGuid():N}.{ext}";
                var folder = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot", "uploads", "courses");
                if (!System.IO.Directory.Exists(folder)) System.IO.Directory.CreateDirectory(folder);

                var filePath = System.IO.Path.Combine(folder, fileName);
                System.IO.File.WriteAllBytes(filePath, bytes);

                return $"/uploads/courses/{fileName}";
            }
            catch
            {
                return base64Data;
            }
        }
    }
}