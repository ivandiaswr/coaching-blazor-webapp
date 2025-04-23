using BusinessLayer.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using ModelLayer.Models;

namespace coachingWebapp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StripeController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public StripeController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [HttpPost("create-checkout-session")]
        public async Task<IActionResult> CreateCheckoutSession([FromBody] Session session)
        {
            var url = await _paymentService.CreateCheckoutSessionAsync(session);
            return Ok(new { url });
        }

        [HttpPost("confirm-payment")]
        public async Task<IActionResult> ConfirmPayment([FromQuery] string sessionId)
        {
            var success = await _paymentService.ConfirmPaymentAsync(sessionId);
            return success ? Ok() : BadRequest("Payment not confirmed");
        }
    }
}