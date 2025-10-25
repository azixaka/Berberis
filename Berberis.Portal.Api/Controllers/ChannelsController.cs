using Berberis.Portal.Api.Services;
using Berberis.Portal.Contracts.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Berberis.Portal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChannelsController : ControllerBase
{
    private readonly IPortalService _portalService;
    private readonly ILogger<ChannelsController> _logger;

    public ChannelsController(IPortalService portalService, ILogger<ChannelsController> logger)
    {
        _portalService = portalService;
        _logger = logger;
    }

    /// <summary>Gets all channels with optional filtering.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ChannelInfoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ChannelInfoDto>>> GetAllChannels([FromQuery] string? search = null)
    {
        try
        {
            var channels = await _portalService.GetAllChannelsAsync(search);
            return Ok(channels);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all channels");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>Gets detailed information about a specific channel.</summary>
    [HttpGet("{channelName}")]
    [ProducesResponseType(typeof(ChannelDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChannelDetailDto>> GetChannelDetails(string channelName)
    {
        try
        {
            var channel = await _portalService.GetChannelDetailsAsync(channelName);
            if (channel == null)
                return NotFound(new { error = $"Channel '{channelName}' not found" });

            return Ok(channel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting channel details for {ChannelName}", channelName);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>Gets subscriptions for a specific channel.</summary>
    [HttpGet("{channelName}/subscriptions")]
    [ProducesResponseType(typeof(List<SubscriptionInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<SubscriptionInfoDto>>> GetChannelSubscriptions(string channelName)
    {
        try
        {
            var channel = await _portalService.GetChannelDetailsAsync(channelName);
            if (channel == null)
                return NotFound(new { error = $"Channel '{channelName}' not found" });

            return Ok(channel.Subscriptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subscriptions for channel {ChannelName}", channelName);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>Gets state keys for a stateful channel.</summary>
    [HttpGet("{channelName}/state")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<string>>> GetChannelState(string channelName)
    {
        try
        {
            var stateKeys = await _portalService.GetChannelStateKeysAsync(channelName);
            return Ok(stateKeys);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting channel state for {ChannelName}", channelName);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>Resets a channel (clears message store).</summary>
    [HttpPost("{channelName}/reset")]
    [ProducesResponseType(typeof(Contracts.DTOs.OperationResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<Contracts.DTOs.OperationResultDto>> ResetChannel(string channelName)
    {
        try
        {
            _logger.LogInformation("Attempting to reset channel {ChannelName}", channelName);
            var success = await _portalService.ResetChannelAsync(channelName);

            if (success)
            {
                return Ok(new Contracts.DTOs.OperationResultDto
                {
                    Success = true,
                    Message = $"Channel '{channelName}' reset successfully"
                });
            }

            return Ok(new Contracts.DTOs.OperationResultDto
            {
                Success = false,
                Error = "Reset operation not yet implemented - requires CrossBar API enhancement"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting channel {ChannelName}", channelName);
            return StatusCode(500, new Contracts.DTOs.OperationResultDto
            {
                Success = false,
                Error = "Internal server error"
            });
        }
    }

    /// <summary>Deletes a specific state key from a stateful channel.</summary>
    [HttpDelete("{channelName}/state/{key}")]
    [ProducesResponseType(typeof(Contracts.DTOs.OperationResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<Contracts.DTOs.OperationResultDto>> DeleteChannelStateKey(string channelName, string key)
    {
        try
        {
            _logger.LogInformation("Attempting to delete state key {Key} from channel {ChannelName}", key, channelName);
            var success = await _portalService.DeleteChannelStateKeyAsync(channelName, key);

            if (success)
            {
                return Ok(new Contracts.DTOs.OperationResultDto
                {
                    Success = true,
                    Message = $"State key '{key}' deleted from channel '{channelName}'"
                });
            }

            return Ok(new Contracts.DTOs.OperationResultDto
            {
                Success = false,
                Error = "Delete state key operation not yet implemented - requires CrossBar API enhancement"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting state key {Key} from channel {ChannelName}", key, channelName);
            return StatusCode(500, new Contracts.DTOs.OperationResultDto
            {
                Success = false,
                Error = "Internal server error"
            });
        }
    }
}
