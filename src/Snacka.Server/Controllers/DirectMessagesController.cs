using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Snacka.Server.Data;
using Snacka.Server.DTOs;
using Snacka.Server.Hubs;
using Snacka.Server.Services;

namespace Snacka.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DirectMessagesController : ControllerBase
{
    private readonly SnackaDbContext _db;
    private readonly IDirectMessageService _directMessageService;
    private readonly IHubContext<SnackaHub> _hubContext;

    public DirectMessagesController(
        SnackaDbContext db,
        IDirectMessageService directMessageService,
        IHubContext<SnackaHub> hubContext)
    {
        _db = db;
        _directMessageService = directMessageService;
        _hubContext = hubContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ConversationSummary>>> GetConversations(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var conversations = await _directMessageService.GetConversationsAsync(userId.Value, cancellationToken);
        return Ok(conversations);
    }

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<IEnumerable<DirectMessageResponse>>> GetConversation(
        Guid userId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null) return Unauthorized();

        var messages = await _directMessageService.GetConversationAsync(
            currentUserId.Value, userId, skip, take, cancellationToken);
        return Ok(messages);
    }

    [HttpPost("{userId:guid}")]
    public async Task<ActionResult<DirectMessageResponse>> SendMessage(
        Guid userId,
        [FromBody] SendDirectMessageRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null) return Unauthorized();

        try
        {
            var message = await _directMessageService.SendMessageAsync(
                currentUserId.Value, userId, request.Content, cancellationToken);

            // Notify recipient via SignalR
            await _hubContext.Clients.User(userId.ToString())
                .SendAsync("ReceiveDirectMessage", message, cancellationToken);

            return Ok(message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DirectMessageResponse>> UpdateMessage(
        Guid id,
        [FromBody] DirectMessageUpdate request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var message = await _directMessageService.UpdateMessageAsync(
                id, userId.Value, request.Content, cancellationToken);

            // Notify both parties about the edit
            await _hubContext.Clients.User(message.RecipientId.ToString())
                .SendAsync("DirectMessageEdited", message, cancellationToken);

            return Ok(message);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteMessage(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        try
        {
            // SECURITY: Get message info BEFORE deletion to notify only the participants
            var message = await _db.DirectMessages
                .AsNoTracking()
                .Where(m => m.Id == id)
                .Select(m => new { m.SenderId, m.RecipientId })
                .FirstOrDefaultAsync(cancellationToken);

            if (message is null)
                return NotFound(new { error = "Message not found." });

            await _directMessageService.DeleteMessageAsync(id, userId.Value, cancellationToken);

            // Notify only the two participants (not all users)
            await _hubContext.Clients
                .Users(message.SenderId.ToString(), message.RecipientId.ToString())
                .SendAsync("DirectMessageDeleted", id, cancellationToken);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    [HttpPost("{userId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid userId, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null) return Unauthorized();

        await _directMessageService.MarkAsReadAsync(currentUserId.Value, userId, cancellationToken);
        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
