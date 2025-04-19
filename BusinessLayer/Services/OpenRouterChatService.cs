using System.Net.Http.Headers;
using System.Text;
using BusinessLayer.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using ModelLayer.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BusinessLayer.Services
{
    public class OpenRouterChatService : IChatService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model = "mistralai/mistral-7b-instruct:free"; // "mistral/mistral-7b-instruct"
        private readonly List<(string Keyword, ChatResource Resource)> _resources;
        private readonly ILogService _logService;

        public OpenRouterChatService(IConfiguration configuration, ILogService logService)
        {
            _apiKey = configuration["OpenRouter:ApiKey"];
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            _logService = logService;

            _resources = new List<(string Keyword, ChatResource Resource)>
            {
                ("goal setting", new ChatResource { Name = "Goal Setting Guide", Url = "/files/file1.pdf" }),
                ("goal setting", new ChatResource { Name = "Online Tips", Url = "/files/file2.pdf" }),
            };
        }

        public List<ChatResource> GetResources(string message)
        {
            return _resources
                .Where(r => message.Contains(r.Keyword, StringComparison.OrdinalIgnoreCase))
                .Select(r => r.Resource)
                .Distinct()
                .ToList();
        }

        public async Task<ChatMessage> SendMessageAsync(List<ChatMessage> conversationHistory, string userMessage)
        {
            try
            {
                var messages = new List<object>
                {
                    new { 
                        role = "system", 
                        content = @"You are a certified life and career coach named Ítala. You are empathetic, supportive, and practical. End with a call to action and an offer to provide more support. Contact information: jostic@italaveloso.com."                    
                        }
                };

                foreach (var message in conversationHistory)
                {
                    messages.Add(new
                    {
                        role = message.IsUser ? "user" : "assistant",
                        content = message.Text
                    });
                }

                messages.Add(new
                {
                    role = "user",
                    content = userMessage
                });

                var requestBody = new
                {
                    model = _model,
                    messages = messages,
                    temperature = 0.7,
                    top_p = 0.9
                };

                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("https://openrouter.ai/api/v1/chat/completions", content);

                var responseString = await response.Content.ReadAsStringAsync();
                await _logService.LogError("OpenRouter response", responseString);

                if (!response.IsSuccessStatusCode)
                {
                    await _logService.LogError("OpenRouterChatService: response.IsSuccessStatusCode", response.IsSuccessStatusCode.ToString());

                    return new ChatMessage
                    {
                        IsUser = false,
                        Text = "Sorry, something went wrong with the chat model.",
                        Resources = new List<ChatResource>()
                    };
                }

                var json = JObject.Parse(responseString);
                var text = json["choices"]?[0]?["message"]?["content"]?.ToString()?.Trim();

                if (string.IsNullOrWhiteSpace(text))
                    text = "Sorry, I couldn't generate a response right now.";

                var resources = GetResources(userMessage);
                var resourceText = resources.Any() 
                    ? "\n\n" + string.Join("\n", resources.Select(r => $"*Check out our [{r.Name}]({r.Url}) for more tips!*"))
                    : "";
                text += resourceText;

                return new ChatMessage
                {
                    IsUser = false,
                    Text = text,
                    Resources = resources
                };
            }
            catch (Exception ex)
            {
                await _logService.LogError("OpenRouterChatService: SendMessageAsync", ex.ToString());
                return new ChatMessage
                {
                    IsUser = false,
                    Text = "Sorry, something went wrong.",
                    Resources = new List<ChatResource>()
                };
            }
        }
    }
}
