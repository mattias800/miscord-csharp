# Gaming Stations

## Overview

Gaming Stations is a feature that allows users to register a powerful gaming PC as a dedicated streaming server, accessible from any device. This enables scenarios like:

- Playing games on your gaming PC from a laptop in another room
- Giving friends access to play games on your PC remotely
- Running a shared gaming rig that multiple people can use

The key insight is separating **where you are** (your laptop, phone, weak PC) from **where the game runs** (the powerful gaming PC).

## Core Concepts

### Gaming Station
A Gaming Station is a registered device (typically a gaming PC) that:
- Runs Snacka in a minimal/headless "Station Mode"
- Stays online and available when the PC is running
- Can stream its screen/games to connected users
- Accepts input (keyboard, mouse, controller) from connected users
- Is owned by a user who controls access permissions

### Station Owner
The user who registered the Gaming Station. They can:
- Connect to their station from any device
- Grant/revoke access to other users
- Configure station settings (auto-start, quality, permissions)
- See who is currently connected

### Station Guest
A user who has been granted access to someone else's Gaming Station. They can:
- Connect to the station (if permitted)
- View the stream
- Send input (if permitted)
- Participate in multiplayer sessions

## User Experience

### Setting Up a Gaming Station

#### On the Gaming PC:

1. Install and open Snacka
2. Log in with your account
3. Go to Settings → Gaming Station
4. Click "Register this device as a Gaming Station"
5. Enter a name (e.g., "My Gaming Rig")
6. Configure options:
   - **Auto-start**: Launch Snacka on Windows startup
   - **Station Mode**: Run minimized/in system tray
   - **Wake-on-LAN**: Allow remote wake (requires network setup)
   - **Default quality**: 1080p60, 4K30, etc.
7. Click "Register"

The gaming PC now shows up in your Gaming Stations list on all your devices.

#### Station Mode UI

When running in Station Mode, the gaming PC shows:
- System tray icon with status indicator
- Right-click menu: "Open Full UI", "Settings", "Exit"
- Notification when someone connects
- Minimal resource usage when idle

### Connecting to Your Gaming Station

#### From your laptop/other device:

1. Open Snacka
2. In the sidebar, see "Gaming Stations" section
3. Your stations are listed with status indicators:
   - 🟢 Online (ready to connect)
   - 🟡 In Use (you or someone else is connected)
   - 🔴 Offline
4. Click on a station to connect
5. The station's screen appears in your window
6. You now have full control (keyboard, mouse, controller)

#### Connection View

When connected to a Gaming Station:
```
┌─────────────────────────────────────────────────────┐
│ 🎮 My Gaming Rig                    [Quality ▼] [⛶] │
├─────────────────────────────────────────────────────┤
│                                                     │
│                                                     │
│              [Game/Desktop Stream]                  │
│                                                     │
│                                                     │
├─────────────────────────────────────────────────────┤
│ 🎤 Voice Chat    │  👥 Connected: You, @friend      │
│ [Mute] [Deafen]  │  [Invite] [Settings] [Disconnect]│
└─────────────────────────────────────────────────────┘
```

### Sharing Your Gaming Station

#### Granting Access:

1. Go to Gaming Station settings (or right-click station in sidebar)
2. Click "Manage Access"
3. Search for users to add
4. Set their permission level:
   - **View Only**: Can watch but not control
   - **Controller**: Can send controller input only
   - **Full Control**: Keyboard, mouse, and controller
   - **Admin**: Can also manage other users' access
5. Click "Grant Access"

The user now sees your station in their Gaming Stations list (under "Shared with me").

#### Permission Levels

| Permission | View Stream | Controller | Keyboard/Mouse | Manage Access |
|------------|-------------|------------|----------------|---------------|
| View Only  | ✓           |            |                |               |
| Controller | ✓           | ✓          |                |               |
| Full Control| ✓          | ✓          | ✓              |               |
| Admin      | ✓           | ✓          | ✓              | ✓             |

### Multiplayer Sessions

When multiple people connect to a Gaming Station:

1. Owner starts a game that supports local multiplayer
2. Opens the station to friends (or they're already granted access)
3. Friends connect to the station
4. Each person is assigned a player slot (Player 1, 2, 3, 4)
5. Controller input from each person goes to their assigned slot
6. Everyone sees the same stream
7. Voice chat works naturally (each person's mic → their device)

#### Player Assignment UI

```
┌─ Connected Players ─────────────────┐
│ Player 1: You (Owner)      [🎮 Xbox]│
│ Player 2: @friend1         [🎮 PS5] │
│ Player 3: @friend2         [⌨️ KB]  │
│ Player 4: (empty)          [Invite] │
└─────────────────────────────────────┘
```

### Voice Chat Integration

Gaming Stations integrate with voice channels:

**Option A: Standalone**
- Connect directly to station, voice chat is between connected users only

**Option B: Voice Channel Bridge**
- Station "joins" a voice channel in a community
- Anyone in that voice channel can see/hear the game
- Only users with station access can control

This allows scenarios like:
- Stream your gameplay to friends in a voice channel
- Let specific friends take control and play
- Others just watch and chat

## Technical Architecture

### New Server Entities

```
GamingStation
├── Id: Guid
├── OwnerId: Guid (User)
├── Name: string
├── MachineId: string (unique hardware identifier)
├── Status: Online | Offline | InUse
├── CreatedAt: DateTime
├── LastSeenAt: DateTime
├── Settings: StationSettings
└── AccessGrants: List<StationAccessGrant>

StationAccessGrant
├── Id: Guid
├── StationId: Guid
├── UserId: Guid
├── PermissionLevel: ViewOnly | Controller | FullControl | Admin
├── GrantedBy: Guid
├── GrantedAt: DateTime
└── ExpiresAt: DateTime? (optional time-limited access)

StationSession
├── Id: Guid
├── StationId: Guid
├── StartedAt: DateTime
├── ConnectedUsers: List<ConnectedUser>
└── Status: Active | Ended

ConnectedUser
├── UserId: Guid
├── PlayerSlot: int?
├── InputMode: Controller | Keyboard | ViewOnly
├── ConnectedAt: DateTime
└── LastInputAt: DateTime
```

### New API Endpoints

```
POST   /api/stations                    # Register a new station
GET    /api/stations                    # List my stations + shared with me
GET    /api/stations/{id}               # Get station details
PUT    /api/stations/{id}               # Update station settings
DELETE /api/stations/{id}               # Unregister station

POST   /api/stations/{id}/access        # Grant access to user
DELETE /api/stations/{id}/access/{uid}  # Revoke access
GET    /api/stations/{id}/access        # List access grants

POST   /api/stations/{id}/connect       # Connect to station (start session)
POST   /api/stations/{id}/disconnect    # Disconnect from station
GET    /api/stations/{id}/session       # Get current session info
```

### SignalR Events

```csharp
// Station status
StationOnline(stationId)
StationOffline(stationId)
StationStatusChanged(stationId, status)

// Session events
UserConnectedToStation(stationId, userId, playerSlot)
UserDisconnectedFromStation(stationId, userId)
PlayerSlotAssigned(stationId, userId, slot)

// Access events
StationAccessGranted(stationId, userId, permission)
StationAccessRevoked(stationId, userId)

// Input events (station → server → station)
ControllerInput(stationId, userId, inputData)
KeyboardInput(stationId, userId, inputData)
MouseInput(stationId, userId, inputData)
```

### Client Architecture

#### Station Mode (Gaming PC)

```
┌─────────────────────────────────────────────────┐
│                  Station Service                │
├─────────────────────────────────────────────────┤
│ ┌─────────────┐ ┌─────────────┐ ┌─────────────┐ │
│ │   Screen    │ │   Input     │ │   Audio     │ │
│ │   Capture   │ │   Receiver  │ │   Capture   │ │
│ └─────────────┘ └─────────────┘ └─────────────┘ │
├─────────────────────────────────────────────────┤
│ ┌─────────────────────────────────────────────┐ │
│ │            WebRTC Connections               │ │
│ │   (one per connected user)                  │ │
│ └─────────────────────────────────────────────┘ │
├─────────────────────────────────────────────────┤
│ ┌─────────────────────────────────────────────┐ │
│ │         SignalR (signaling, presence)       │ │
│ └─────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────┘
```

#### Client Mode (Laptop/Phone)

```
┌─────────────────────────────────────────────────┐
│                 Station Client                  │
├─────────────────────────────────────────────────┤
│ ┌─────────────┐ ┌─────────────┐ ┌─────────────┐ │
│ │   Video     │ │   Input     │ │   Audio     │ │
│ │   Renderer  │ │   Sender    │ │   Playback  │ │
│ └─────────────┘ └─────────────┘ └─────────────┘ │
├─────────────────────────────────────────────────┤
│ ┌─────────────────────────────────────────────┐ │
│ │          WebRTC Connection to Station       │ │
│ └─────────────────────────────────────────────┘ │
├─────────────────────────────────────────────────┤
│ ┌─────────────────────────────────────────────┐ │
│ │         SignalR (signaling, presence)       │ │
│ └─────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────┘
```

### Connection Flow

```
1. Client requests connection
   Client → Server: POST /api/stations/{id}/connect

2. Server validates permissions, creates session
   Server → Station: SignalR UserConnectingToStation(userId)

3. Station prepares WebRTC offer
   Station → Server: SignalR WebRtcOffer(userId, sdp)
   Server → Client: SignalR WebRtcOffer(stationId, sdp)

4. Client responds with answer
   Client → Server: SignalR WebRtcAnswer(stationId, sdp)
   Server → Station: SignalR WebRtcAnswer(userId, sdp)

5. ICE candidate exchange (peer-to-peer connection setup)

6. Stream established
   Station sends: Video (screen capture) + Audio (game audio)
   Client sends: Input data (via WebRTC data channel)
```

## Security Considerations

### Authentication
- Station authenticates to server with user credentials + machine ID
- Machine ID prevents token theft (token only works from registered machine)
- Optional: require 2FA to register a new station

### Authorization
- All access checks happen server-side
- Station verifies user permissions before accepting input
- Rate limiting on input to prevent abuse

### Input Validation
- Station validates all input before injecting
- Configurable input filtering (e.g., block certain key combinations)
- Input logging for security audit

### Network Security
- All signaling through server (no direct IP exposure)
- WebRTC with DTLS encryption
- Optional: require TURN relay (no direct peer connection)

### Privacy
- Station owner can see who is connected
- Notification when someone connects
- Option to require approval for each connection
- Session history/audit log

## Future Possibilities

### Wake-on-LAN
- Server sends magic packet to wake station
- Requires network configuration guide

### Mobile App
- View/control stations from phone
- Touch controls mapped to gamepad

### Game Library Integration
- See installed games on station
- Launch games remotely
- Steam/Epic integration

### Scheduling
- Grant time-limited access ("play for 2 hours")
- Scheduled availability ("available 6pm-10pm")

### Quality Presets
- Auto-adjust based on network conditions
- Per-user quality settings
- Bandwidth usage limits

### Recording/Streaming
- Record sessions
- Stream to Twitch/YouTube from station
- Clip capture

## Implementation Phases

### Phase 1: Core Infrastructure
- [ ] Station registration and persistence
- [ ] Access grant system
- [ ] Station online/offline status
- [ ] Basic API endpoints

### Phase 2: Connection & Streaming
- [ ] Station mode UI (minimal/tray)
- [ ] Client connection flow
- [ ] Video streaming (reuse existing WebRTC)
- [ ] Audio streaming

### Phase 3: Input & Control
- [ ] Keyboard/mouse input forwarding
- [ ] Controller input forwarding
- [ ] Player slot assignment
- [ ] Input permission enforcement

### Phase 4: Polish & UX
- [ ] Gaming Stations sidebar section
- [ ] Connection view UI
- [ ] Settings and management UI
- [ ] Notifications and status indicators

### Phase 5: Advanced Features
- [ ] Voice channel integration
- [ ] Wake-on-LAN
- [ ] Quality auto-adjustment
- [ ] Session history

## Open Questions

1. **Should stations be visible in communities?**
   - Could show "Gaming Stations" section in community sidebar
   - Community admins could add shared stations

2. **How to handle station going offline mid-session?**
   - Reconnection attempt?
   - Notification to connected users?

3. **Mobile support?**
   - Touch controls feasible?
   - Or view-only on mobile?

4. **Pricing/limits?**
   - Free tier: 1 station, 2 concurrent users?
   - Premium: unlimited stations, more users?

5. **Peer-to-peer vs server relay?**
   - P2P: lower latency, but exposes IPs
   - Relay: higher latency, but more private
   - Option to choose?
