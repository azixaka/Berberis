using Berberis.Portal.Api.Services;
using Berberis.Portal.Contracts.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Berberis.Portal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OverviewController : ControllerBase
{
    private readonly IPortalService _portalService;
    private readonly ILogger<OverviewController> _logger;

    public OverviewController(IPortalService portalService, ILogger<OverviewController> logger)
    {
        _portalService = portalService;
        _logger = logger;
    }

    /// <summary>Gets system-wide overview statistics.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(SystemOverviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SystemOverviewDto>> GetSystemOverview()
    {
        try
        {
            var overview = await _portalService.GetSystemOverviewAsync();
            return Ok(overview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system overview");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
