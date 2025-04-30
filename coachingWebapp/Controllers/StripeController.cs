using BusinessLayer.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using ModelLayer.Models;
using Stripe;

namespace coachingWebapp.Controllers
{
    [ApiController]
    [Route("api/payments")]
    public class StripeController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly ILogService _logService;

        public StripeController(IPaymentService paymentService, ILogService logService)
        {
            _paymentService = paymentService;
            _logService = logService;
        }

        [HttpPost("create-checkout-session")]
        public async Task<IActionResult> CreateCheckoutSession([FromBody] Session session)
        {
            try
            {
                if (session == null)
                {
                    await _logService.LogError("CreateCheckoutSession", "Invalid session data provided.");
                    return BadRequest(new { error = "Invalid session data provided." });
                }

                var url = await _paymentService.CreateCheckoutSessionAsync(session);
                
                return Ok(new { url });
            }
            catch (StripeException ex)
            {
                await _logService.LogError("CreateCheckoutSession Stripe Error", $"Error: {ex.Message}, StripeError: {ex.StripeError?.Message}");
                return BadRequest(new { error = $"Stripe error: {ex.Message}", details = ex.StripeError?.Message });
            }
            catch (Exception ex)
            {
                await _logService.LogError("CreateCheckoutSession Error", ex.Message);
                return BadRequest(new { error = "Failed to create checkout session.", details = ex.Message });
            }
        }

        [HttpPost("confirm-payment")]
        public async Task<IActionResult> ConfirmPayment([FromQuery] string sessionId)
        {
            try
            {
                var success = await _paymentService.ConfirmPaymentAsync(sessionId);
                if (success)
                {
                    return Ok(new { message = "Payment confirmed successfully." });
                }
                else
                {
                    await _logService.LogInfo($"ConfirmPayment -> Payment not confirmed for sessionId: {sessionId}");
                    return BadRequest(new { error = "Payment not confirmed." });
                }
            }
            catch (Exception ex)
            {
                await _logService.LogError("ConfirmPayment Error", ex.Message);
                return StatusCode(500, new { error = "An error occurred while confirming payment." });
            }
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook()
        {
            try
            {
                var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
                var stripeSignature = Request.Headers["Stripe-Signature"];

                if (string.IsNullOrEmpty(stripeSignature))
                {
                    await _logService.LogError("Webhook Error", "Missing Stripe-Signature header.");
                    return BadRequest(new { error = "Invalid request: Missing Stripe-Signature header." });
                }

                await _paymentService.HandleWebhookAsync(json, stripeSignature);
                return Ok();
            }
            catch (StripeException ex)
            {
                await _logService.LogError("Webhook Stripe Error", $"Error: {ex.Message}, StripeError: {ex.StripeError?.Message}");
                return BadRequest(new { error = "Invalid Stripe webhook signature or data." });
            }
            catch (Exception ex)
            {
                await _logService.LogError("Webhook Error", ex.Message);
                return StatusCode(500, new { error = "An error occurred while processing the webhook." });
            }
        }
    }
}