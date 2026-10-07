namespace ELearningPlatform.Core.Entities
{
    public class AssignmentQuestion
    {
        public int Id { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? Options { get; set; }
        public string? CorrectAnswer { get; set; }
        public int Score { get; set; }

        public int AssignmentId { get; set; }
        public Assignment? Assignment { get; set; }
    }
}