using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using BusinessLayer.Services.Interfaces;

namespace BusinessLayer.Services
{
    public class GoogleReviewsService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _placeId;
        private readonly ILogService _logService;

        public GoogleReviewsService(HttpClient httpClient, IConfiguration configuration, ILogService logService)
        {
            _httpClient = httpClient;
            _apiKey = configuration["GooglePlaces:ApiKey"] ?? string.Empty;
            _placeId = configuration["GooglePlaces:PlaceId"] ?? string.Empty;
            _logService = logService;
        }

        public async Task<(List<GoogleReview> reviews, double rating, int totalReviews)> GetReviewsAsync()
        {
            try
            {
                await _logService.LogInfo("GoogleReviews", "Loading hardcoded Google reviews data");

                // Always return hardcoded data - no API calls
                return GetFallbackData();
            }
            catch (Exception ex)
            {
                await _logService.LogError("GoogleReviews", $"Error loading Google reviews: {ex.Message}");
                return GetFallbackData();
            }
        }

        private (List<GoogleReview> reviews, double rating, int totalReviews) GetFallbackData()
        {
            var fallbackReviews = new List<GoogleReview>
            {
                new GoogleReview
                {
                    ReviewerName = "Karen",
                    Rating = 5,
                    ReviewText = "I have been working with the lovely Itala Veloso of Jostic for ten weeks and she has been nothing short of a revelation. Itala is an awesome coach, a truly wonderful person and extremely passionate about her work and her clients. I look forward to working with her again very soon! If you are a bit lost, stuck, needing direction or just a helping hand to navigate life's difficulties, please do have a chat with Itala. I can't thank her enough.",
                    ReviewDate = DateTime.Now.AddMonths(-2),
                    ProfilePhotoUrl = ""
                },
                new GoogleReview
                {
                    ReviewerName = "Esmae",
                    Rating = 5,
                    ReviewText = "Thank you SO much for the life coaching…. I highly recommend!! Ítala listens deeply and really empathised with my situation and helped me so much. If you're considering working with her, let this be your sign! She's helped me so much.. thank you! 🙏🏽💓",
                    ReviewDate = DateTime.Now.AddMonths(-1),
                    ProfilePhotoUrl = ""
                },
                new GoogleReview
                {
                    ReviewerName = "Sheila",
                    Rating = 5,
                    ReviewText = "Itala has been helping a friend and they only have good things to say. I would recommend Itala. If just for a chat as she is so caring.",
                    ReviewDate = DateTime.Now.AddDays(-21),
                    ProfilePhotoUrl = ""
                }
            };

            return (fallbackReviews, 5.0, fallbackReviews.Count);
        }
    }

    public class GooglePlacesResponse
    {
        [JsonPropertyName("result")]
        public GooglePlaceResult Result { get; set; } = new();

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }

    public class GooglePlaceResult
    {
        [JsonPropertyName("reviews")]
        public List<GooglePlaceReview> Reviews { get; set; } = new();

        [JsonPropertyName("rating")]
        public double Rating { get; set; }

        [JsonPropertyName("user_ratings_total")]
        public int UserRatingsTotal { get; set; }
    }

    public class GooglePlaceReview
    {
        [JsonPropertyName("author_name")]
        public string AuthorName { get; set; } = string.Empty;

        [JsonPropertyName("author_url")]
        public string AuthorUrl { get; set; } = string.Empty;

        [JsonPropertyName("profile_photo_url")]
        public string ProfilePhotoUrl { get; set; } = string.Empty;

        [JsonPropertyName("rating")]
        public int Rating { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("time")]
        public long Time { get; set; }
    }
}
