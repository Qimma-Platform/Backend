using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELearningPlatform.Core.Entities
{
    public class QuizAttempt
    {
        [Key]
        public int Id { get; set; }
        public int StudentId { get; set; }
        public int? LessonId { get; set; }
        public int? CourseId { get; set; }
        public int AttemptNumber { get; set; }
        public int Score { get; set; }
        public int TotalQuestions { get; set; }
        public DateTime AttemptDate { get; set; }

        [ForeignKey("StudentId")]
        public User? Student { get; set; }

        [ForeignKey("LessonId")]
        public Lesson? Lesson { get; set; }

        [ForeignKey("CourseId")]
        public Course? Course { get; set; }
    }
}