using System;
using System.Collections.Generic;

namespace ELearningPlatform.Core.Entities
{
    public class AssignmentSubmission
    {
        public int Id { get; set; }
        public int? GivenScore { get; set; }
        public DateTime SubmissionDate { get; set; } = DateTime.UtcNow;
        public bool IsGraded { get; set; } = false;

        public int StudentId { get; set; }
        public User? Student { get; set; }

        public int AssignmentId { get; set; }
        public Assignment? Assignment { get; set; }

        public ICollection<AssignmentAnswer> Answers { get; set; } = new List<AssignmentAnswer>();
    }
}