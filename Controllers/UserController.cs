using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ELearningPlatform.Infrastructure.Data;
using ELearningPlatform.Core.Entities;

namespace ELearningPlatform.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public UserController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("profile")]
        [Authorize]
        public async Task<IActionResult> GetProfile()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            return Ok(new
            {
                user.Id,
                user.Username,
                user.Email,
                user.PhoneNumber,
                user.Bio,
                user.ProfileImage,
                user.TotalXP,
                user.Role,
                user.Specialization
            });
        }

        [HttpPut("profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto model)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            user.Username = model.Username;
            user.PhoneNumber = model.PhoneNumber;
            user.Bio = model.Bio;
            user.Specialization = model.Specialization;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Profile updated" });
        }

        [HttpPost("upload-avatar")]
        [Authorize]
        public async Task<IActionResult> UploadAvatar(IFormFile file)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            if (file == null || file.Length == 0) return BadRequest("No file uploaded.");

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "avatars");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            user.ProfileImage = $"/uploads/avatars/{uniqueFileName}";
            await _context.SaveChangesAsync();

            return Ok(new { profileImage = user.ProfileImage });
        }

        [HttpPost("upload-cover")]
        [Authorize]
        public async Task<IActionResult> UploadCover(IFormFile file)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            if (file == null || file.Length == 0) return BadRequest("No file uploaded.");

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "covers");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            user.CoverImage = $"/uploads/covers/{uniqueFileName}";
            await _context.SaveChangesAsync();

            return Ok(new { coverImage = user.CoverImage });
        }

        [HttpGet("teacher/{id}")]
        [Authorize]
        public async Task<IActionResult> GetTeacherProfile(int id)
        {
            var teacher = await _context.Users
                .Where(u => u.Id == id)
                .Select(u => new {
                    u.Id,
                    u.Username,
                    u.ProfileImage,
                    u.CoverImage,
                    u.Bio,
                    u.Specialization
                }).FirstOrDefaultAsync();

            if (teacher == null) return NotFound("Teacher not found");

            var courses = await _context.Courses
                .Where(c => c.InstructorId == id)
                .Select(c => new {
                    id = c.Id,
                    title = c.Title,
                    description = c.Description,
                    coverImageUrl = c.CoverImageUrl,
                    price = c.Price,
                    isFree = c.IsFree,
                    discountPercentage = c.DiscountPercentage,
                    instructorName = teacher.Username,
                    instructorId = teacher.Id
                }).ToListAsync();

            return Ok(new { teacher, courses });
        }

        [HttpGet("teachers")]
        [Authorize]
        public async Task<IActionResult> GetAllTeachers()
        {
            var users = await _context.Users.ToListAsync();

            var teachers = users
                .Where(u => u.Role.ToString() == "Teacher" || ((int)u.Role) == 1)
                .Select(u => new {
                    u.Id,
                    u.Username,
                    u.ProfileImage,
                    u.Specialization
                }).ToList();

            return Ok(teachers);
        }
    }

    public class UpdateProfileDto
    {
        public string Username { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public string Specialization { get; set; } = string.Empty;
    }
}