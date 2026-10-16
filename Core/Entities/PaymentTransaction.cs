using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELearningPlatform.Core.Entities
{
    public class PaymentTransaction
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string StudentEmail { get; set; } = string.Empty;
        public string? StudentPhone { get; set; }
        public int CourseId { get; set; }
        public string CourseTitle { get; set; } = string.Empty;
        public int InstructorId { get; set; }
        public string InstructorName { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountPaid { get; set; }
        public string PaymentStatus { get; set; } = "PAID";
        public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    }
}
