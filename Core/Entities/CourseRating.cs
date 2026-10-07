using System;
using System.ComponentModel.DataAnnotations;

namespace ELearningPlatform.Entities
{
    public class CourseRating
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CourseId { get; set; }

        [Required]
        public string StudentId { get; set; } = string.Empty;

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}