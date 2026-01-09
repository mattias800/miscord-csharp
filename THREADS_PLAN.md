# Miscord Threads Implementation Plan

## Overview
Add Slack-style message threads to Miscord, allowing users to have organized sub-conversations within channels. Threads will support all messaging features: replies, edits, deletions, and reactions.

## Architecture Overview

### Thread Model
Threads are sub-conversations branching from a parent message in a channel:
- Each thread has a **parent message** (the starting message in the channel)
- Thread contains **replies** (messages within the thread)
- All thread operations (edit, delete, react) work identically to channel messages
- Parent message shows **reply count** and **latest reply timestamp**

### Key Concepts
- **Parent Message**: Original message in channel that starts a thread
- **Thread ID**: Unique identifier (same as parent message ID)
- **Thread Replies**: All responses within that thread
- **Unread Thread Badge**: Count of unread replies per thread

---

## Database Schema Changes

### New Entity: ThreadReply
Add a new entity to track replies within threads:

```
ThreadReply
├─ Id (PK, Guid)
├─ Content (string, required)
├─ ParentMessageId (FK → Message) - The original channel message
├─ AuthorId (FK → User)
├─ Author (User navigation)
├─ ChannelId (FK → Channel)
├─ Channel (Channel navigation)
├─ CreatedAt (DateTime)
├─ UpdatedAt (DateTime)
├─ IsEdited (bool computed from CreatedAt vs UpdatedAt)
```

### Modified Entity: Message
Add thread tracking to existing Message entity:

```
Message
├─ ... (existing fields)
├─ ReplyCount (int) - Count of replies in thread
├─ LastReplyAt (DateTime?) - Timestamp of most recent reply
├─ ThreadReplies (ICollection<ThreadReply>) - All replies in this thread
```

### New Entity: ThreadReplyReaction
Add reactions support for thread replies:

```
ThreadReplyReaction
├─ Id (PK, Guid)
├─ EmojiName (string, required)
├─ UserId (FK → User)
├─ User (User navigation)
├─ ThreadReplyId (FK → ThreadReply)
├─ ThreadReply (ThreadReply navigation)
├─ CreatedAt (DateTime)
```

### EF Core Configuration

**DbContext additions:**
```csharp
public DbSet<ThreadReply> ThreadReplies => Set<ThreadReply>();
public DbSet<ThreadReplyReaction> ThreadReplyReactions => Set<ThreadReplyReaction>();
```

**Relationships to configure:**
- Message → ThreadReplies (one-to-many, cascade delete)
- ThreadReply → Author (many-to-one)
- ThreadReply → Channel (many-to-one, for query optimization)
- ThreadReplyReaction → ThreadReply (many-to-one, cascade delete)
- ThreadReplyReaction → User (many-to-one)

**Performance indexes:**
- `ThreadReply.ParentMessageId` (for fetching thread replies)
- `ThreadReply.AuthorId` (for user activity queries)
- `ThreadReplyReaction.ThreadReplyId` (for reaction lookup)

---

## Phase 2.1: Backend - Thread Replies

### Endpoints to Implement

#### Thread Management
- `GET /api/channels/{channelId}/messages/{messageId}/thread` - Get all replies in a thread
  - Query parameters: `page`, `pageSize`, `sort` (newest/oldest)
  - Returns: List of ThreadReply objects with author info and reactions
  - Pagination support for large threads

- `POST /api/channels/{channelId}/messages/{messageId}/thread/replies` - Add reply to thread
  - Body: `{ content: string }`
  - Returns: Created ThreadReply with author and timestamps
  - Updates parent message `ReplyCount` and `LastReplyAt`

- `PUT /api/thread-replies/{replyId}` - Edit thread reply
  - Body: `{ content: string }`
  - Returns: Updated ThreadReply
  - Track edit timestamp in `UpdatedAt`

- `DELETE /api/thread-replies/{replyId}` - Delete thread reply
  - Decrements parent message `ReplyCount`
  - Soft delete or hard delete (TBD based on requirements)

#### Reactions in Threads
- `POST /api/thread-replies/{replyId}/reactions` - Add reaction to thread reply
  - Body: `{ emojiName: string }`
  - Returns: Created ThreadReplyReaction

- `DELETE /api/thread-replies/{replyId}/reactions/{emojiName}` - Remove reaction from thread reply
  - Returns: 204 No Content

- `GET /api/thread-replies/{replyId}/reactions` - Get all reactions for thread reply
  - Returns: List of reactions grouped by emoji

### SignalR Events for Threads

#### Real-time Thread Updates
- `SendThreadReply(parentMessageId, content)` - Send reply (from client)
- `ReceiveThreadReply(threadReply)` - Notify channel users of new reply (to client)
- `ThreadReplyEdited(replyId, newContent)` - Notify of edited reply
- `ThreadReplyDeleted(replyId)` - Notify of deleted reply
- `ThreadReplyReactionAdded(replyId, emoji, userId)` - Notify of reaction
- `ThreadReplyReactionRemoved(replyId, emoji, userId)` - Notify of reaction removal
- `ThreadUnreadCountUpdated(parentMessageId, unreadCount, userId)` - Per-user unread count

#### Hub Groups
- Create group per thread: `thread_{parentMessageId}`
- Users join group when opening thread
- Users leave group when closing thread
- Broadcast updates only to users in that thread group

### Data Transfer Objects (DTOs)

```csharp
// Request DTOs
public class CreateThreadReplyDto
{
    public required string Content { get; set; }
}

public class UpdateThreadReplyDto
{
    public required string Content { get; set; }
}

public class AddReactionDto
{
    public required string EmojiName { get; set; }
}

// Response DTOs
public class ThreadReplyDto
{
    public Guid Id { get; set; }
    public required string Content { get; set; }
    public Guid ParentMessageId { get; set; }
    public Guid AuthorId { get; set; }
    public UserDto? Author { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsEdited => UpdatedAt > CreatedAt;
    public List<ThreadReplyReactionDto> Reactions { get; set; } = new();
}

public class ThreadDto
{
    public Guid ParentMessageId { get; set; }
    public MessageDto? ParentMessage { get; set; }
    public int ReplyCount { get; set; }
    public DateTime? LastReplyAt { get; set; }
    public List<ThreadReplyDto> Replies { get; set; } = new();
}

public class ThreadReplyReactionDto
{
    public required string EmojiName { get; set; }
    public List<UserDto> Users { get; set; } = new();
    public int Count { get; set; }
    public bool CurrentUserReacted { get; set; }
}
```

### Service Layer

**IThreadService interface:**
```csharp
public interface IThreadService
{
    Task<ThreadDto> GetThreadAsync(Guid parentMessageId, int page = 1, int pageSize = 50);
    Task<ThreadReplyDto> AddReplyAsync(Guid parentMessageId, Guid userId, string content);
    Task<ThreadReplyDto> UpdateReplyAsync(Guid replyId, Guid userId, string content);
    Task DeleteReplyAsync(Guid replyId, Guid userId);
    Task<ThreadReplyReactionDto> AddReactionAsync(Guid replyId, Guid userId, string emoji);
    Task RemoveReactionAsync(Guid replyId, Guid userId, string emoji);
    Task<int> GetUnreadThreadCountAsync(Guid channelId, Guid userId);
}
```

**Key implementation details:**
- Authorization: Verify user owns reply before edit/delete
- Validation: Content length limits, emoji validation
- Pagination: Implement cursor-based pagination for large threads
- Caching: Cache thread metadata (reply count, last reply time)

---

## Phase 4: UI - Thread Display

### Thread UI Components

#### 1. Thread Panel (Replaces User List)
**Location:** Right sidebar, appears when thread is opened
**Layout:**
```
┌──────────────────────────┐
│ Thread                [X] │  (Close button at top)
├──────────────────────────┤
│ [Parent Message Preview] │
│ Author | 2m ago          │
│ Original message content │
│ 5 replies                │
├──────────────────────────┤
│ [ThreadReplyItem] ×5     │  (List of replies)
│ Author | timestamp       │
│ Reply content            │
│ [emoji] [edit] [delete] │
├──────────────────────────┤
│ [Input field for reply]  │
│ [Send button]            │
└──────────────────────────┘
```

#### 2. Message with Thread Indicator
**Location:** Channel message list
**Changes to MessageItem:**
- Add "N replies" badge showing reply count
- Add "Last reply 2m ago" timestamp
- Clickable to open thread panel
- Show thread indicator icon (chat bubble with number)

#### 3. Thread Reply Item Component
**Features:**
- Author avatar and name
- Timestamp (e.g., "2m ago")
- Reply content with text formatting
- Reactions display (emoji + count)
- Action buttons (reply, edit, delete) - visible on hover
- Edit indicator "edited" text if message was edited

#### 4. Reactions Section in Thread Reply
**Display:**
- Show emoji groups (e.g., 👍 3, 🎉 1, 🔥 2)
- Click emoji to add/remove reaction
- Highlight if current user already reacted
- Hover to show list of users who reacted

### MVVM Architecture for Threads

**ViewModels to Create:**

```csharp
public class ThreadViewModel : IDisposable
{
    public Guid ParentMessageId { get; }
    public MessageViewModel ParentMessage { get; }
    public ObservableCollection<ThreadReplyViewModel> Replies { get; }
    public int ReplyCount { get; set; }
    public DateTime? LastReplyAt { get; set; }
    
    // Commands
    public ICommand SendReplyCommand { get; }
    public ICommand CloseThreadCommand { get; }
    
    // Methods
    Task LoadThreadAsync();
    Task SendReplyAsync(string content);
    Task RefreshAsync();
}

public class ThreadReplyViewModel
{
    public Guid Id { get; }
    public Guid ParentMessageId { get; }
    public string Content { get; set; }
    public UserViewModel Author { get; }
    public DateTime CreatedAt { get; }
    public DateTime UpdatedAt { get; }
    public bool IsEdited => UpdatedAt > CreatedAt;
    public bool IsCurrentUserAuthor { get; }
    
    public ObservableCollection<ReactionViewModel> Reactions { get; }
    
    // Commands
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand AddReactionCommand { get; }
}

public class ReactionViewModel
{
    public string EmojiName { get; }
    public int Count { get; }
    public bool CurrentUserReacted { get; set; }
    public List<UserViewModel> Users { get; }
    
    public ICommand ToggleReactionCommand { get; }
}
```

### UI Hierarchy

**Main Channel Window (existing):**
```
├─ ServerListPanel
├─ ChannelListPanel
├─ ChatPanel
│  └─ MessageItem (with thread indicator)
│     └─ Click → Open Thread
└─ RightPanel
   ├─ ThreadPanel (NEW - replaces UserListPanel)
   │  ├─ ParentMessagePreview
   │  ├─ ThreadReplyList
   │  │  └─ ThreadReplyItem × N
   │  │     └─ ReactionRow
   │  └─ ReplyInputField
   └─ UserListPanel (hidden when thread open)
```

### Interaction Flow

1. **User clicks "N replies" badge on channel message**
   - MainViewModel detects click
   - Calls `threadViewModel.LoadThreadAsync()`
   - ThreadPanel becomes visible
   - UserListPanel is hidden
   - Thread replies load and display

2. **User types in thread reply input and sends**
   - ThreadViewModel.SendReplyCommand executes
   - SignalR sends `SendThreadReply` event
   - Server adds reply to database
   - SignalR broadcasts `ReceiveThreadReply` to connected clients
   - ThreadReplyList updates in real-time

3. **User edits reply**
   - ThreadReplyViewModel.EditCommand shows edit dialog
   - User confirms changes
   - SignalR sends update
   - Message content updates in UI

4. **User closes thread (X button)**
   - ThreadPanel is hidden
   - UserListPanel is shown
   - Leave SignalR group for this thread

### State Management

**MainViewModel thread state:**
```csharp
public ThreadViewModel? CurrentThread { get; set; }

public void OpenThread(Guid parentMessageId)
{
    CurrentThread = new ThreadViewModel(parentMessageId);
    CurrentThread.LoadThreadAsync();
}

public void CloseThread()
{
    CurrentThread?.Dispose();
    CurrentThread = null;
}
```

**ChannelViewModel message list:**
- Include `ReplyCount` and `LastReplyAt` in MessageViewModel
- Subscribe to thread updates via SignalR to update counts
- Bind "N replies" text to `ReplyCount`

---

## Phase 5: Advanced Thread Features

### 5.1 Unread Thread Tracking
**Database addition:**
```csharp
public class UserThreadUnread
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ParentMessageId { get; set; }
    public int UnreadCount { get; set; }
    public DateTime LastReadAt { get; set; }
}
```

**Features:**
- Track unread reply count per user per thread
- Badge showing unread count on parent message
- Mark as read when user opens thread
- Broadcast unread count updates via SignalR

### 5.2 Thread Notifications
- Notify user when someone replies to a thread they're in
- Notify user when someone reacts to their reply
- Configurable notification settings per user

### 5.3 Thread Pagination & Performance
- Implement cursor-based pagination (load older replies)
- Lazy load replies as user scrolls
- Cache thread metadata separately from replies
- Index queries on ParentMessageId for fast lookup

### 5.4 Thread Search
- Search within thread replies
- Search for threads mentioning specific users
- Search for threads with specific reactions

### 5.5 Thread Exports
- Export thread to PDF or markdown
- Copy thread link to clipboard

---

## Migration Plan

### Step 1: Database Schema (EF Core)
1. Create migration file for new entities
2. Add ThreadReply and ThreadReplyReaction DbSets
3. Configure relationships and indexes
4. Apply migration

**Migration Commands:**
```bash
dotnet ef migrations add AddThreadSupport --project src/Miscord.Server
dotnet ef database update --project src/Miscord.Server
```

### Step 2: Backend API (Phase 2.1)
1. Implement IThreadService with all methods
2. Create ThreadController with endpoints
3. Add SignalR hub methods for thread events
4. Add authorization/validation logic

### Step 3: DTOs and Models
1. Create ThreadReplyDto, ThreadDto classes
2. Add AutoMapper profiles for entity → DTO mapping
3. Add validation attributes

### Step 4: UI Components (Phase 4)
1. Create ThreadPanel.axaml and ThreadViewModel
2. Create ThreadReplyItem.axaml and ThreadReplyViewModel
3. Create ReactionRow.axaml and ReactionViewModel
4. Integrate with MessageItem (add reply count badge)
5. Add thread opening logic to ChannelViewModel

### Step 5: SignalR Integration
1. Add thread event handlers to ChatHub
2. Implement thread group management
3. Test real-time updates

### Step 6: Testing
1. Unit tests for IThreadService
2. Integration tests for thread endpoints
3. UI tests for thread panel interactions
4. SignalR connection tests

---

## Implementation Order & Dependencies

1. **Database Schema** (prerequisite for everything)
   - Add ThreadReply entity
   - Add ThreadReplyReaction entity
   - Modify Message entity with ReplyCount, LastReplyAt
   - Create and apply migration

2. **Backend Service** (prerequisite for API)
   - Implement IThreadService
   - Add CRUD operations for replies
   - Add reaction management

3. **API Endpoints** (prerequisite for client)
   - GET thread with pagination
   - POST reply
   - PUT/DELETE reply
   - Reaction endpoints

4. **SignalR Events** (for real-time updates)
   - Thread reply events
   - Reaction events
   - Group management

5. **DTOs & Mapping**
   - Create all DTOs
   - Setup AutoMapper profiles

6. **UI Components** (client-side)
   - ThreadPanel component
   - ThreadReplyItem component
   - ReactionRow component
   - Integrate with existing message display

7. **State Management**
   - ThreadViewModel
   - ThreadReplyViewModel
   - ReactionViewModel
   - Connect to MainViewModel

8. **Testing** (throughout)
   - Unit tests for service
   - Integration tests for API
   - UI component tests

---

## Estimated Effort

| Phase | Component | LOC | Days | Notes |
|-------|-----------|-----|------|-------|
| 2.1 | DB Schema | 150 | 0.5 | New entities, migration |
| 2.1 | Thread Service | 600 | 3-4 | CRUD, reactions, validation |
| 2.1 | API Endpoints | 400 | 2-3 | REST controllers |
| 2.1 | SignalR Integration | 300 | 2 | Hub methods, groups |
| 4 | Thread UI Components | 1000 | 5-6 | Panel, reply item, reactions |
| 4 | Thread ViewModels | 500 | 3 | MVVM architecture |
| 4 | Integration | 300 | 2 | Connect components |
| 5 | Testing | 400 | 2-3 | Unit, integration, UI tests |
| 5 | Advanced Features | 500 | 3-4 | Unread, notifications, search |
| **Total** | | **4150** | **23-26 days** | ~4-5 weeks effort |

---

## Success Criteria

- ✅ Users can reply to channel messages in threads
- ✅ Thread replies support all message features (edit, delete, reactions)
- ✅ Parent message shows reply count and last reply timestamp
- ✅ Thread panel replaces user list when opened
- ✅ Close button returns to user list
- ✅ Real-time updates via SignalR for new replies
- ✅ Reactions work identically in threads and channels
- ✅ Proper authorization (users can only edit/delete own messages)
- ✅ Pagination for large threads
- ✅ All tests passing (>80% coverage)

---

## Database Schema Diagram

```
Channel
  ├─ Messages (1:*)
  │   ├─ ThreadReplies (1:*)
  │   │   ├─ Author (FK → User)
  │   │   └─ ThreadReplyReactions (1:*)
  │   │       └─ User (FK → User)
  │   └─ MessageReactions (existing)

User
  ├─ SentMessages
  ├─ ThreadReplies
  ├─ ThreadReplyReactions
  └─ UserThreadUnread (future)
```

---

## Future Enhancements (Phase 5+)

- **Muted Threads**: Users can mute notifications for specific threads
- **Followed Threads**: Explicitly follow threads you're interested in
- **Thread Summary**: AI-generated summaries of long threads
- **Thread Pinning**: Pin important threads to top of channel
- **Thread Analytics**: See most active threads, engagement stats
- **Thread Bookmarks**: Save threads for later reading
- **Thread Mentions**: Mention threads in other channels
- **Thread Previews**: Hover preview of thread contents
- **Nested Replies**: Allow replies-to-replies (3-level depth max)

---

## Implementation Checklist

**Database:**
- [ ] Create ThreadReply entity
- [ ] Create ThreadReplyReaction entity
- [ ] Modify Message entity
- [ ] Create EF Core migration
- [ ] Add relationships in DbContext
- [ ] Add indexes for performance

**Backend:**
- [ ] Implement IThreadService
- [ ] Create ThreadController
- [ ] Add SignalR hub methods
- [ ] Create DTOs and AutoMapper profiles
- [ ] Add validation and authorization

**Frontend:**
- [ ] Create ThreadPanel component
- [ ] Create ThreadReplyItem component
- [ ] Create ReactionRow component
- [ ] Create ViewModels (Thread, ThreadReply, Reaction)
- [ ] Integrate with ChannelViewModel
- [ ] Add thread opening/closing logic

**Testing:**
- [ ] Unit tests for IThreadService
- [ ] Integration tests for API endpoints
- [ ] UI component tests
- [ ] SignalR event tests
- [ ] End-to-end thread flow tests

**Documentation:**
- [ ] Update API documentation
- [ ] Update user guide
- [ ] Add code comments

---

## Notes for Developers

### Performance Considerations
1. **Pagination is critical**: Don't load all replies at once
2. **Cache thread metadata**: ReplyCount and LastReplyAt should be cached
3. **Index ParentMessageId**: Most common query filter
4. **Lazy load author info**: Load user details only when needed
5. **SignalR group management**: Minimize broadcast scope

### Threading Model
- Use async/await throughout
- Handle concurrent replies gracefully
- Ensure edit/delete operations are atomic

### User Experience
- Show "loading..." while fetching thread
- Smooth scroll to newest reply
- Preserve scroll position when adding reactions
- Show "X is typing..." indicator in thread

### Testing Strategy
- Mock IThreadService for UI tests
- Test SignalR events with test hub
- Verify authorization on all endpoints
- Test concurrent operations (2 users editing same reply)

---

**Last Updated:** 2026-01-09  
**Status:** Plan Created, Ready for Implementation  
**Estimated Start:** After Phase 2 (Direct Messages complete)  
**Estimated Completion:** 4-5 weeks from start
