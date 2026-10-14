using System.Collections.Generic;
using System.Threading.Tasks;
using ELearningPlatform.Core.DTOs;

namespace ELearningPlatform.Core.Interfaces
{
    public interface ICourseService
    {
        Task<CourseResponseDto> CreateCourseAsync(CreateCourseDto request, int instructorId);
        Task<IEnumerable<CourseResponseDto>> GetAllCoursesAsync();
        Task<bool> EnrollInCourseAsync(int studentId, int courseId);
        Task<IEnumerable<CourseResponseDto>> GetStudentCoursesAsync(int studentId);
        Task<IEnumerable<CourseResponseDto>> GetInstructorCoursesAsync(int instructorId);
        Task<CourseResponseDto?> UpdateCourseAsync(int courseId, UpdateCourseDto request, int instructorId);
    }
}