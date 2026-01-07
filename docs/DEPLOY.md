# Deployment Guide

This guide covers how to deploy Miscord server and client applications.

## Prerequisites

### All Platforms
- .NET 9 SDK or later
- Git

### Client Dependencies

#### macOS
```bash
# Install FFmpeg and SDL2 via Homebrew
brew install ffmpeg sdl2
```

#### Windows
- FFmpeg: Download from https://ffmpeg.org/download.html and add to PATH
- SDL2: Usually bundled or download from https://www.libsdl.org/

#### Linux (Ubuntu/Debian)
```bash
sudo apt update
sudo apt install ffmpeg libsdl2-dev
```

## Development Setup

### Quick Start

The easiest way to run Miscord for development is using the provided script:

```bash
./dev-start.sh
```

This will:
1. Build both server and client
2. Start the server on `http://localhost:5117`
3. Start two client instances with test accounts (Alice and Bob)

### Manual Development Setup

#### 1. Start the Server

```bash
cd src/Miscord.Server
dotnet run
```

The server starts on `http://localhost:5117` by default.

#### 2. Start a Client

```bash
cd src/Miscord.Client
dotnet run -- --server http://localhost:5117
```

Optional client arguments:
- `--server <url>` - Server URL (default: http://localhost:5117)
- `--email <email>` - Auto-login with this email
- `--password <password>` - Auto-login password
- `--title <title>` - Window title
- `--profile <name>` - Profile name for settings storage

## Production Deployment

### Server Deployment

#### 1. Configure Production Settings

Create `appsettings.Production.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "UseSqlite": false,
  "ConnectionStrings": {
    "DefaultConnection": "Server=your-db-server;Database=Miscord;User Id=miscord;Password=your-password;"
  },
  "Jwt": {
    "SecretKey": "YOUR-PRODUCTION-SECRET-KEY-AT-LEAST-32-CHARACTERS-LONG!",
    "Issuer": "Miscord",
    "Audience": "Miscord",
    "AccessTokenExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 7
  },
  "ServerInfo": {
    "Name": "Your Server Name",
    "Description": "Your server description",
    "AllowRegistration": true
  }
}
```

**Important Security Notes:**
- Change the `Jwt:SecretKey` to a strong, unique secret (minimum 32 characters)
- Use environment variables for sensitive values in production
- Set `AllowRegistration` to `false` if you don't want open registration

#### 2. Build for Production

```bash
dotnet publish src/Miscord.Server/Miscord.Server.csproj \
  -c Release \
  -o ./publish/server
```

#### 3. Database Options

**SQLite (Simple, Single Server)**
```json
{
  "UseSqlite": true
}
```
The database file `miscord.db` will be created in the working directory.

**SQL Server (Recommended for Production)**
```json
{
  "UseSqlite": false,
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=Miscord;User Id=miscord;Password=your-password;"
  }
}
```

#### 4. Run the Server

```bash
cd ./publish/server
dotnet Miscord.Server.dll
```

Or with environment-specific configuration:
```bash
ASPNETCORE_ENVIRONMENT=Production dotnet Miscord.Server.dll
```

#### 5. Reverse Proxy Setup (Recommended)

For production, run behind a reverse proxy like nginx:

```nginx
server {
    listen 443 ssl http2;
    server_name miscord.example.com;

    ssl_certificate /path/to/cert.pem;
    ssl_certificate_key /path/to/key.pem;

    location / {
        proxy_pass http://localhost:5117;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;

        # WebSocket support for SignalR
        proxy_read_timeout 86400;
    }
}
```

### Client Distribution

#### Build for All Platforms

**macOS:**
```bash
dotnet publish src/Miscord.Client/Miscord.Client.csproj \
  -c Release \
  -r osx-x64 \
  --self-contained true \
  -o ./publish/client-macos-x64

# For Apple Silicon
dotnet publish src/Miscord.Client/Miscord.Client.csproj \
  -c Release \
  -r osx-arm64 \
  --self-contained true \
  -o ./publish/client-macos-arm64
```

**Windows:**
```bash
dotnet publish src/Miscord.Client/Miscord.Client.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -o ./publish/client-windows-x64
```

**Linux:**
```bash
dotnet publish src/Miscord.Client/Miscord.Client.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -o ./publish/client-linux-x64
```

### Docker Deployment (Server)

Create a `Dockerfile` in the repository root:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5117

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Miscord.Server/Miscord.Server.csproj", "Miscord.Server/"]
COPY ["src/Miscord.Shared/Miscord.Shared.csproj", "Miscord.Shared/"]
COPY ["src/Miscord.WebRTC/Miscord.WebRTC.csproj", "Miscord.WebRTC/"]
RUN dotnet restore "Miscord.Server/Miscord.Server.csproj"
COPY src/ .
RUN dotnet publish "Miscord.Server/Miscord.Server.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Miscord.Server.dll"]
```

Build and run:
```bash
docker build -t miscord-server .
docker run -d -p 5117:5117 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e Jwt__SecretKey=your-secret-key \
  -v miscord-data:/app/data \
  miscord-server
```

## Environment Variables

The server supports configuration via environment variables:

| Variable | Description |
|----------|-------------|
| `ASPNETCORE_ENVIRONMENT` | Environment name (Development/Production) |
| `ASPNETCORE_URLS` | Server URLs (e.g., `http://0.0.0.0:5117`) |
| `Jwt__SecretKey` | JWT signing key |
| `ConnectionStrings__DefaultConnection` | Database connection string |
| `UseSqlite` | Use SQLite instead of SQL Server |

## Health Check

The server exposes a health check endpoint:

```bash
curl http://localhost:5117/api/health
```

## Troubleshooting

### Server won't start
- Check that port 5117 is not in use
- Verify database connection string
- Check logs for specific error messages

### Client can't connect
- Verify server URL is correct
- Check firewall settings
- Ensure WebSocket connections are allowed through any proxy

### Audio/Video issues
- Verify FFmpeg is installed and in PATH
- Check SDL2 installation
- On macOS, ensure microphone/camera permissions are granted

### Database errors
- For SQLite: ensure write permissions in the directory
- For SQL Server: verify connection string and credentials
- Run migrations if using a fresh database

## Ports and Firewall

| Port | Protocol | Purpose |
|------|----------|---------|
| 5117 | TCP | HTTP API and SignalR |

WebRTC media uses dynamic UDP ports. For production behind NAT, consider setting up a TURN server.
