public interface ILogService {
    Task LogInfo(string message);
    Task LogWarning(string message);
    Task LogError(string message, string exception);
}