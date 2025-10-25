using Berberis.Portal.Api.Services;
using Berberis.Portal.Contracts.DTOs;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Berberis.Portal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigurationController : ControllerBase
{
    private readonly IPortalService _portalService;
    private readonly ILogger<ConfigurationController> _logger;

    public ConfigurationController(IPortalService portalService, ILogger<ConfigurationController> logger)
    {
        _portalService = portalService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<ConfigurationDto>> GetConfiguration()
    {
        try
        {
            var config = await _portalService.GetConfigurationAsync();
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving configuration");
            return Problem("Failed to retrieve configuration");
        }
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportConfiguration()
    {
        try
        {
            var config = await _portalService.GetConfigurationAsync();
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            return File(bytes, "application/json", "berberis-config.json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting configuration");
            return Problem("Failed to export configuration");
        }
    }
}
