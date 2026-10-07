using System.Threading.Tasks;
using ELearningPlatform.Core.DTOs;

namespace ELearningPlatform.Core.Interfaces
{
    public interface IAIGradingService
    {
        Task<AIGradeResult> GradeSubmissionAsync(string taskDescription, string studentSubmission);
    }
}