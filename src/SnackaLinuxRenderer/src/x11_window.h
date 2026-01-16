#ifndef X11_WINDOW_H
#define X11_WINDOW_H

#include <stdbool.h>
#include <X11/Xlib.h>

// Create an overlay window (override-redirect, click-through)
Window x11_create_overlay_window(Display* display, int width, int height);

// Destroy an overlay window
void x11_destroy_overlay_window(Display* display, Window window);

// Set window position and size
void x11_set_window_geometry(Display* display, Window window, int x, int y, int width, int height);

// Make window click-through
bool x11_set_click_through(Display* display, Window window);

// Show the window
void x11_show_window(Display* display, Window window);

// Hide the window
void x11_hide_window(Display* display, Window window);

#endif // X11_WINDOW_H
