using System;

namespace ELearningPlatform.Core.Entities
{
    public class AuditLog
    {
        public int Id { get; set; }
        public int AdminId { get; set; }
        public string AdminUsername { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Entity { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
