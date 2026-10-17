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

            string clientDomain = "https://qimma-academy.vercel.app";
            if (Request.Headers.TryGetValue("Origin", out var origin) && !string.IsNullOrWhiteSpace(origin))
            {
                clientDomain = origin.ToString().TrimEnd('/');
            }
            else if (Request.Headers.TryGetValue("Referer", out var referer) && !string.IsNullOrWhiteSpace(referer))
            {
                try
                {
                    var uri = new Uri(referer.ToString());
                    clientDomain = $"{uri.Scheme}://{uri.Authority}";
                }
                catch { }
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
                },
                special_reference = $"{student.Id}_{course.Id}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
                notification_url = "https://qimma.runasp.net/api/payment/webhook",
                redirection_url = $"{clientDomain}/learn/{course.Id}?enrolled=true"
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
                Console.WriteLine("Webhook Received: " + payload.ToString());

                JsonElement obj;
                if (payload.TryGetProperty("obj", out var objProp))
                {
                    obj = objProp;
                }
                else
                {
                    obj = payload;
                }

                bool isSuccess = false;
                if (obj.TryGetProperty("success", out var successProp))
                {
                    if (successProp.ValueKind == JsonValueKind.True) isSuccess = true;
                    else if (successProp.ValueKind == JsonValueKind.String && bool.TryParse(successProp.GetString(), out bool b)) isSuccess = b;
                }

                if (isSuccess)
                {
                    int courseId = 0;
                    int studentId = 0;

                    // 1. Try payment_key_claims.extra
                    if (obj.TryGetProperty("payment_key_claims", out var pkc) && pkc.TryGetProperty("extra", out var extra))
                    {
                        if (extra.TryGetProperty("course_id", out var cIdProp)) courseId = cIdProp.GetInt32();
                        if (extra.TryGetProperty("student_id", out var sIdProp)) studentId = sIdProp.GetInt32();
                    }

                    // 2. Try obj.data.extras or obj.extras
                    if ((courseId == 0 || studentId == 0) && obj.TryGetProperty("data", out var dataProp))
                    {
                        if (dataProp.TryGetProperty("extras", out var dExtras))
                        {
                            if (dExtras.TryGetProperty("course_id", out var cIdProp)) courseId = cIdProp.GetInt32();
                            if (dExtras.TryGetProperty("student_id", out var sIdProp)) studentId = sIdProp.GetInt32();
                        }
                    }

                    // 3. Try special_reference / merchant_order_id (format: {studentId}_{courseId}_{timestamp})
                    if (courseId == 0 || studentId == 0)
                    {
                        string specRef = "";
                        if (obj.TryGetProperty("special_reference", out var sRefProp)) specRef = sRefProp.GetString() ?? "";
                        if (string.IsNullOrEmpty(specRef) && obj.TryGetProperty("order", out var orderProp) && orderProp.TryGetProperty("merchant_order_id", out var mOrderIdProp))
                        {
                            specRef = mOrderIdProp.GetString() ?? "";
                        }

                        if (!string.IsNullOrEmpty(specRef) && specRef.Contains('_'))
                        {
                            var parts = specRef.Split('_');
                            if (parts.Length >= 2 && int.TryParse(parts[0], out int sId) && int.TryParse(parts[1], out int cId))
                            {
                                studentId = sId;
                                courseId = cId;
                            }
                        }
                    }

                    if (courseId == 0 || studentId == 0)
                    {
                        Console.WriteLine("⚠️ Webhook could not find courseId or studentId in payload.");
                        return Ok();
                    }

                    int amountCents = 0;
                    if (obj.TryGetProperty("amount_cents", out var acProp)) amountCents = acProp.GetInt32();
                    decimal totalPaid = amountCents > 0 ? (amountCents / 100m) : 0m;

                    var course = await _context.Courses.FindAsync(courseId);
                    if (course != null)
                    {
                        if (totalPaid <= 0)
                        {
                            totalPaid = course.Price - (course.Price * course.DiscountPercentage / 100m);
                        }
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

                            var student = await _context.Users.FindAsync(studentId);
                            string sName = student?.Username ?? "طالب";

                            decimal instructorShare = totalPaid * 0.80m;
                            var instructor = await _context.Users.FindAsync(course.InstructorId);
                            if (instructor != null)
                            {
                                instructor.WalletBalance += instructorShare;
                            }

                            _context.PaymentTransactions.Add(new ELearningPlatform.Core.Entities.PaymentTransaction
                            {
                                StudentId = studentId,
                                StudentName = sName,
                                StudentEmail = student?.Email ?? "",
                                StudentPhone = student?.PhoneNumber,
                                CourseId = course.Id,
                                CourseTitle = course.Title,
                                InstructorId = course.InstructorId,
                                InstructorName = instructor?.Username ?? "Instructor",
                                AmountPaid = totalPaid,
                                PaymentStatus = "PAID",
                                EnrolledAt = DateTime.UtcNow
                            });

                            decimal platformShare = totalPaid * 0.20m;

                            // إرسال إشعار للمعلم بانضمام طالب جديد
                            var notifyTeacher = new Notification
                            {
                                UserId = course.InstructorId,
                                Message = $"طالب جديد انضم! لقد قام الطالب '{sName}' بالاشتراك في كورس '{course.Title}'.",
                                CreatedAt = DateTime.UtcNow,
                                IsRead = false
                            };
                            _context.Notifications.Add(notifyTeacher);

                            // إرسال إشعار للمشرفين
                            var adminIds = await _context.Users.Where(u => (int)u.Role == 2).Select(u => u.Id).ToListAsync();
                            foreach (var aId in adminIds)
                            {
                                _context.Notifications.Add(new Notification
                                {
                                    UserId = aId,
                                    Message = $"ENROLLMENT_PAID||اشتراك كورس مدفوع: قام الطالب '{sName}' بالاشتراك في كورس '{course.Title}' بمبلغ {totalPaid} ج.م (نسبة المنصة: {platformShare} ج.م، نسبة المعلم: {instructorShare} ج.م)||Paid Enrollment: Student '{sName}' enrolled in course '{course.Title}' for {totalPaid} EGP",
                                    CreatedAt = DateTime.UtcNow,
                                    IsRead = false
                                });
                            }

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