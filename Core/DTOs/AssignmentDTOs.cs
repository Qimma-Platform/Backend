using System;
using System.Collections.Generic;

namespace ELearningPlatform.Core.DTOs
{
    public class CreateAssignmentDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime? DueDate { get; set; }
        public int TotalScore { get; set; }
        public int CourseId { get; set; }
        public int? LessonId { get; set; }
        public List<CreateAssignmentQuestionDto> Questions { get; set; } = new List<CreateAssignmentQuestionDto>();
    }

    public class CreateAssignmentQuestionDto
    {
        public string QuestionText { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? Options { get; set; }
        public string? CorrectAnswer { get; set; }
        public int Score { get; set; }
    }

    public class SubmitAssignmentDto
    {
        public int AssignmentId { get; set; }
        public List<SubmitAssignmentAnswerDto> Answers { get; set; } = new List<SubmitAssignmentAnswerDto>();
    }

    public class SubmitAssignmentAnswerDto
    {
        public int AssignmentQuestionId { get; set; }
        public string AnswerText { get; set; } = string.Empty;
    }

    public class GradeAssignmentDto
    {
        public int Score { get; set; }
    }

    public class AssignmentDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime? DueDate { get; set; }
        public int TotalScore { get; set; }
        public int CourseId { get; set; }
        public string CourseTitle { get; set; } = string.Empty;
        public int? LessonId { get; set; }
        public List<AssignmentQuestionDto> Questions { get; set; } = new List<AssignmentQuestionDto>();
    }

    public class AssignmentQuestionDto
    {
        public int Id { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? Options { get; set; }
        public int Score { get; set; }
    }

    public class AssignmentSubmissionDto
    {
        public int Id { get; set; }
        public int AssignmentId { get; set; }
        public string AssignmentTitle { get; set; } = string.Empty;
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public int? GivenScore { get; set; }
        public int TotalScore { get; set; }
        public bool IsGraded { get; set; }
        public DateTime SubmissionDate { get; set; }
        public List<AssignmentAnswerDto> Answers { get; set; } = new List<AssignmentAnswerDto>();
    }

    public class AssignmentAnswerDto
    {
        public int Id { get; set; }
        public int AssignmentQuestionId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string QuestionType { get; set; } = string.Empty;
        public string AnswerText { get; set; } = string.Empty;
        public int? Score { get; set; }
        public string? CorrectAnswer { get; set; }
        public bool? IsCorrect { get; set; }
    }
}