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
            var course = new Course
            {
                Title = request.Title,
                Description = request.Description,
                CoverImageUrl = request.CoverImageUrl,
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
                .Include(c => c.Instructor)
                .Select(c => new CourseResponseDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    Description = c.Description,
                    CoverImageUrl = c.CoverImageUrl,
                    InstructorName = c.Instructor!.Username,
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
                .Where(ce => ce.StudentId == studentId)
                .Include(ce => ce.Course)
                .ThenInclude(c => c!.Instructor)
                .Select(ce => new CourseResponseDto
                {
                    Id = ce.Course!.Id,
                    Title = ce.Course.Title,
                    Description = ce.Course.Description,
                    CoverImageUrl = ce.Course.CoverImageUrl,
                    InstructorName = ce.Course.Instructor!.Username,
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
                .Where(c => c.InstructorId == instructorId)
                .Include(c => c.Instructor)
                .Select(c => new CourseResponseDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    Description = c.Description,
                    CoverImageUrl = c.CoverImageUrl,
                    InstructorName = c.Instructor!.Username,
                    IsFree = c.IsFree,
                    Price = c.Price,
                    DiscountPercentage = c.DiscountPercentage,
                    Rating = (double)(c.Rating ?? 0m),
                    TotalLessonsCount = _context.Lessons.Count(l => l.CourseId == c.Id),
                    TotalStudentsCount = _context.CourseEnrollments.Count(e => e.CourseId == c.Id)
                }).ToListAsync();
        }
    }
}