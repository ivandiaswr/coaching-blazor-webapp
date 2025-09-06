using System.Globalization;
using System.Text;
using System.Xml;

namespace BusinessLayer.Services
{
    public interface ISitemapService
    {
        Task<string> GenerateSitemapAsync();
    }

    public class SitemapService : ISitemapService
    {
        private readonly List<SitemapUrl> _staticUrls = new()
        {
            new SitemapUrl { Url = "/", Priority = 1.0, ChangeFrequency = "weekly" },
            new SitemapUrl { Url = "/about/meet-itala", Priority = 0.9, ChangeFrequency = "monthly" },
            new SitemapUrl { Url = "/about/process", Priority = 0.8, ChangeFrequency = "monthly" },
            new SitemapUrl { Url = "/services/life-coaching", Priority = 0.9, ChangeFrequency = "monthly" },
            new SitemapUrl { Url = "/services/career-coaching", Priority = 0.9, ChangeFrequency = "monthly" },
            new SitemapUrl { Url = "/services/regulatory/consulting", Priority = 0.8, ChangeFrequency = "monthly" },
            new SitemapUrl { Url = "/services/regulatory/training", Priority = 0.8, ChangeFrequency = "monthly" },
            new SitemapUrl { Url = "/resources", Priority = 0.7, ChangeFrequency = "weekly" },
            new SitemapUrl { Url = "/case-studies", Priority = 0.8, ChangeFrequency = "monthly" },
            new SitemapUrl { Url = "/faqs", Priority = 0.6, ChangeFrequency = "monthly" },
            new SitemapUrl { Url = "/privacy-policy", Priority = 0.3, ChangeFrequency = "yearly" },
            new SitemapUrl { Url = "/terms-and-conditions", Priority = 0.3, ChangeFrequency = "yearly" }
        };

        public Task<string> GenerateSitemapAsync()
        {
            var sitemap = new StringBuilder();
            sitemap.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sitemap.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

            var baseUrl = "https://italaveloso.com";
            var lastModified = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

            foreach (var url in _staticUrls)
            {
                sitemap.AppendLine("  <url>");
                sitemap.AppendLine($"    <loc>{baseUrl}{url.Url}</loc>");
                sitemap.AppendLine($"    <lastmod>{lastModified}</lastmod>");
                sitemap.AppendLine($"    <changefreq>{url.ChangeFrequency}</changefreq>");
                sitemap.AppendLine($"    <priority>{url.Priority.ToString("0.0", CultureInfo.InvariantCulture)}</priority>");
                sitemap.AppendLine("  </url>");
            }

            sitemap.AppendLine("</urlset>");
            return Task.FromResult(sitemap.ToString());
        }
    }

    public class SitemapUrl
    {
        public string Url { get; set; } = "";
        public double Priority { get; set; }
        public string ChangeFrequency { get; set; } = "";
    }
}
