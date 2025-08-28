public class GoogleReview
{
    public string ReviewerName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string ReviewText { get; set; } = string.Empty;
    public DateTime ReviewDate { get; set; }
    public string ProfilePhotoUrl { get; set; } = string.Empty;
    public string AuthorUrl { get; set; } = string.Empty;
}