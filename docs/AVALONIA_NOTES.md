# Avalonia Development Notes

This document captures lessons learned and gotchas encountered while developing the Miscord client with Avalonia UI.

---

## Virtualization with Variable-Height Items

**Problem:** When using `VirtualizingStackPanel` with items that have variable heights (e.g., chat messages where some have images/link previews and others don't), the virtualizer struggles to estimate heights correctly. This causes:
- Being able to scroll past the content (empty space appears)
- Jumpy scrolling behavior
- Layout recalculations causing items to shift

**Solution:** Use a regular `StackPanel` instead of `VirtualizingStackPanel` when items have unpredictable heights.

```xml
<!-- Instead of this -->
<ItemsControl.ItemsPanel>
    <ItemsPanelTemplate>
        <VirtualizingStackPanel/>
    </ItemsPanelTemplate>
</ItemsControl.ItemsPanel>

<!-- Use this -->
<ItemsControl.ItemsPanel>
    <ItemsPanelTemplate>
        <StackPanel/>
    </ItemsPanelTemplate>
</ItemsControl.ItemsPanel>
```

**Trade-off:** All items are rendered at once, using more memory. This is acceptable when the item count is bounded (e.g., loading 50 messages at a time).

**When to use VirtualizingStackPanel:** Only when items have consistent, predictable heights (e.g., a list of fixed-height rows).

---

## Image Caching for Scrollable Lists

**Problem:** When images are loaded asynchronously in list items, scrolling causes:
- Images to clear and reload (flicker)
- Layout shifts as images load (height changes from 0 to image height)
- Poor scroll performance

**Solution:** Implement a static image cache that persists across control instances.

```csharp
// Static cache shared across all instances
private static readonly Dictionary<string, Bitmap> ImageCache = new();
private static readonly HashSet<string> LoadingImages = new();
private static readonly object CacheLock = new();

private static void LoadImageAsync(string imageUrl, Border container)
{
    // Check cache first - display immediately if cached
    lock (CacheLock)
    {
        if (ImageCache.TryGetValue(imageUrl, out var cachedBitmap))
        {
            container.Child = new Image { Source = cachedBitmap, ... };
            return;
        }
        // ... load from network and cache
    }
}
```

**Key points:**
- Check cache synchronously before any async operations
- Display cached images immediately (not via Dispatcher.Post) to ensure correct height calculation
- Track loading state to prevent duplicate network requests for the same URL

---

## Button Click vs PointerPressed Events

**Problem:** `PointerPressed` events don't fire on `Button` controls because the Button handles and swallows the event.

**Solution:**
- Use `Click` event for `Button` controls
- Use `PointerPressed` for non-button elements like `Border` that need click handling

```xml
<!-- For Button - use Click -->
<Button Click="OnButtonClick" />

<!-- For Border/other controls - use PointerPressed -->
<Border PointerPressed="OnBorderPressed" />
```

---

## ItemsControl Width Stretching

**Problem:** Items inside an `ItemsControl` with `ScrollViewer` don't stretch to full width by default.

**Solution:** Bind the `ItemsControl.Width` to the `ScrollViewer.Viewport.Width`:

```xml
<ScrollViewer x:Name="MessagesScrollViewer">
    <ItemsControl ItemsSource="{Binding Items}"
                  Width="{Binding $parent[ScrollViewer].Viewport.Width}">
        ...
    </ItemsControl>
</ScrollViewer>
```

This ensures items take the full available width while still allowing vertical scrolling.

---

## Adding More Notes

When you encounter a new gotcha or learn something important about Avalonia development, add it here following the same format:
1. **Problem:** What went wrong
2. **Solution:** How to fix it
3. Code example if applicable
4. Any trade-offs or considerations
