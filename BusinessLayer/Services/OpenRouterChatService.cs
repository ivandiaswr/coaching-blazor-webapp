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
        private readonly string _model = "mistralai/mistral-7b-instruct:free";
        private readonly List<(string[] Keywords, ChatResource Resource, double Weight)> _resources;
        private readonly ILogService _logService;
        private readonly Random _random = new Random();

        public OpenRouterChatService(IConfiguration configuration, ILogService logService)
        {
            _apiKey = configuration["OpenRouter:ApiKey"];
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            _logService = logService;

            _resources = new List<(string[] Keywords, ChatResource Resource, double Weight)>
                {
                    (
                        new[] 
                        { 
                            "inner peace", "mindfulness", "calm", "clarity", "resilience", "self-discovery", 
                            "avoid burnout", "stress free", "self-care", "work-life balance", "worthiness", 
                            "personal life", "spiritual", "busy", "overcommitment", "trauma", "overcome", 
                            "self-love", "limiting beliefs", "women", "healing", "real transformation", 
                            "wellness coach", "mental health", "holistic life coaching", "holistic wellbeing", 
                            "emotional wellness coach", "burnout recovery coach", "mindset and clarity coach", 
                            "trauma-informed life coach", "coaching for self-worth and confidence", 
                            "wellness coach online", "personal growth guide", "Christian", "faith-based coaching",
                            "resources", "guide", "files", "file", "link", "download", "pdf", "available resources", 
                            "direct link", "access resources", "get resources", "resource link"
                        },
                        new ChatResource 
                        { 
                            Name = "Find Your Calm, Shape Your Power: A 30-Day Inner Peace Challenge", 
                            Url = "/Files/file1.pdf"
                        },
                        0.8
                    ),
                    (
                        new[] 
                        { 
                            "goals", "success", "focus", "planning", "2025", "motivation", 
                            "smart goals", "finish strong", "career mapping", "direction", 
                            "confidence boost", "live with purpose", "life coaching", "life coach", 
                            "purpose-driven life coaching", "clarity coach for professionals",
                            // Generic resource keywords
                            "resources", "guide", "files", "file", "link", "download", "pdf", "available resources", 
                            "direct link", "access resources", "get resources", "resource link"
                        },
                        new ChatResource 
                        { 
                            Name = "Master Your 2025: Focused, Fresh, and Fearless", 
                            Url = "/Files/file2.pdf"
                        },
                        0.7
                    )
                };
        }

        public List<ChatResource> GetResources(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                _logService.LogInfo("ResourceSuggestion", "No resources suggested: empty or null message");
                return new List<ChatResource>();
            }

            var matchedResources = new List<(ChatResource Resource, double Weight, bool IsGenericRequest)>();
            var genericResourceKeywords = new[] 
            { 
                "resources", "guide", "files", "file", "link", "download", "pdf", 
                "available resources", "direct link", "access resources", "get resources", "resource link" 
            };

            foreach (var (keywords, resource, weight) in _resources)
            {
                bool isGenericRequest = genericResourceKeywords.Any(k => message.Contains(k, StringComparison.OrdinalIgnoreCase));
                bool isThematicMatch = keywords.Except(genericResourceKeywords).Any(k => message.Contains(k, StringComparison.OrdinalIgnoreCase));

                if (isGenericRequest || isThematicMatch)
                {
                    matchedResources.Add((resource, weight, isGenericRequest));
                }
            }

            var selectedResources = matchedResources
                .Where(r => r.IsGenericRequest || _random.NextDouble() < r.Weight)
                .Select(r => r.Resource)
                .Distinct()
                .Take(2)
                .ToList();

            return selectedResources;
        }

        public async Task<ChatMessage> SendMessageAsync(List<ChatMessage> conversationHistory, string userMessage)
        {
            try
            {
                var messages = new List<object>
                {
                    new { 
                        role = "system", 
                        content = @"You are Ítala, a certified life and career coach with a warm, empathetic, and practical approach. 
                        Your mission is to empower clients through mindfulness, resilience, and actionable steps toward personal growth. 
                        Focus on clarity, purpose, and transformation, inspired by your 30-Day Inner Peace Challenge and 2025 success guides. 
                        When relevant, subtly suggest exploring your free resources, but only if it fits the conversation naturally. 
                        Always end with an encouraging call to action, inviting users to email jostic@italaveloso.com or book a session for deeper support."
                    }
                };

                if (conversationHistory.Count > 5)
                {
                    var summary = string.Join(" ", conversationHistory.Take(conversationHistory.Count - 5).Select(m => m.Text).Take(100));
                    messages.Add(new
                    {
                        role = "system",
                        content = $"Conversation summary: {summary}"
                    });
                    conversationHistory = conversationHistory.Skip(conversationHistory.Count - 5).ToList();
                }

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
                    messages,
                    temperature = 0.7,
                    top_p = 0.9,
                    max_tokens = 500
                };

                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("https://openrouter.ai/api/v1/chat/completions", content);

                var responseString = await response.Content.ReadAsStringAsync();
                await _logService.LogInfo("OpenRouter response", responseString);

                if (!response.IsSuccessStatusCode)
                {
                    await _logService.LogError("OpenRouterChatService: response.IsSuccessStatusCode", response.StatusCode.ToString());
                    return new ChatMessage
                    {
                        IsUser = false,
                        Text = "Sorry, I'm having trouble responding right now. Please try again or contact me at jostic@italaveloso.com.",
                        Resources = new List<ChatResource>()
                    };
                }

                var json = JObject.Parse(responseString);
                var text = json["choices"]?[0]?["message"]?["content"]?.ToString()?.Trim();

                if (string.IsNullOrWhiteSpace(text))
                {
                    text = "I’m here to help, but I couldn’t generate a response. Let’s try again or explore my free resources.";
                }

                var resources = GetResources(userMessage);
                if (resources.Any())
                {
                    // Append resource names to the response text
                    var resourceText = "\n\nYou might find this helpful (click to download PDF):\n" + 
                                    string.Join("\n", resources.Select(r => $"* {r.Name}"));
                    text += resourceText;
                    await _logService.LogInfo("ResourceSuggestion", $"Suggested resources: {string.Join(", ", resources.Select(r => r.Name))}");
                }

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
                    Text = "Oops, something went wrong. Please try again or reach out via WhatsApp or email at jostic@italaveloso.com.",
                    Resources = new List<ChatResource>()
                };
            }
        }
    }
}
