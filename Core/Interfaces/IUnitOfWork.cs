using System;
using System.Threading.Tasks;
using ELearningPlatform.Core.Entities;

namespace ELearningPlatform.Core.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IGenericRepository<User> Users { get; }
        IGenericRepository<Course> Courses { get; }
        IGenericRepository<Module> Modules { get; }
        IGenericRepository<CourseTask> Tasks { get; }
        IGenericRepository<Submission> Submissions { get; }
        IGenericRepository<Notification> Notifications { get; }

        Task<int> CompleteAsync();
    }
}