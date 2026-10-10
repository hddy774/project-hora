"""Pure coordinate mapping for rooted-emulator screenshots (no Android/Pillow dependency)."""
import math
import re


def logical_display_size(wm_output):
    """Use the active WM override, or the physical size when no override is set."""
    override = re.search(r"Override size:\s*(\d+)x(\d+)", wm_output)
    physical = re.search(r"Physical size:\s*(\d+)x(\d+)", wm_output)
    match = override or physical
    if not match:
        raise ValueError("wm size did not report a display size")
    size = tuple(map(int, match.groups()))
    if min(size) <= 0:
        raise ValueError("Display dimensions must be positive")
    return size


def display_projection(logical_size, screenshot_size):
    """WM input coordinates project into the centered, aspect-fit physical display.

    A small override is letterboxed in screencap's physical framebuffer; a tall
    override can be scaled down and pillarboxed. Input/UIAutomator coordinates
    remain in the logical display's coordinate system in both cases.
    """
    logical_width, logical_height = logical_size
    image_width, image_height = screenshot_size
    if any(not math.isfinite(v) or v <= 0 for v in (*logical_size, *screenshot_size)):
        raise ValueError("Display and screenshot dimensions must be positive and finite")
    scale = min(image_width / logical_width, image_height / logical_height)
    return scale, (image_width - logical_width * scale) / 2, (image_height - logical_height * scale) / 2


def logical_crop_bounds(rect, logical_size, screenshot_size):
    """Project logical-pixel edges, round once, and clamp to the screenshot."""
    left, top, right, bottom = rect
    if any(not math.isfinite(v) for v in rect) or left >= right or top >= bottom:
        raise ValueError("Crop rectangle must be finite and nonempty")
    scale, offset_x, offset_y = display_projection(logical_size, screenshot_size)
    width, height = screenshot_size
    bounds = (
        max(0, min(width, round(offset_x + left * scale))),
        max(0, min(height, round(offset_y + top * scale))),
        max(0, min(width, round(offset_x + right * scale))),
        max(0, min(height, round(offset_y + bottom * scale))),
    )
    if bounds[0] >= bounds[2] or bounds[1] >= bounds[3]:
        raise ValueError("Crop rectangle is empty after projection/clipping")
    return bounds
