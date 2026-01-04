# Miscord - Complete Feature Implementation Plan

## Overview
Implement a self-hosted Discord alternative with user accounts, messaging, voice channels, and media streaming. Built with ASP.NET Core 9 backend, Avalonia UI desktop client, and WebRTC for real-time communication.

## Current Status

### Completed (Foundation)
- ✅ Project structure with Shared/Server/Client/WebRTC libraries
- ✅ Avalonia UI 11.1.3 integrated for cross-platform desktop
- ✅ Database schema designed with 7 entities
- ✅ EF Core DbContext fully configured
- ✅ Build pipeline working (zero errors)
- ✅ Git repository on GitHub (private)

### Implementation Progress
- **Database Models**: 100% (7 entities: User, MiscordServer, Channel, Message, DirectMessage, UserServer, VoiceParticipant)
- **Authentication**: 0% (to implement)
- **Messaging**: 0% (to implement)
- **Voice & Media**: 0% (to implement)
- **UI**: 0% (to implement)

## Phase 1: Core Infrastructure & User Management

### 1.1 Database Setup [COMPLETED]
- ✅ Create EF Core models for users, channels, messages, and relationships
- ✅ Configure DbContext with proper relationships and cascading deletes
- ✅ Set up unique constraints on email/username
- ✅ Configure composite indexes for performance

**Deliverables:**
- 7 database entities with proper foreign keys
- Migration-ready DbContext
- No build errors

### 1.2 User Accounts [NOT STARTED]
**Estimated: 300-400 lines of code**

#### Endpoints to Implement:
- `POST /api/auth/register` - User registration with email/password validation
- `POST /api/auth/login` - User login with JWT token generation
- `POST /api/auth/refresh` - Refresh expired tokens
- `GET /api/users/me` - Get current user profile
- `PUT /api/users/me` - Update user profile
- `POST /api/users/me/avatar` - Upload user avatar

#### Implementation Details:
- Use BCrypt.Net-Next (4.0.3) for password hashing
- Implement JWT token generation with configurable expiration
- Add token refresh mechanism for security
- Validate email format and password strength
- Create authentication middleware for protected routes
- Handle user online status with a UserConnection tracking table

**Dependencies:**
- BCrypt.Net-Next 4.0.3 ✅ (already added)
- System.IdentityModel.Tokens.Jwt
- Microsoft.IdentityModel.Tokens

### 1.3 SignalR Hub Setup [NOT STARTED]
**Estimated: 200-300 lines of code**

#### Hub Methods to Implement:
- Connection handling (OnConnectedAsync, OnDisconnectedAsync)
- User online status broadcasting
- Connection authentication via JWT
- User presence tracking

#### Key Features:
- SignalR hub for real-time updates
- Connection state management
- User online status tracking
- Group-based messaging for scalability

**Dependencies:**
- Microsoft.AspNetCore.SignalR (included with ASP.NET Core 9)

---

## Phase 2: Messaging Features

### 2.1 Direct Messages [NOT STARTED]
**Estimated: 500-600 lines of code**

#### Endpoints to Implement:
- `GET /api/direct-messages/{userId}` - Get DM history with specific user
- `POST /api/direct-messages/{userId}` - Send direct message
- `PUT /api/direct-messages/{id}` - Edit message
- `DELETE /api/direct-messages/{id}` - Delete message
- `GET /api/direct-messages` - Get list of DM conversations

#### SignalR Events:
- `SendDirectMessage(recipientId, content)` - Send DM in real-time
- `ReceiveDirectMessage(senderId, content)` - Receive DM notification
- `UserTyping(recipientId)` - Typing indicator
- `UserStoppedTyping(recipientId)` - Stop typing indicator
- `MessageEdited(messageId, newContent)` - Message edit notification
- `MessageDeleted(messageId)` - Message delete notification

#### Implementation Details:
- Persist messages to database for history
- Real-time delivery via SignalR
- Typing indicators with timeout
- Edit/delete with timestamp tracking

### 2.2 Text Channels [NOT STARTED]
**Estimated: 700-800 lines of code**

#### Endpoints to Implement:
- `POST /api/servers/{serverId}/channels` - Create text channel
- `GET /api/servers/{serverId}/channels` - List channels
- `GET /api/channels/{channelId}` - Get channel details
- `PUT /api/channels/{channelId}` - Update channel
- `DELETE /api/channels/{channelId}` - Delete channel
- `GET /api/channels/{channelId}/messages` - Get message history (paginated)
- `POST /api/channels/{channelId}/messages` - Post message
- `PUT /api/messages/{id}` - Edit channel message
- `DELETE /api/messages/{id}` - Delete channel message
- `POST /api/channels/{channelId}/members` - Add member to channel
- `DELETE /api/channels/{channelId}/members/{userId}` - Remove member

#### SignalR Events:
- `SendChannelMessage(channelId, content)` - Send message
- `ReceiveChannelMessage(channelId, message)` - Receive message
- `ChannelCreated(channel)` - Channel creation notification
- `ChannelDeleted(channelId)` - Channel deletion notification
- `ChannelUpdated(channel)` - Channel update notification
- `MemberJoined(channelId, user)` - Member join notification
- `MemberLeft(channelId, userId)` - Member leave notification

#### Implementation Details:
- Channel permissions (basic: public/private)
- Message history with pagination
- Member management per channel
- Message threading support (optional)

---

## Phase 3: Voice & Media Communication

### 3.1 Voice Channels & WebRTC Signaling [NOT STARTED]
**Estimated: 1000-1200 lines of code**

#### Endpoints to Implement:
- `POST /api/servers/{serverId}/voice-channels` - Create voice channel
- `GET /api/servers/{serverId}/voice-channels` - List voice channels
- `DELETE /api/voice-channels/{channelId}` - Delete voice channel

#### SignalR Events (Critical for WebRTC):
- `JoinVoiceChannel(channelId)` - Join voice channel
- `LeaveVoiceChannel(channelId)` - Leave voice channel
- `SendOffer(targetUserId, offer)` - WebRTC SDP offer
- `SendAnswer(targetUserId, answer)` - WebRTC SDP answer
- `SendIceCandidate(targetUserId, candidate)` - ICE candidate
- `ParticipantJoined(channelId, user)` - Notify others of new participant
- `ParticipantLeft(channelId, userId)` - Notify others of leaving participant
- `ToggleMute(channelId, isMuted)` - Mute/unmute notification
- `ToggleDeafen(channelId, isDeafened)` - Deafen/undeafen notification
- `ToggleCamera(channelId, isCameraOn)` - Camera toggle notification
- `ToggleScreenShare(channelId, isSharing)` - Screen share toggle notification

#### Implementation Details:
- Use SipSorcery for WebRTC peer connection management
- Handle SDP offer/answer exchange via SignalR
- ICE candidate gathering and exchange
- STUN/TURN server configuration for NAT traversal
- Track active voice participants in database
- Support multiple concurrent voice channels
- Audio codec negotiation (opus preferred)

**Dependencies:**
- SipSorcery (WebRTC library for C#)
- System.Net.WebSockets for signaling

### 3.2 Webcam Streaming [NOT STARTED]
**Estimated: 600-800 lines of code**

#### Client-Side Implementation (Avalonia):
- Enumerate available cameras via OS APIs
- Capture video frames from selected camera
- Add video track to WebRTC peer connection
- Render local preview video stream
- Render remote participant video streams in grid layout

#### Server-Side Support:
- Track camera status per participant
- Coordinate video capability negotiation between peers
- Handle camera selection/switching

#### Implementation Details:
- Use OS-native camera APIs (macOS AVFoundation, Windows WinRT, Linux libcamera)
- H.264 or VP8 video codec
- Adaptive bitrate control based on network
- Audio track management (opus codec)
- Mute/unmute functionality
- Display participant names on video tiles
- Handle camera permission requests gracefully

**Dependencies:**
- SipSorcery for video track handling
- OS-specific camera APIs via interop

### 3.3 Screen Sharing [NOT STARTED]
**Estimated: 800-1000 lines of code**

#### Client-Side Implementation (Avalonia):
- Enumerate available displays
- Enumerate available capture devices (including Elgato 4K)
- Capture screen/display frames at high resolution
- Add screen share track to WebRTC connection
- Switch between camera and screen share
- Display screen share to remote participants

#### Device Support:
- macOS: ScreenCaptureKit (Sonoma 14+) or legacy APIs
- Windows: DXGI Desktop Duplication
- Linux: X11/Wayland screen capture
- Elgato 4K Capture: USB device enumeration, video4linux2 on Linux, AVFoundation on macOS

#### Implementation Details:
- Support multiple simultaneous displays
- Handle display hotplug/disconnect
- High-resolution capture (4K support)
- Configurable frame rate (30fps standard)
- Option to capture specific window
- Pause/stop screen share controls
- Audio from shared application (optional)
- Cursor capture and overlay

**Dependencies:**
- SipSorcery for screen share track
- Platform-specific screen capture libraries
- Device enumeration libraries

---

## Phase 4: UI Implementation

### 4.1 Authentication UI [NOT STARTED]
**Estimated: 300-400 lines of XAML/C#**

#### Windows/Views to Create:
1. **LoginWindow.axaml**
   - Email field
   - Password field
   - Login button
   - "Register" link
   - Error message display
   - Loading indicator

2. **RegisterWindow.axaml**
   - Username field (validation)
   - Email field (validation)
   - Password field (strength indicator)
   - Confirm password field
   - Register button
   - "Already have account" link
   - Terms of service checkbox

3. **Session Management**
   - Token storage in secure location
   - Auto-login on app restart
   - Session refresh handling
   - Logout functionality

#### MVVM Pattern:
- AuthViewModel for credential management
- Command bindings for Login/Register
- Reactive properties for form state
- Error handling and validation

**Dependencies:**
- Avalonia UI ✅
- ReactiveUI for MVVM ✅

### 4.2 Main Application Layout [NOT STARTED]
**Estimated: 800-1000 lines of XAML/C#**

#### Main Window Structure:
```
┌─────────────────────────────────────────┐
│     Miscord                         [_][□][X]  │
├─────────────────────────────────────────┤
│ Servers │ Channels │    Chat Area    │ Users │
│         │          │                 │       │
│ [Server1]│ # general│ Messages Here │ User1 │
│ [Server2]│ # random │ [input field] │ User2 │
│          │ 🔊 voice │                │ User3 │
│          │ 🔊 calls │                │       │
│          │          │                │       │
└─────────────────────────────────────────┘
```

#### Components to Create:
1. **ServerListPanel**
   - Display list of joined servers
   - Create/join server dialogs
   - Server icons/avatars
   - Notification badges

2. **ChannelListPanel**
   - List text and voice channels per server
   - Channel icons (# for text, 🔊 for voice)
   - Unread message indicators
   - Context menus for channel management

3. **ChatPanel**
   - Message list (virtualized for performance)
   - Message display with author, timestamp, avatar
   - Message actions (edit, delete, react)
   - Input field with rich text support
   - @mentions autocomplete
   - Emoji picker

4. **UserListPanel**
   - Display online users in current server
   - User status indicators (online, idle, offline)
   - User avatars
   - Right-click context menu (DM, profile view)

5. **Settings Panel**
   - User profile settings
   - Server settings (if owner)
   - Audio/video settings
   - Appearance/theme settings

#### MVVM Structure:
- MainViewModel (orchestrates all panels)
- ChannelViewModel (channel management and messages)
- ServerViewModel (server management)
- UserViewModel (user list and presence)
- Converters for message formatting, timestamps, etc.

### 4.3 Voice UI [NOT STARTED]
**Estimated: 600-800 lines of XAML/C#**

#### Voice Channel Window Components:
```
┌──────────────────────────────────┐
│ 🔊 Voice Channel Name            │
├──────────────────────────────────┤
│  ┌──────────────┐  ┌──────────┐ │
│  │   Local Video│  │User1 Video│ │
│  │ (Camera/Share)│  │(Camera)   │ │
│  └──────────────┘  └──────────┘ │
│  ┌──────────────┐  ┌──────────┐ │
│  │User2 Video   │  │User3 Video│ │
│  │(Camera)      │  │(Camera)   │ │
│  └──────────────┘  └──────────┘ │
├──────────────────────────────────┤
│ [🎤] [🔊] [📹] [🖥️] [⚙️] [X]     │
│ Mute Camera Share Screen Settings Exit  │
└──────────────────────────────────┘
```

#### Controls to Implement:
1. **Video Grid**
   - Dynamic grid layout (1x1, 2x2, 3x3, etc.)
   - Local video preview (self-view)
   - Remote participant videos
   - Participant names/labels
   - Status indicators (muted, deafened, sharing)
   - Click to focus/enlarge participant

2. **Control Bar**
   - Microphone toggle (mute/unmute)
   - Speaker toggle (deafen/undeafen)
   - Camera toggle (on/off)
   - Screen share button (camera vs screen)
   - Settings button
   - End call button
   - Volume controls per participant (hover)

3. **Participant List**
   - Connected users in voice channel
   - Status per user
   - Right-click context menu (mute, remove, profile)

4. **Settings Dialog**
   - Audio device selection (microphone, speaker)
   - Video device selection
   - Display selection for screen share
   - Audio/video quality settings
   - Echo cancellation, noise suppression settings

#### MVVM Structure:
- VoiceChannelViewModel (manage participants, media state)
- ParticipantViewModel (per-user data)
- WebRTC connection management service
- Media device enumeration service

---

## Phase 5: Advanced Features & Polish

### 5.1 Additional Backend Features [NOT STARTED]
**Estimated: 500-700 lines of code**

- User server invites system
- Role-based permissions (Admin, Moderator, Member)
- Message search across server
- Audit logging for server actions
- Rate limiting on API endpoints
- Batch loading optimization (DataLoader pattern)
- Caching strategy for frequently accessed data

### 5.2 Client Enhancements [NOT STARTED]
**Estimated: 400-600 lines of code**

- Auto-reconnection on network loss
- Offline message queuing
- Message persistence cache
- Performance optimization (virtualization, lazy loading)
- Error handling and user notifications
- App state persistence (open channels, window size)
- Keyboard shortcuts

### 5.3 Testing [NOT STARTED]
**Estimated: 500-800 lines of code**

- Unit tests for all services (target: >80% coverage)
- Integration tests for SignalR hub methods
- WebRTC connection tests
- UI component tests with ReactiveUI
- End-to-end testing (if time permits)

**Testing Framework:** MSTest ✅ (already configured)

---

## Technical Implementation Details

### Backend Stack
- **ASP.NET Core 9** - Web framework ✅
- **Entity Framework Core 9** - ORM with DbContext ✅
- **SignalR** - Real-time communication
- **SipSorcery** - WebRTC implementation
- **BCrypt.Net-Next 4.0.3** - Password hashing ✅
- **JWT (System.IdentityModel.Tokens.Jwt)** - Authentication
- **Swagger/OpenAPI** - API documentation ✅

### Client Stack
- **Avalonia UI 11.1.3** - Cross-platform UI ✅
- **ReactiveUI** - MVVM pattern ✅
- **WebRTC Client** - Peer connection management
- **HTTP Client** - REST API communication
- **SignalR Client** - Real-time updates

### Database Schema (7 Tables)

```
Users
├─ Id (PK)
├─ Username (Unique)
├─ Email (Unique)
├─ PasswordHash
├─ Avatar
├─ Status
├─ IsOnline
├─ CreatedAt
└─ UpdatedAt

MiscordServers
├─ Id (PK)
├─ Name
├─ Description
├─ OwnerId (FK → Users)
├─ Icon
├─ CreatedAt
└─ UpdatedAt

Channels
├─ Id (PK)
├─ Name
├─ Topic
├─ ServerId (FK → MiscordServers)
├─ Type (Text/Voice)
├─ Position
├─ CreatedAt
└─ UpdatedAt

Messages
├─ Id (PK)
├─ Content
├─ AuthorId (FK → Users)
├─ ChannelId (FK → Channels)
├─ CreatedAt
└─ UpdatedAt

DirectMessages
├─ Id (PK)
├─ Content
├─ SenderId (FK → Users)
├─ RecipientId (FK → Users)
├─ CreatedAt
└─ IsRead

UserServers (Junction)
├─ Id (PK)
├─ UserId (FK → Users)
├─ ServerId (FK → MiscordServers)
├─ Role (Owner/Admin/Moderator/Member)
└─ JoinedAt

VoiceParticipants
├─ Id (PK)
├─ UserId (FK → Users)
├─ ChannelId (FK → Channels)
├─ IsMuted
├─ IsDeafened
├─ IsScreenSharing
├─ IsCameraOn
└─ JoinedAt
```

---

## Implementation Order & Dependencies

1. **Phase 1.2** - User Authentication (prerequisite for everything)
   - Register/Login endpoints
   - JWT token generation
   - Password hashing

2. **Phase 1.3** - SignalR Hub Setup (prerequisite for messaging)
   - Hub configuration
   - Connection management
   - Online status tracking

3. **Phase 2.1** - Direct Messages (simplest messaging feature)
   - DM endpoints
   - SignalR DM events
   - DM history

4. **Phase 2.2** - Text Channels (builds on DM infrastructure)
   - Channel management
   - Channel messages
   - Member management

5. **Phase 4.1** - Authentication UI (needed before app is usable)
   - Login window
   - Register window
   - Session management

6. **Phase 4.2** - Main App Layout (basic chat UI)
   - Server list
   - Channel list
   - Chat display and input

7. **Phase 3.1** - Voice Channels & WebRTC (complex but isolated)
   - WebRTC signaling
   - SDP/ICE candidate handling
   - Participant tracking

8. **Phase 4.3** - Voice UI (depends on Phase 3.1)
   - Video grid
   - Control buttons
   - Participant management

9. **Phase 3.2** - Webcam Streaming (depends on Phase 3.1)
   - Camera enumeration
   - Video track integration
   - Local/remote video rendering

10. **Phase 3.3** - Screen Sharing (depends on Phase 3.1 & 3.2)
    - Screen capture
    - Device enumeration (Elgato support)
    - Screen share controls

11. **Phase 5** - Testing & Polish (throughout, but formalized here)
    - Unit tests for all services
    - Integration tests
    - Performance optimization

---

## Estimated Timeline

| Phase | Component | LOC | Effort | Notes |
|-------|-----------|-----|--------|-------|
| 1.1 | Database | 400 | ✅ Complete | All models and migrations |
| 1.2 | Auth Backend | 400 | 2-3 days | Register, Login, JWT |
| 1.3 | SignalR Hub | 300 | 1-2 days | Connection management |
| 2.1 | Direct Messages | 600 | 3-4 days | Endpoints + SignalR |
| 2.2 | Text Channels | 800 | 4-5 days | More complex than DM |
| 4.1 | Auth UI | 400 | 2-3 days | Login/Register windows |
| 4.2 | Main Layout | 1000 | 5-7 days | Most complex UI |
| 3.1 | Voice/WebRTC | 1200 | 5-7 days | Signaling is complex |
| 4.3 | Voice UI | 700 | 3-4 days | Depends on Phase 3.1 |
| 3.2 | Webcam | 700 | 4-5 days | Camera APIs per OS |
| 3.3 | Screen Share | 1000 | 5-7 days | Elgato + device enum |
| 5 | Testing/Polish | 800 | 4-5 days | Throughout project |
| **Total** | | **8000** | **8-10 weeks** | Single developer estimate |

---

## Success Criteria

- ✅ All 6 core features implemented and working
- ✅ Real-time messaging delivery via SignalR
- ✅ WebRTC voice calls establishing successfully
- ✅ Webcam video streams displaying in UI
- ✅ Screen sharing functional with device detection
- ✅ Cross-platform client (Windows, macOS, Linux)
- ✅ Self-hosted server deployment ready
- ✅ All tests passing with >80% coverage
- ✅ No build errors or warnings
- ✅ Database migrations included for fresh deployments

---

## Key Architecture Decisions

### Why Avalonia over MAUI?
- MAUI has poor macOS/Linux support
- Avalonia proven in production (JetBrains Rider)
- XAML-based, familiar to WPF developers
- Cross-platform performance excellent

### Why SipSorcery for WebRTC?
- Pure C# implementation
- Comprehensive WebRTC support
- Active maintenance and community
- No native dependencies required

### Why SignalR for Real-Time?
- Built into ASP.NET Core
- Automatic fallback to polling if WebSocket fails
- Integrated with Dependency Injection
- Excellent documentation and examples

### Database: SQL Server vs PostgreSQL
- Both fully supported via EF Core
- Migrations work identically
- Choose based on deployment preference
- SQL Server recommended for Windows hosts
- PostgreSQL recommended for Linux/cloud hosts

---

## Notes for Future Developers

### Before Starting Each Phase:
1. Read the phase description fully
2. Understand all dependencies
3. Plan the API contract before coding
4. Write tests as you code, not after
5. Commit frequently (after each logical unit)

### Critical Gotchas:
1. **WebRTC is complex** - Plan extra time for Phase 3.1
2. **Screen capture is OS-specific** - Need platform-specific implementations
3. **Cross-platform testing is essential** - Test on Windows, macOS, Linux
4. **SignalR groups are powerful** - Use them for channel message broadcasting
5. **JWT expiration handling** - Implement token refresh properly
6. **Elgato enumeration** - Requires USB device enumeration library
7. **Performance matters for video** - Virtualization and lazy loading are critical

### Recommended Libraries to Add Later:
- `AutoMapper` for entity/DTO mapping
- `FluentValidation` for complex validation
- `Serilog` for structured logging
- `Polly` for resilience and retry policies
- `MediatR` if CQRS pattern desired (optional)

---

## Repository Structure

```
miscord-csharp/
├── src/
│   ├── Miscord.Server/
│   │   ├── Controllers/          (REST endpoints)
│   │   ├── Hubs/                 (SignalR hubs)
│   │   ├── Services/             (business logic)
│   │   ├── Data/                 (DbContext ✅)
│   │   ├── DTOs/                 (data transfer objects)
│   │   ├── Middleware/           (auth, error handling)
│   │   ├── Migrations/           (EF Core migrations)
│   │   └── Program.cs
│   ├── Miscord.Client/
│   │   ├── Views/                (XAML windows ✅)
│   │   ├── ViewModels/           (MVVM VMs)
│   │   ├── Services/             (HTTP, SignalR clients)
│   │   ├── Models/               (UI models)
│   │   ├── Converters/           (value converters)
│   │   ├── App.axaml ✅
│   │   └── Program.cs ✅
│   ├── Miscord.Shared/
│   │   └── Models/               (entities ✅)
│   └── Miscord.WebRTC/
│       ├── Handlers/             (WebRTC peer handling)
│       └── Services/             (media capture, encoding)
├── tests/
│   ├── Miscord.Server.Tests/
│   └── Miscord.WebRTC.Tests/
├── .github/
│   └── workflows/                (CI/CD if desired)
├── PLAN.md ✅
├── AGENTS.md ✅
├── README.md ✅
└── Miscord.sln ✅
```

---

## Additional Resources

### Learning Materials:
- SignalR Documentation: https://learn.microsoft.com/en-us/aspnet/core/signalr/
- WebRTC Overview: https://webrtc.org/
- SipSorcery GitHub: https://github.com/sipsorcery-org/sipsorcery
- Avalonia Documentation: https://docs.avaloniaui.net/
- Entity Framework Core: https://learn.microsoft.com/en-us/ef/core/

### Reference Projects:
- Discord Clone implementations on GitHub
- SipSorcery examples directory
- Avalonia sample applications
- SignalR chat application sample

---

## Next Steps

1. **Implement Phase 1.2** (User Authentication) - This unblocks everything else
2. **Create folder structure** for Controllers, Services, DTOs, etc.
3. **Define API DTOs** for Register/Login/User profile
4. **Write integration tests** as features are implemented
5. **Deploy to test environment** frequently
6. **Gather feedback** from early users
7. **Optimize based on usage patterns**

---

**Last Updated:** 2026-01-04  
**Status:** Foundation Complete, Ready for Core Development  
**Maintainer:** Development Team
