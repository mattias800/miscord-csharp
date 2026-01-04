# Agent Guidelines for Miscord

This document provides guidelines for AI agents working on the Miscord project.

## Project Overview

Miscord is a self-hosted Discord alternative built with C# and .NET. The project consists of:
- **Miscord.Server**: ASP.NET Core server application
- **Miscord.Client**: Desktop client application (to be implemented)
- **Miscord.Shared**: Shared models and interfaces
- **Miscord.WebRTC**: WebRTC/media handling library

## Core Requirements

### Features to Implement
1. User Accounts - Secure user authentication and account management
2. Direct Messages - Private messaging between users
3. Text Channels - Organized text-based communication
4. Voice Channels - Real-time voice communication
5. Webcam Streaming - Share camera in voice channels or private calls
6. Screen Sharing - Share screen with support for capture devices (e.g., Elgato 4K)

### Technology Stack
- **Language**: C# (.NET 8+)
- **Server**: ASP.NET Core
- **Real-time Communication**: WebRTC with SipSorcery
- **Signaling**: SignalR for WebSocket-based signaling
- **Database**: Entity Framework Core (SQL Server or PostgreSQL)
- **Testing**: MSTest

## Code Conventions

### C# Style
- Use arrow function syntax for all methods where possible
- Always use explicit type annotations (never use `dynamic` or untyped variables)
- Prefer interfaces over classes for abstractions
- All functions should have explicit return types
- One-liner methods should be expression-bodied

Example:
```csharp
public interface IUserService
{
    Task<User> GetUserByIdAsync(Guid userId);
}

public class UserService : IUserService
{
    private readonly IUserRepository _repository;
    
    public UserService(IUserRepository repository) => _repository = repository;
    
    public Task<User> GetUserByIdAsync(Guid userId) => _repository.FindByIdAsync(userId);
}
```

### Testing
- All features must have unit tests
- Test coverage should be verified automatically
- Integration tests for critical workflows
- No manual testing steps - everything must be automated

## Common Pitfalls to Avoid

1. **Never use `dynamic` type** - Always use proper type annotations
2. **Don't skip tests** - Write tests as you implement features
3. **Don't commit without testing** - Run `dotnet test` before committing
4. **Don't break the build** - Run `dotnet build` to verify compilation

## WebRTC Implementation Notes

- Use SipSorcery library for WebRTC implementation
- SignalR handles signaling between peers
- Support both peer-to-peer and server-mediated connections
- Handle ICE candidate exchange properly
- Support STUN/TURN servers for NAT traversal

## Database Conventions

- Use Entity Framework Core migrations
- Support both SQL Server and PostgreSQL
- Use proper foreign key relationships
- Index frequently queried columns
- Use soft deletes where appropriate

## Security

- Store passwords using bcrypt or similar
- Use JWT tokens for authentication
- Validate all user input
- Use HTTPS for all communications
- Sanitize output to prevent XSS

## Project Commands

```bash
# Build the solution
dotnet build

# Run tests
dotnet test

# Run the server (development)
dotnet run --project src/Miscord.Server

# Create a migration
dotnet ef migrations add MigrationName --project src/Miscord.Server
```

## When You Make Mistakes

If a user corrects you on something, update this file with the correction so future agents don't make the same mistake.
