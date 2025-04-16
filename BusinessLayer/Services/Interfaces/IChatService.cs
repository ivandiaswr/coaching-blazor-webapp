using ModelLayer.Models;

namespace BusinessLayer.Services.Interfaces;

public interface IChatService
{
    Task<ChatMessage> SendMessageAsync(List<ChatMessage> conversationHistory, string userMessage);
    List<ChatResource> GetResources(string message);
}