namespace ELearningPlatform.Core.DTOs
{
    public class CreateCourseDto
    {
        public required string Title { get; set; }
        public string? Description { get; set; }
        public string? CoverImageUrl { get; set; }
        public bool IsFree { get; set; }
        public decimal Price { get; set; }
        public decimal DiscountPercentage { get; set; }
    }

    public class UpdateCourseDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? CoverImageUrl { get; set; }
        public bool? IsFree { get; set; }
        public decimal? Price { get; set; }
        public decimal? DiscountPercentage { get; set; }
    }

    public class CourseResponseDto
    {
        private string? _coverImageUrl;

        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? CoverImageUrl
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_coverImageUrl)) return _coverImageUrl;
                if (_coverImageUrl.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase) ||
                    _coverImageUrl.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase) ||
                    _coverImageUrl.StartsWith("data:", System.StringComparison.OrdinalIgnoreCase))
                {
                    return _coverImageUrl;
                }
                return $"http://localhost:5162{(_coverImageUrl.StartsWith('/') ? "" : "/")}{_coverImageUrl}";
            }
            set => _coverImageUrl = value;
        }
        public string InstructorName { get; set; } = string.Empty;
        public bool IsFree { get; set; }
        public decimal Price { get; set; }
        public decimal DiscountPercentage { get; set; }
        public double Rating { get; set; }
        public int TotalLessonsCount { get; set; }
        public int TotalStudentsCount { get; set; }
    }
}