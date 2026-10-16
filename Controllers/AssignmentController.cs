using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ELearningPlatform.Core.DTOs;
using ELearningPlatform.Core.Interfaces;

namespace ELearningPlatform.Controllers
{
    [Route("api/assignments")]
    [ApiController]
    [Authorize]
    public class AssignmentController : ControllerBase
    {
        private readonly IAssignmentService _assignmentService;

        public AssignmentController(IAssignmentService assignmentService)
        {
            _assignmentService = assignmentService;
        }

        private int GetUserId()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                          User.FindFirst("id")?.Value ??
                          User.FindFirst("UserId")?.Value;
            return int.TryParse(idClaim, out var id) ? id : 0;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetAssignmentById(int id)
        {
            var result = await _assignmentService.GetAssignmentByIdAsync(id);
            if (result == null) return NotFound(new { message = "Assignment not found." });
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> CreateAssignment([FromBody] CreateAssignmentDto dto)
        {
            try
            {
                var result = await _assignmentService.CreateAssignmentAsync(GetUserId(), dto);
                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("submit")]
        public async Task<IActionResult> SubmitAssignment([FromBody] SubmitAssignmentDto dto)
        {
            try
            {
                var result = await _assignmentService.SubmitAssignmentAsync(GetUserId(), dto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("grade/{submissionId}")]
        public async Task<IActionResult> GradeAssignment(int submissionId, [FromBody] GradeAssignmentDto dto)
        {
            try
            {
                var success = await _assignmentService.GradeAssignmentAsync(GetUserId(), submissionId, dto);

                if (!success)
                    return BadRequest(new { message = "Failed to grade assignment or unauthorized." });

                return Ok(new { message = "Grade saved successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("course/{courseId}")]
        public async Task<IActionResult> GetAssignmentsByCourse(int courseId)
        {
            var result = await _assignmentService.GetAssignmentsByCourseAsync(courseId);
            return Ok(result);
        }

        [HttpGet("lesson/{lessonId}")]
        public async Task<IActionResult> GetAssignmentsByLesson(int lessonId)
        {
            var result = await _assignmentService.GetAssignmentsByLessonAsync(lessonId);
            return Ok(result);
        }

        [HttpGet("student")]
        public async Task<IActionResult> GetStudentAssignments()
        {
            var (pending, missed, completed) = await _assignmentService.GetStudentAssignmentsAsync(GetUserId());
            return Ok(new { Pending = pending, Missed = missed, Completed = completed });
        }

        [HttpGet("grading/course/{courseId}/student/{studentId}")]
        public async Task<IActionResult> GetSubmissionsForGrading(int courseId, int studentId)
        {
            var result = await _assignmentService.GetSubmissionsForGradingAsync(GetUserId(), courseId, studentId);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAssignment(int id)
        {
            try
            {
                var success = await _assignmentService.DeleteAssignmentAsync(GetUserId(), id, User.IsInRole("Admin"));
                if (!success) return NotFound(new { message = "Assignment not found." });
                return Ok(new { message = "Assignment deleted successfully." });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("lesson/{lessonId}")]
        public async Task<IActionResult> DeleteAssignmentByLesson(int lessonId)
        {
            try
            {
                var success = await _assignmentService.DeleteAssignmentByLessonAsync(GetUserId(), lessonId, User.IsInRole("Admin"));
                if (!success) return NotFound(new { message = "No assignments found for this lesson." });
                return Ok(new { message = "Assignment deleted successfully." });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}