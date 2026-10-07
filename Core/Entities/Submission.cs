using System;

namespace ELearningPlatform.Core.Entities
{
    public class Submission
    {
        public int Id { get; set; }
        public required string Content { get; set; }
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        public int StudentId { get; set; }
        public User? Student { get; set; }

        public int CourseTaskId { get; set; }
        public CourseTask? CourseTask { get; set; }

        public int? AIScore { get; set; }
        public string? AIFeedback { get; set; }

        public bool IsApprovedByInstructor { get; set; } = false;
        public string? InstructorNotes { get; set; }
    }
}