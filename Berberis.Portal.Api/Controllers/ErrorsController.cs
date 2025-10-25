using Berberis.Portal.Api.Services;
using Berberis.Portal.Contracts.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Berberis.Portal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ErrorsController : ControllerBase
{
    private readonly ErrorTrackingService _errorTracking;
    private readonly ILogger<ErrorsController> _logger;

    public ErrorsController(ErrorTrackingService errorTracking, ILogger<ErrorsController> logger)
    {
        _errorTracking = errorTracking;
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<ErrorLogDto> GetErrors(
        [FromQuery] string? errorType = null,
        [FromQuery] string? search = null,
        [FromQuery] int limit = 100)
    {
        try
        {
            var errors = _errorTracking.GetErrors(errorType, search, limit);
            return Ok(errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving error logs");
            return Problem("Failed to retrieve error logs");
        }
    }

    [HttpDelete]
    public ActionResult ClearErrors()
    {
        try
        {
            _errorTracking.ClearErrors();
            return Ok(new { success = true, message = "All errors cleared" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing error logs");
            return Problem("Failed to clear error logs");
        }
    }

    [HttpPost("test")]
    public ActionResult AddTestErrors()
    {
        _errorTracking.TrackHandlerTimeout("sub-123", "orders.created", TimeSpan.FromSeconds(5), "OrderHandler");
        _errorTracking.TrackPublishFailure("orders.deleted", "Channel not found");
        _errorTracking.TrackTypeMismatch("trades.executed", "TradeMessage", "string");
        _errorTracking.TrackInvalidOperation("DetachSubscription", "Subscription not found", subscriptionId: "sub-456");

        return Ok(new { success = true, message = "Test errors added" });
    }
}
