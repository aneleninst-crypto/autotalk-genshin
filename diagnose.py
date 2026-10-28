"""Read-only diagnostics for the foreground window and dialogue pixels."""
import ctypes
from ctypes import wintypes

user32 = ctypes.windll.user32
gdi32 = ctypes.windll.gdi32
user32.GetForegroundWindow.restype = wintypes.HWND
user32.GetDC.restype = wintypes.HDC
user32.GetDC.argtypes = [wintypes.HWND]
gdi32.GetPixel.restype = wintypes.DWORD
gdi32.GetPixel.argtypes = [wintypes.HDC, ctypes.c_int, ctypes.c_int]

class RECT(ctypes.Structure):
    _fields_ = [("left", ctypes.c_long), ("top", ctypes.c_long), ("right", ctypes.c_long), ("bottom", ctypes.c_long)]

class POINT(ctypes.Structure):
    _fields_ = [("x", ctypes.c_long), ("y", ctypes.c_long)]

hwnd = user32.GetForegroundWindow()
title = ctypes.create_unicode_buffer(512)
user32.GetWindowTextW(hwnd, title, 512)
pid = wintypes.DWORD()
user32.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
rect = RECT()
origin = POINT(0, 0)
user32.GetClientRect(hwnd, ctypes.byref(rect))
user32.ClientToScreen(hwnd, ctypes.byref(origin))
width, height = rect.right - rect.left, rect.bottom - rect.top
print(f"foreground={title.value!r} pid={pid.value} client={width}x{height} origin=({origin.x},{origin.y})")
dc = user32.GetDC(None)
for name, bx, by in [("auto",84,46),("option_low",1301,808),("option_high",1301,790),("loading",1200,700)]:
    x = origin.x + round(bx * width / 1920)
    y = origin.y + round(by * height / 1080)
    value = gdi32.GetPixel(dc, x, y)
    print(f"{name}=({value & 255},{(value >> 8) & 255},{(value >> 16) & 255}) at ({x},{y})")
user32.ReleaseDC(None, dc)

helper = user32.FindWindowW(None, "Genshin Dialogue Helper")
if helper:
    lines = []
    @ctypes.WINFUNCTYPE(ctypes.c_bool, wintypes.HWND, wintypes.LPARAM)
    def child_text(child, _):
        text = ctypes.create_unicode_buffer(512)
        user32.GetWindowTextW(child, text, 512)
        if text.value:
            lines.append(text.value)
        return True
    user32.EnumChildWindows(helper, child_text, 0)
    print("helper_controls:", ascii(lines))
