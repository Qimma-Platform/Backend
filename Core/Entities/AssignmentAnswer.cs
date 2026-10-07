namespace ELearningPlatform.Core.Entities
{
    public class AssignmentAnswer
    {
        public int Id { get; set; }
        public string AnswerText { get; set; } = string.Empty;
        public int? Score { get; set; }

        public int AssignmentSubmissionId { get; set; }
        public AssignmentSubmission? Submission { get; set; }

        public int AssignmentQuestionId { get; set; }
        public AssignmentQuestion? Question { get; set; }
    }
}