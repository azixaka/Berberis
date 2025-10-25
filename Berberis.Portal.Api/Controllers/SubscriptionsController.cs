using Berberis.Portal.Api.Services;
using Berberis.Portal.Contracts.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Berberis.Portal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubscriptionsController : ControllerBase
{
    private readonly IPortalService _portalService;
    private readonly ILogger<SubscriptionsController> _logger;

    public SubscriptionsController(IPortalService portalService, ILogger<SubscriptionsController> logger)
    {
        _portalService = portalService;
        _logger = logger;
    }

    /// <summary>Gets all subscriptions with optional filtering.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<SubscriptionInfoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SubscriptionInfoDto>>> GetAllSubscriptions(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null)
    {
        try
        {
            var subscriptions = await _portalService.GetAllSubscriptionsAsync(search, status);
            return Ok(subscriptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all subscriptions");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>Gets detailed information about a specific subscription.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(SubscriptionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubscriptionDetailDto>> GetSubscriptionDetails(string id)
    {
        try
        {
            var subscription = await _portalService.GetSubscriptionDetailsAsync(id);
            if (subscription == null)
                return NotFound(new { error = $"Subscription '{id}' not found" });

            return Ok(subscription);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subscription details for {SubscriptionId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>Suspends processing for a subscription.</summary>
    [HttpPost("{id}/suspend")]
    [ProducesResponseType(typeof(Contracts.DTOs.OperationResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<Contracts.DTOs.OperationResultDto>> SuspendSubscription(string id)
    {
        try
        {
            _logger.LogInformation("Attempting to suspend subscription {SubscriptionId}", id);
            var success = await _portalService.SuspendSubscriptionAsync(id);

            if (success)
            {
                return Ok(new Contracts.DTOs.OperationResultDto
                {
                    Success = true,
                    Message = $"Subscription '{id}' suspended successfully"
                });
            }

            return Ok(new Contracts.DTOs.OperationResultDto
            {
                Success = false,
                Error = "Suspend operation not yet implemented - requires CrossBar API enhancement"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suspending subscription {SubscriptionId}", id);
            return StatusCode(500, new Contracts.DTOs.OperationResultDto
            {
                Success = false,
                Error = "Internal server error"
            });
        }
    }

    /// <summary>Resumes processing for a subscription.</summary>
    [HttpPost("{id}/resume")]
    [ProducesResponseType(typeof(Contracts.DTOs.OperationResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<Contracts.DTOs.OperationResultDto>> ResumeSubscription(string id)
    {
        try
        {
            _logger.LogInformation("Attempting to resume subscription {SubscriptionId}", id);
            var success = await _portalService.ResumeSubscriptionAsync(id);

            if (success)
            {
                return Ok(new Contracts.DTOs.OperationResultDto
                {
                    Success = true,
                    Message = $"Subscription '{id}' resumed successfully"
                });
            }

            return Ok(new Contracts.DTOs.OperationResultDto
            {
                Success = false,
                Error = "Resume operation not yet implemented - requires CrossBar API enhancement"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming subscription {SubscriptionId}", id);
            return StatusCode(500, new Contracts.DTOs.OperationResultDto
            {
                Success = false,
                Error = "Internal server error"
            });
        }
    }

    /// <summary>Detaches (removes) a subscription.</summary>
    [HttpPost("{id}/detach")]
    [ProducesResponseType(typeof(Contracts.DTOs.OperationResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<Contracts.DTOs.OperationResultDto>> DetachSubscription(string id)
    {
        try
        {
            _logger.LogInformation("Attempting to detach subscription {SubscriptionId}", id);
            var success = await _portalService.DetachSubscriptionAsync(id);

            if (success)
            {
                return Ok(new Contracts.DTOs.OperationResultDto
                {
                    Success = true,
                    Message = $"Subscription '{id}' detached successfully"
                });
            }

            return Ok(new Contracts.DTOs.OperationResultDto
            {
                Success = false,
                Error = "Detach operation not yet implemented - requires CrossBar API enhancement"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detaching subscription {SubscriptionId}", id);
            return StatusCode(500, new Contracts.DTOs.OperationResultDto
            {
                Success = false,
                Error = "Internal server error"
            });
        }
    }
}
