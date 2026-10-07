using System.Collections.Generic;

namespace ELearningPlatform.Core.Entities
{
    public class Module
    {
        public int Id { get; set; }
        public required string Title { get; set; }
        public int OrderIndex { get; set; }

        public int CourseId { get; set; }
        public Course? Course { get; set; }

        public ICollection<CourseTask> Tasks { get; set; } = new List<CourseTask>();
    }
}