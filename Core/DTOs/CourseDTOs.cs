namespace ELearningPlatform.Core.DTOs
{
    public class CreateCourseDto
    {
        public required string Title { get; set; }
        public required string Description { get; set; }
        public string? CoverImageUrl { get; set; }
        public bool IsFree { get; set; }
        public decimal Price { get; set; }
        public decimal DiscountPercentage { get; set; }
    }

    public class CourseResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? CoverImageUrl { get; set; }
        public string InstructorName { get; set; } = string.Empty;
        public bool IsFree { get; set; }
        public decimal Price { get; set; }
        public decimal DiscountPercentage { get; set; }
        public double Rating { get; set; }
        public int TotalLessonsCount { get; set; }
        public int TotalStudentsCount { get; set; }
    }
}