using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ELearningPlatform.Core.DTOs;
using ELearningPlatform.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ELearningPlatform.Infrastructure.Services
{
    public class AIGradingService : IAIGradingService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public AIGradingService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;
        }

        public async Task<AIGradeResult> GradeSubmissionAsync(string taskDescription, string studentSubmission)
        {
            var apiKey = _config["OpenAI:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
                throw new InvalidOperationException("OpenAI API Key is missing.");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var prompt = $@"
You are an encouraging AI teacher for kids learning digital skills.
Task Details: {taskDescription}
Student Submission: {studentSubmission}

Evaluate the submission. Respond ONLY in JSON format:
{{
  ""score"": <0-100>,
  ""feedback"": ""<short encouraging feedback>""
}}";

            var requestBody = new
            {
                model = "gpt-4o",
                messages = new[]
                {
                    new { role = "system", content = "You are an encouraging AI teacher. You must always output valid JSON." },
                    new { role = "user", content = prompt }
                },
                response_format = new { type = "json_object" },
                temperature = 0.7
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(jsonResponse);
            var messageContent = document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

            if (string.IsNullOrWhiteSpace(messageContent))
                throw new Exception("AI returned empty content.");

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<AIGradeResult>(messageContent, options);

            return result ?? new AIGradeResult { Score = 0, Feedback = "Error grading." };
        }
    }
}