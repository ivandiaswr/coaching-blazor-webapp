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
        private readonly ISessionService _sessionService;

        public StripeController(IPaymentService paymentService, ILogService logService, ISessionService sessionService)
        {
            _paymentService = paymentService;
            _logService = logService;
            _sessionService = sessionService;
        }

        [HttpPost("create-checkout-session")]
        public async Task<IActionResult> CreateCheckoutSession([FromBody] CheckoutSessionRequest request)
        {
            var requestJson = System.Text.Json.JsonSerializer.Serialize(request);
            await _logService.LogInfo("CreateCheckoutSession - Deserialized Request", requestJson);

            try
            {
                if (request == null || request.Session == null)
                {
                    await _logService.LogError("CreateCheckoutSession", "Invalid or missing request data.");
                    return BadRequest(new { error = "Invalid or missing request data." });
                }

                if (string.IsNullOrEmpty(request.Currency) || !IsStripeSupportedCurrency(request.Currency))
                {
                    await _logService.LogWarning("CreateCheckoutSession", $"Invalid or unsupported currency: {request.Currency}. Falling back to GBP.");
                    request.Currency = "GBP";
                }

                await _logService.LogInfo("CreateCheckoutSession",
                    $"Received request: BookingType={request.BookingType}, PlanId={request.PlanId}, SessionId={request.Session.Id}, Currency={request.Currency}, IdempotencyKey={request.IdempotencyKey}");

                var url = await _paymentService.CreateCheckoutSessionAsync(request);
                return Ok(new ModelLayer.Models.DTOs.StripeResponse { Url = url });
            }
            catch (Exception ex)
            {
                await _logService.LogError("CreateCheckoutSession Error", $"Failed to create checkout session for SessionId: {request?.Session?.Id}, Email: {request?.Session?.Email}, BookingType: {request?.BookingType}, Error: {ex.Message}, StackTrace: {ex.StackTrace}");

                // Try to clean up any pending sessions that might have been created
                if (request?.Session?.Email != null)
                {
                    try
                    {
                        await _sessionService.CleanupPendingSessionsForUserAsync(request.Session.Email);
                        await _logService.LogInfo("CreateCheckoutSession Cleanup", $"Cleaned up pending sessions after error for Email: {request.Session.Email}");
                    }
                    catch (Exception cleanupEx)
                    {
                        await _logService.LogError("CreateCheckoutSession Cleanup Error", $"Failed to cleanup after error for Email: {request.Session.Email}, Error: {cleanupEx.Message}");
                    }
                }

                return StatusCode(500, new { error = "Failed to create checkout session.", details = ex.Message });
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
                    await _logService.LogInfo("ConfirmPayment", $"Payment not confirmed for sessionId: {sessionId}");
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

                await _paymentService.HandleWebhookAsync(json, stripeSignature.ToString());
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

        private bool IsStripeSupportedCurrency(string currency)
        {
            var supportedCurrencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "AED", "AFN", "ALL", "AMD", "ANG", "AOA", "ARS", "AUD", "AWG", "AZN", "BAM", "BBD", "BDT", "BGN",
                "BHD", "BIF", "BMD", "BND", "BOB", "BRL", "BSD", "BTN", "BWP", "BYN", "BZD", "CAD", "CDF", "CHF",
                "CLP", "CNY", "COP", "CRC", "CUP", "CVE", "CZK", "DJF", "DKK", "DOP", "DZD", "EGP", "ERN", "ETB",
                "EUR", "FJD", "FKP", "FOK", "GBP", "GEL", "GGP", "GHS", "GIP", "GMD", "GNF", "GTQ", "GYD", "HKD",
                "HNL", "HRK", "HTG", "HUF", "IDR", "ILS", "IMP", "INR", "IQD", "IRR", "ISK", "JEP", "JMD", "JOD",
                "JPY", "KES", "KGS", "KHR", "KID", "KMF", "KRW", "KWD", "KYD", "KZT", "LAK", "LBP", "LKR", "LRD",
                "LSL", "LYD", "MAD", "MDL", "MGA", "MKD", "MMK", "MNT", "MOP", "MRU", "MUR", "MVR", "MWK", "MXN",
                "MYR", "MZN", "NAD", "NGN", "NIO", "NOK", "NPR", "NZD", "OMR", "PAB", "PEN", "PGK", "PHP", "PKR",
                "PLN", "PYG", "QAR", "RON", "RSD", "RUB", "RWF", "SAR", "SBD", "SCR", "SDG", "SEK", "SGD", "SHP",
                "SLE", "SOS", "SRD", "SSP", "STN", "SYP", "SZL", "THB", "TJS", "TMT", "TND", "TOP", "TRY", "TTD",
                "TVD", "TWD", "TZS", "UAH", "UGX", "USD", "UYU", "UZS", "VES", "VND", "VUV", "WST", "XAF", "XCD",
                "XOF", "XPF", "YER", "ZAR", "ZMW"
            };
            return supportedCurrencies.Contains(currency.ToUpper());
        }
    }
}