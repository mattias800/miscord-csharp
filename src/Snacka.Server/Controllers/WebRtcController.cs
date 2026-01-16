using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Snacka.Server.Services;

namespace Snacka.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WebRtcController(ITurnService turnService) : ControllerBase
{
    /// <summary>
    /// Gets ICE server configurations for WebRTC connections.
    /// Includes STUN servers and optionally TURN servers with time-limited credentials.
    /// </summary>
    [HttpGet("ice-servers")]
    public ActionResult<IceServersResponse> GetIceServers()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var response = turnService.GetIceServers(userId);
        return Ok(response);
    }
}
