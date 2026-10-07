using System;
using System.Collections.Generic;

namespace ELearningPlatform.Core.Entities
{
    public class Assignment
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime? DueDate { get; set; }
        public int TotalScore { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int CourseId { get; set; }
        public Course? Course { get; set; }

        public int? LessonId { get; set; }
        public Lesson? Lesson { get; set; }

        public ICollection<AssignmentQuestion> Questions { get; set; } = new List<AssignmentQuestion>();
        public ICollection<AssignmentSubmission> Submissions { get; set; } = new List<AssignmentSubmission>();
    }
}