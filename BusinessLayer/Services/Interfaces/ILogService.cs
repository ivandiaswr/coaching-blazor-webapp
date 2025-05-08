namespace BusinessLayer.Services.Interfaces {
    public interface ILogService {
        Task LogInfo(string message, string exception);
        Task LogWarning(string message, string exception);
        Task LogError(string message, string exception);
    }
}