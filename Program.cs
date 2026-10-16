using ELearningPlatform.Core.Interfaces;
using ELearningPlatform.Hubs;
using ELearningPlatform.Infrastructure.Data;
using ELearningPlatform.Infrastructure.Repositories;
using ELearningPlatform.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System;
using System.Threading.Tasks;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options => {
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
});

// 1. PERFORMANCE FIX: Enable In-Memory Caching
builder.Services.AddMemoryCache();

// 2. PERFORMANCE FIX: Enable Response Compression for API payloads
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// Configure Form Options for large multipart (video) uploads
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = 524288000; 
    options.MemoryBufferThreshold = int.MaxValue;
});

// Set Kestrel max request body size to 500 MB
builder.WebHost.ConfigureKestrel(serverOptions => {
    serverOptions.Limits.MaxRequestBodySize = 524288000; 
});

builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer' [space] and then your valid token.\r\n\r\nExample: \"Bearer eyJhbGciOiJIUzI1NiIs...\""
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddTransient<ICourseService, CourseService>();
builder.Services.AddScoped<IAssignmentService, AssignmentService>();
builder.Services.AddHttpClient<IAIGradingService, AIGradingService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularApp", policy =>
    {
        policy.SetIsOriginAllowed(origin => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var jwtSettings = builder.Configuration.GetSection("Jwt");
var keyString = jwtSettings["Key"];
if (string.IsNullOrEmpty(keyString)) throw new InvalidOperationException("JWT Key is missing.");

var key = Encoding.UTF8.GetBytes(keyString);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/notifications"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddTransient<IEmailService, EmailService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ELearningPlatform.Infrastructure.Data.ApplicationDbContext>();
    try
    {
        context.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AuditLogs')
            BEGIN
                CREATE TABLE AuditLogs (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    AdminId INT NOT NULL,
                    AdminUsername NVARCHAR(100) NOT NULL,
                    Action NVARCHAR(50) NOT NULL,
                    Entity NVARCHAR(50) NOT NULL,
                    Details NVARCHAR(MAX) NOT NULL,
                    Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                );
            END
        ");

        var admin = context.Users.FirstOrDefault(u => u.Email == "admin@fluencyfirst.com");
        if (admin == null)
        {
            context.Users.Add(new ELearningPlatform.Core.Entities.User
            {
                Username = "Super Admin",
                Email = "admin@fluencyfirst.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@12345"),
                Role = ELearningPlatform.Core.Enums.UserRole.Admin,
                IsEmailConfirmed = true,
                TotalXP = 0
            });
            context.SaveChanges();
        }
        else
        {
            admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@12345");
            admin.Role = ELearningPlatform.Core.Enums.UserRole.Admin;
            admin.IsEmailConfirmed = true;
            context.SaveChanges();
        }
    }
    catch { }
}

// 3. PERFORMANCE FIX: Use Response Compression Middleware
app.UseResponseCompression();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AngularApp");

app.UseAuthentication();
app.UseAuthorization();

var contentTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".mkv"] = "video/mp4";
contentTypeProvider.Mappings[".webm"] = "video/webm";
contentTypeProvider.Mappings[".mp4"] = "video/mp4";
contentTypeProvider.Mappings[".mov"] = "video/quicktime";
contentTypeProvider.Mappings[".avi"] = "video/x-msvideo";
contentTypeProvider.Mappings[".ogg"] = "video/ogg";
contentTypeProvider.Mappings[".ogv"] = "video/ogg";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypeProvider
});

app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<ELearningPlatform.Hubs.LiveSessionHub>("/liveHub");

app.MapControllers();
app.Run();