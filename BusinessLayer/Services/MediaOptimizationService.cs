using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace BusinessLayer.Services
{
    public class MediaOptimizationService
    {
        private readonly ILogger<MediaOptimizationService> _logger;
        private readonly IConfiguration _configuration;
        private readonly Dictionary<string, MediaInfo> _mediaCache;

        public MediaOptimizationService(ILogger<MediaOptimizationService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _mediaCache = new Dictionary<string, MediaInfo>();
        }

        public string GetOptimizedVideoPath(string originalPath, VideoQuality quality = VideoQuality.Medium)
        {
            try
            {
                var baseName = Path.GetFileNameWithoutExtension(originalPath);
                var directory = Path.GetDirectoryName(originalPath);

                return quality switch
                {
                    VideoQuality.Low => Path.Combine(directory, $"{baseName}_low.mp4"),
                    VideoQuality.Medium => Path.Combine(directory, $"{baseName}_medium.mp4"),
                    VideoQuality.High => originalPath, // Use original for high quality
                    _ => Path.Combine(directory, $"{baseName}_medium.mp4")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting optimized video path for {OriginalPath}", originalPath);
                return originalPath; // Fallback to original
            }
        }

        public string GetOptimizedImagePath(string originalPath, ImageSize size = ImageSize.Medium)
        {
            try
            {
                var baseName = Path.GetFileNameWithoutExtension(originalPath);
                var directory = Path.GetDirectoryName(originalPath);

                return size switch
                {
                    ImageSize.Thumbnail => Path.Combine(directory, $"{baseName}_thumb.webp"),
                    ImageSize.Medium => Path.Combine(directory, $"{baseName}_medium.webp"),
                    ImageSize.Large => Path.Combine(directory, $"{baseName}_large.webp"),
                    _ => Path.Combine(directory, $"{baseName}_medium.webp")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting optimized image path for {OriginalPath}", originalPath);
                return originalPath; // Fallback to original
            }
        }

        public string GetPosterImage(string videoPath)
        {
            try
            {
                var baseName = Path.GetFileNameWithoutExtension(videoPath);
                var directory = Path.GetDirectoryName(videoPath);
                return Path.Combine(directory, $"{baseName}_poster.jpg");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting poster image for {VideoPath}", videoPath);
                return string.Empty;
            }
        }

        public async Task<MediaInfo> GetMediaInfoAsync(string filePath)
        {
            if (_mediaCache.TryGetValue(filePath, out var cachedInfo))
            {
                return cachedInfo;
            }

            try
            {
                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                {
                    return new MediaInfo { FilePath = filePath, Size = 0, IsOptimized = false };
                }

                var mediaInfo = new MediaInfo
                {
                    FilePath = filePath,
                    Size = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTime,
                    IsOptimized = filePath.Contains("_low") || filePath.Contains("_medium") || filePath.Contains("_thumb") || filePath.EndsWith(".webp")
                };

                // Cache for 5 minutes to reduce file system calls
                _mediaCache[filePath] = mediaInfo;
                _ = Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(_ => _mediaCache.Remove(filePath));

                return mediaInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting media info for {FilePath}", filePath);
                return new MediaInfo { FilePath = filePath, Size = 0, IsOptimized = false };
            }
        }

        public bool ShouldLoadMedia(string userAgent, bool isLowBandwidth = false)
        {
            // Don't auto-load videos on mobile or low bandwidth
            if (isLowBandwidth || userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        public VideoQuality GetRecommendedVideoQuality(string userAgent, bool isLowBandwidth = false)
        {
            if (isLowBandwidth || userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase))
            {
                return VideoQuality.Low;
            }

            if (userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase))
            {
                return VideoQuality.Medium;
            }

            return VideoQuality.Medium; // Default to medium for better performance
        }
    }

    public class MediaInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public bool IsOptimized { get; set; }
    }

    public enum VideoQuality
    {
        Low,    // 360p, ~400kb/s
        Medium, // 720p, ~800kb/s  
        High    // 1080p, original
    }

    public enum ImageSize
    {
        Thumbnail, // 300px width
        Medium,    // 800px width
        Large      // 1200px width
    }
}
