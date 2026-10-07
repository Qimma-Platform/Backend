using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ELearningPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ELearningPlatform.Core.Entities;

namespace ELearningPlatform.Controllers
{
    [Route("api/payment")]
    [ApiController]
    [Authorize]
    public class PaymentController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;

        public PaymentController(ApplicationDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
            _httpClient = new HttpClient();
        }

        [HttpPost("checkout/{courseId}")]
        public async Task<IActionResult> Checkout(int courseId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int studentId)) return Unauthorized("Invalid token");

            var student = await _context.Users.FindAsync(studentId);
            if (student == null) return NotFound("Student not found");

            var course = await _context.Courses.FindAsync(courseId);
            if (course == null || course.IsFree) return BadRequest("Course is not available for purchase or is free.");

            decimal finalPrice = course.Price - (course.Price * course.DiscountPercentage / 100);
            int amountInCents = (int)(finalPrice * 100);

            string secretKey = _config["Paymob:SecretKey"] ?? "";
            string publicKey = _config["Paymob:PublicKey"] ?? "";

            var paymentMethodsList = new List<int>();
            int cardId = _config.GetValue<int>("Paymob:CardIntegrationId");
            int fawryId = _config.GetValue<int>("Paymob:FawryIntegrationId");
            int walletId = _config.GetValue<int>("Paymob:WalletIntegrationId");
            int defaultId = _config.GetValue<int>("Paymob:IntegrationId");

            if (cardId > 0) paymentMethodsList.Add(cardId);
            if (fawryId > 0) paymentMethodsList.Add(fawryId);
            if (walletId > 0) paymentMethodsList.Add(walletId);

            if (paymentMethodsList.Count == 0 && defaultId > 0)
            {
                paymentMethodsList.Add(defaultId);
            }

            string rawPhone = student.PhoneNumber ?? "01012345678";
            string cleanMobile = new string(rawPhone.Where(char.IsDigit).ToArray());

            string firstName = student.Username ?? "Student";
            string lastName = "ELearning";
            if (firstName.Contains(" "))
            {
                var names = firstName.Split(' ');
                firstName = names[0];
                lastName = names[names.Length - 1];
            }

            var payload = new
            {
                amount = amountInCents,
                currency = "EGP",
                payment_methods = paymentMethodsList.ToArray(),
                billing_data = new
                {
                    first_name = firstName,
                    last_name = lastName,
                    email = student.Email ?? "student@elearning.com",
                    phone_number = cleanMobile,
                    apartment = "NA",
                    floor = "NA",
                    street = "NA",
                    building = "NA",
                    city = "Cairo",
                    country = "EG"
                },
                items = new[]
                {
                    new {
                        name = course.Title,
                        amount = amountInCents,
                        description = "Course Subscription",
                        quantity = 1
                    }
                },
                extras = new
                {
                    course_id = course.Id,
                    student_id = student.Id
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Token {secretKey}");

            Console.WriteLine("🚀 Payment Methods IDs being sent: " + string.Join(", ", paymentMethodsList));
            var response = await _httpClient.PostAsync("https://accept.paymob.com/v1/intention/", content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var paymobResponse = JsonSerializer.Deserialize<JsonElement>(responseString);

                if (paymobResponse.TryGetProperty("client_secret", out var clientSecretProp))
                {
                    string clientSecret = clientSecretProp.GetString();
                    string checkoutUrl = $"https://accept.paymob.com/unifiedcheckout/?publicKey={publicKey}&clientSecret={clientSecret}";

                    return Ok(new
                    {
                        message = "Payment link generated successfully",
                        checkoutUrl = checkoutUrl
                    });
                }
            }

            return BadRequest(new { message = "Failed to generate Paymob Link", details = responseString });
        }

        [AllowAnonymous]
        [HttpPost("webhook")]
        public async Task<IActionResult> PaymobWebhook([FromBody] JsonElement payload)
        {
            try
            {
                Console.WriteLine("Webhook Received successfully!");

                var obj = payload.GetProperty("obj");
                bool isSuccess = obj.GetProperty("success").GetBoolean();

                if (isSuccess)
                {
                    var paymentKeyClaims = obj.GetProperty("payment_key_claims");
                    var extra = paymentKeyClaims.GetProperty("extra");

                    int courseId = extra.GetProperty("course_id").GetInt32();
                    int studentId = extra.GetProperty("student_id").GetInt32();

                    int amountCents = obj.GetProperty("amount_cents").GetInt32();
                    decimal totalPaid = amountCents / 100m;

                    var course = await _context.Courses.FindAsync(courseId);
                    if (course != null)
                    {
                        var isAlreadyEnrolled = await _context.CourseEnrollments
                            .AnyAsync(e => e.StudentId == studentId && e.CourseId == courseId);

                        if (!isAlreadyEnrolled)
                        {
                            var enrollment = new CourseEnrollment
                            {
                                StudentId = studentId,
                                CourseId = courseId
                            };
                            _context.CourseEnrollments.Add(enrollment);

                            decimal instructorShare = totalPaid * 0.80m;
                            var instructor = await _context.Users.FindAsync(course.InstructorId);
                            if (instructor != null)
                            {
                                instructor.WalletBalance += instructorShare;
                            }

                            // إرسال إشعار للمعلم بانضمام طالب جديد
                            var notifyTeacher = new Notification
                            {
                                UserId = course.InstructorId,
                                Message = $"طالب جديد انضم! لقد قام طالب بالاشتراك في كورس '{course.Title}'.",
                                CreatedAt = DateTime.UtcNow,
                                IsRead = false
                            };
                            _context.Notifications.Add(notifyTeacher);

                            await _context.SaveChangesAsync();
                            Console.WriteLine($"🎉 Success: Student {studentId} successfully enrolled in Course {courseId}. Instructor earned {instructorShare} EGP.");
                        }
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Webhook Error: " + ex.Message);
                return Ok();
            }
        }
    }
}