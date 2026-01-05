using Microsoft.AspNetCore.Mvc;

namespace Miscord.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public HealthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    public ActionResult<ServerInfoResponse> GetHealth()
    {
        return Ok(new ServerInfoResponse(
            Name: _configuration["ServerInfo:Name"] ?? "Miscord Server",
            Description: _configuration["ServerInfo:Description"],
            Version: "1.0.0",
            AllowRegistration: _configuration.GetValue("ServerInfo:AllowRegistration", true)
        ));
    }
}

public record ServerInfoResponse(
    string Name,
    string? Description,
    string Version,
    bool AllowRegistration
);
