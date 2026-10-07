namespace ELearningPlatform.Core.Entities
{
    public class ScheduleEvent
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime EventDate { get; set; }
        public string EventType { get; set; } = "Live";
        public Course? Course { get; set; }
    }
}