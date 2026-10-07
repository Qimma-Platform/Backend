using System;

namespace ELearningPlatform.Core.Entities
{
    public class CourseEnrollment
    {
        public int StudentId { get; set; }
        public User? Student { get; set; }

        public int CourseId { get; set; }
        public Course? Course { get; set; }

        public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    }
}