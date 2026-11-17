import subprocess
import time
import os
import ctypes
from ctypes import wintypes
from PIL import ImageGrab

user32 = ctypes.windll.user32
installers_dir = r"C:\Users\ITSUPA\.gemini\antigravity\scratch\rac_industrial_monitor\LineNodeAgent\bin\Release\net8.0-windows"
agent_exe = os.path.join(installers_dir, "LineNodeAgent.exe")
output_path = r"C:\Users\ITSUPA\.gemini\antigravity\brain\cfc8f59c-822b-49ae-8e56-7b0867344232\minimal_linenodeagent.png"

print("Starting LineNodeAgent...")
proc = subprocess.Popen([agent_exe], cwd=installers_dir)
time.sleep(3)

try:
    hwnd = user32.FindWindowW(None, "PlantOps™ Line Node Agent — Workstation Monitor")
    if not hwnd:
        target_hwnd = None
        def enum_windows_proc(h, l):
            global target_hwnd
            length = user32.GetWindowTextLengthW(h)
            if length > 0:
                buff = ctypes.create_unicode_buffer(length + 1)
                user32.GetWindowTextW(h, buff, length + 1)
                if "Line Node Agent" in buff.value:
                    target_hwnd = h
            return True
        WNDENUMPROC = ctypes.WINFUNCTYPE(ctypes.c_bool, wintypes.HWND, wintypes.LPARAM)
        user32.EnumWindows(WNDENUMPROC(enum_windows_proc), 0)
        hwnd = target_hwnd

    if hwnd:
        user32.SetForegroundWindow(hwnd)
        time.sleep(1)
        rect = wintypes.RECT()
        user32.GetWindowRect(hwnd, ctypes.byref(rect))
        x1 = max(0, rect.left)
        y1 = max(0, rect.top)
        x2 = max(x1 + 100, rect.right)
        y2 = max(y1 + 100, rect.bottom)
        bbox = (x1, y1, x2, y2)
        print(f"Clamped bbox: {bbox}")
        img = ImageGrab.grab(bbox)
        img.save(output_path)
        print(f"Captured window screenshot to {output_path} ({img.size})")
    else:
        print("Window not found, capturing primary monitor...")
        img = ImageGrab.grab()
        img.save(output_path)
        print(f"Captured screen to {output_path}")
finally:
    proc.terminate()
    print("Process terminated.")
