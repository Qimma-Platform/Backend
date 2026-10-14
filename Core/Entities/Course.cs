using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELearningPlatform.Core.Entities
{
    public class Course
    {
        public int Id { get; set; }
        public required string Title { get; set; }
        public string? Description { get; set; }
        public string? CoverImageUrl { get; set; }
        public int InstructorId { get; set; }
        public User? Instructor { get; set; }
        public bool IsFree { get; set; } = false;
        public decimal Price { get; set; }
        public decimal DiscountPercentage { get; set; }

        [Column(TypeName = "decimal(18,1)")]
        public decimal? Rating { get; set; }
        public int TotalRatingsCount { get; set; } = 0;

        public ICollection<Module> Modules { get; set; } = new List<Module>();
        public ICollection<CourseEnrollment> Enrollments { get; set; } = new List<CourseEnrollment>();
    }
}