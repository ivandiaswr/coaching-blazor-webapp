public interface ISecurityService {
    string GenerateUnsubscribeToken(string email);
    bool ValidateUnsubscribeToken(string email, string token);
}