namespace ELearningPlatform.Core.Entities
{
    public class CourseTask
    {
        public int Id { get; set; }
        public int? LessonId { get; set; }
        public int? CourseId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string Options { get; set; } = string.Empty;
        public int CorrectOptionIndex { get; set; }
        public int XPReward { get; set; } = 50;

        public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
    }
}