using Microsoft.AspNetCore.Mvc;
using BusinessLayer.Services;

namespace coachingWebapp.Controllers
{
    [Route("sitemap.xml")]
    public class SitemapController : ControllerBase
    {
        private readonly ISitemapService _sitemapService;

        public SitemapController(ISitemapService sitemapService)
        {
            _sitemapService = sitemapService;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                var sitemap = await _sitemapService.GenerateSitemapAsync();
                return Content(sitemap, "application/xml");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error generating sitemap: {ex.Message}");
            }
        }
    }
}
