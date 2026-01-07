# Screen Share Drawing Annotations

This guide explains how to use the drawing annotation feature during screen shares in Miscord.

## Overview

When sharing your screen, you and other participants can draw annotations directly on the shared content. This is useful for:
- Highlighting areas of interest during presentations
- Collaborative debugging or code review
- Teaching and demonstrations
- Pointing out UI elements or issues

## For the Host (Screen Sharer)

### Starting a Screen Share

1. Join a voice channel
2. Click the **screen share button** in the voice controls
3. Select a **display** (monitor) to share
   - Note: Drawing annotations are only available for display sharing, not window sharing
4. Click **Start Sharing**

### The Annotation Toolbar

When you start sharing a display, a toolbar appears at the bottom of your shared screen:

| Button | Function |
|--------|----------|
| **Draw** | Toggle drawing mode on/off |
| **Allow** | Toggle whether viewers can draw (in main UI) |
| **Colors** | Select your drawing color |
| **Clear** | Erase all drawings from all users |
| **X** | Stop screen sharing |

### Enabling Drawing for Viewers

By default, viewers cannot draw on your screen share. To allow them:

1. Look for the **"Allow Drawing"** toggle button in the main application UI (near the screen share button)
2. Click it to enable viewer drawing
3. Click again to disable and clear all drawings

### Drawing as the Host

1. Click the **Draw** button on the toolbar to enable draw mode
2. Click and drag on your screen to draw
3. Select different colors from the color palette
4. Click **Draw** again to disable draw mode and interact with your desktop normally

### Important Notes for Host

- When draw mode is **enabled**: Your mouse clicks are captured for drawing
- When draw mode is **disabled**: Your mouse clicks pass through to your desktop normally
- The **Clear** button erases all drawings (yours and viewers')
- Disabling **Allow Drawing** also clears all drawings

## For Viewers (Guests)

### Viewing a Screen Share

1. Join the same voice channel as the host
2. When someone shares their screen, a video tile appears
3. Click the **fullscreen button** (⛶) on the screen share tile to expand it

### Drawing on a Screen Share

1. The host must have **Allow Drawing** enabled
2. Open the screen share in **fullscreen mode**
3. Click the **pencil icon** to enable drawing mode
4. Click and drag to draw
5. Select colors from the color palette at the bottom
6. Click the pencil icon again to disable drawing mode

### Exiting Fullscreen

- Press **Escape** or click the **X** button to exit fullscreen view

## Color Palette

Both hosts and viewers can choose from these colors:
- Red (default)
- Green
- Blue
- Yellow
- Magenta
- Cyan
- White
- Black

## Tips

1. **Use contrasting colors**: Choose colors that stand out against the shared content
2. **Clear regularly**: Use the Clear button to keep the screen uncluttered
3. **Coordinate with viewers**: Let viewers know when you've enabled drawing for them
4. **Short strokes work best**: Draw short, deliberate strokes rather than long continuous lines

## Troubleshooting

### Drawings not appearing for others
- Ensure you're connected to the voice channel
- Check that the host has enabled "Allow Drawing" (for viewers)
- Try refreshing the fullscreen view

### Can't draw on the screen
- **Host**: Make sure Draw mode is enabled on the toolbar
- **Viewer**: Make sure you're in fullscreen mode and the host has allowed drawing

### Mouse clicks going to desktop instead of drawing (Host)
- Enable Draw mode on the annotation toolbar
- When Draw mode is off, clicks pass through to the desktop by design

### Overlay blocking desktop interaction (Host)
- Disable Draw mode on the toolbar to interact with your desktop
- The overlay becomes click-through when Draw mode is off
