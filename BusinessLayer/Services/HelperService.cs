using BusinessLayer.Services.Interfaces;
using Microsoft.Extensions.Configuration;

public class HelperService : IHelperService
{
    private readonly IConfiguration _configuration;

    public HelperService(IConfiguration configuration)
    {
        this._configuration = configuration;
    }

    public string GetConfigValue(string key)
    {
        var value = _configuration[key];
        return !string.IsNullOrEmpty(value) ? value : Environment.GetEnvironmentVariable(key) ?? string.Empty;
    }
}