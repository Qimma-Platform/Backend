using System.Collections.Generic;
using System.Threading.Tasks;
using ELearningPlatform.Core.DTOs;

namespace ELearningPlatform.Core.Interfaces
{
    public interface IAssignmentService
    {
        Task<AssignmentDto> GetAssignmentByIdAsync(int id);
        Task<AssignmentDto> CreateAssignmentAsync(int instructorId, CreateAssignmentDto dto);
        Task<AssignmentSubmissionDto> SubmitAssignmentAsync(int studentId, SubmitAssignmentDto dto);
        Task<bool> GradeAssignmentAsync(int instructorId, int submissionId, GradeAssignmentDto dto);
        Task<IEnumerable<AssignmentDto>> GetAssignmentsByCourseAsync(int courseId);
        Task<IEnumerable<AssignmentDto>> GetAssignmentsByLessonAsync(int lessonId);
        Task<(IEnumerable<AssignmentDto> Pending, IEnumerable<AssignmentSubmissionDto> Completed)> GetStudentAssignmentsAsync(int studentId);
        Task<IEnumerable<AssignmentSubmissionDto>> GetSubmissionsForGradingAsync(int instructorId, int courseId, int studentId);
    }
}