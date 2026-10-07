using System.Threading.Tasks;
using ELearningPlatform.Core.Entities;
using ELearningPlatform.Core.Interfaces;
using ELearningPlatform.Infrastructure.Data;

namespace ELearningPlatform.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;

        public IGenericRepository<User> Users { get; private set; }
        public IGenericRepository<Course> Courses { get; private set; }
        public IGenericRepository<Module> Modules { get; private set; }
        public IGenericRepository<CourseTask> Tasks { get; private set; }
        public IGenericRepository<Submission> Submissions { get; private set; }
        public IGenericRepository<Notification> Notifications { get; private set; }

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;

            Users = new GenericRepository<User>(_context);
            Courses = new GenericRepository<Course>(_context);
            Modules = new GenericRepository<Module>(_context);
            Tasks = new GenericRepository<CourseTask>(_context);
            Submissions = new GenericRepository<Submission>(_context);
            Notifications = new GenericRepository<Notification>(_context);
        }

        public async Task<int> CompleteAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}