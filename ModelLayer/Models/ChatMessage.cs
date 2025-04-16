namespace ModelLayer.Models
{
    public class ChatMessage
    {
        public bool IsUser { get; set; }
        public string Text { get; set; }
        public List<ChatResource> Resources { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}