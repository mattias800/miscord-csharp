#include "x11_window.h"
#include <stdio.h>
#include <X11/Xutil.h>
#include <X11/extensions/Xfixes.h>
#include <X11/extensions/shape.h>

Window x11_create_overlay_window(Display* display, int width, int height) {
    if (!display) {
        return 0;
    }

    int screen = DefaultScreen(display);
    Window root = RootWindow(display, screen);

    // Get visual info
    XVisualInfo visual_info;
    if (!XMatchVisualInfo(display, screen, 24, TrueColor, &visual_info)) {
        fprintf(stderr, "X11Window: Cannot get visual info\n");
        return 0;
    }

    // Create colormap
    Colormap colormap = XCreateColormap(display, root, visual_info.visual, AllocNone);

    // Set window attributes
    XSetWindowAttributes attrs;
    attrs.colormap = colormap;
    attrs.border_pixel = 0;
    attrs.background_pixel = 0;
    attrs.override_redirect = True;  // Bypass window manager
    attrs.event_mask = ExposureMask | StructureNotifyMask;

    unsigned long attr_mask = CWColormap | CWBorderPixel | CWBackPixel | CWOverrideRedirect | CWEventMask;

    // Create window
    Window window = XCreateWindow(
        display,
        root,
        0, 0,
        width, height,
        0,  // border width
        visual_info.depth,
        InputOutput,
        visual_info.visual,
        attr_mask,
        &attrs
    );

    if (!window) {
        fprintf(stderr, "X11Window: Cannot create window\n");
        XFreeColormap(display, colormap);
        return 0;
    }

    // Set window class
    XClassHint class_hint;
    class_hint.res_name = "snacka_video";
    class_hint.res_class = "SnackaVideoOverlay";
    XSetClassHint(display, window, &class_hint);

    // Make window click-through
    x11_set_click_through(display, window);

    XFlush(display);

    return window;
}

void x11_destroy_overlay_window(Display* display, Window window) {
    if (display && window) {
        XDestroyWindow(display, window);
        XFlush(display);
    }
}

void x11_set_window_geometry(Display* display, Window window, int x, int y, int width, int height) {
    if (!display || !window) return;

    XMoveResizeWindow(display, window, x, y, width, height);
    XRaiseWindow(display, window);
    XFlush(display);
}

bool x11_set_click_through(Display* display, Window window) {
    if (!display || !window) return false;

    // Check for XFixes extension
    int event_base, error_base;
    if (!XFixesQueryExtension(display, &event_base, &error_base)) {
        fprintf(stderr, "X11Window: XFixes extension not available\n");
        return false;
    }

    // Create an empty region (no input area)
    XserverRegion region = XFixesCreateRegion(display, NULL, 0);

    // Apply the region as input shape
    XFixesSetWindowShapeRegion(display, window, ShapeInput, 0, 0, region);

    XFixesDestroyRegion(display, region);
    XFlush(display);

    return true;
}

void x11_show_window(Display* display, Window window) {
    if (!display || !window) return;

    XMapWindow(display, window);
    XRaiseWindow(display, window);
    XFlush(display);
}

void x11_hide_window(Display* display, Window window) {
    if (!display || !window) return;

    XUnmapWindow(display, window);
    XFlush(display);
}
