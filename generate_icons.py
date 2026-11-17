import os
import math
from PIL import Image, ImageDraw

SIZES = [(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (24, 24), (16, 16)]

def create_monitor_icon(size):
    w, h = size
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    scale = w / 256.0

    # Outer Monitor Bezel
    pad = int(14 * scale)
    b_left, b_top = pad, pad
    b_right, b_bottom = w - pad, int(190 * scale)
    r = int(24 * scale)
    draw.rounded_rectangle([b_left, b_top, b_right, b_bottom], radius=r, fill=(15, 23, 42, 255), outline=(56, 189, 248, 255), width=max(1, int(6 * scale)))

    # Screen Background (Dark Industrial Cyan)
    s_pad = int(24 * scale)
    s_bottom = int(180 * scale)
    draw.rounded_rectangle([s_pad, s_pad, w - s_pad, s_bottom], radius=int(12 * scale), fill=(6, 15, 30, 255))

    # Grid Lines inside screen
    grid_col = (15, 35, 65, 180)
    for y in range(int(50 * scale), s_bottom, int(30 * scale)):
        draw.line([(s_pad, y), (w - s_pad, y)], fill=grid_col, width=max(1, int(1 * scale)))
    for x in range(int(50 * scale), w - s_pad, int(40 * scale)):
        draw.line([(x, s_pad), (x, s_bottom)], fill=grid_col, width=max(1, int(1 * scale)))

    # Heartbeat / Telemetry Pulse Wave (Vivid Emerald Green & Cyan)
    pts = [
        (s_pad + int(10 * scale), int(105 * scale)),
        (s_pad + int(50 * scale), int(105 * scale)),
        (s_pad + int(70 * scale), int(80 * scale)),
        (s_pad + int(90 * scale), int(145 * scale)),
        (s_pad + int(115 * scale), int(55 * scale)),
        (s_pad + int(140 * scale), int(130 * scale)),
        (s_pad + int(155 * scale), int(105 * scale)),
        (w - s_pad - int(10 * scale), int(105 * scale)),
    ]
    pulse_width = max(2, int(6 * scale))
    draw.line(pts, fill=(16, 185, 129, 255), width=pulse_width, joint="curve")
    
    # Pulse Node Dot
    cx, cy = pts[4]
    nr = int(6 * scale)
    draw.ellipse([cx - nr, cy - nr, cx + nr, cy + nr], fill=(52, 211, 153, 255), outline=(255, 255, 255, 255), width=max(1, int(2 * scale)))

    # Monitor Stand Base
    stand_top = b_bottom
    stand_bottom = int(230 * scale)
    stand_w = int(28 * scale)
    draw.rectangle([w // 2 - stand_w, stand_top, w // 2 + stand_w, stand_bottom], fill=(30, 41, 59, 255), outline=(56, 189, 248, 180), width=max(1, int(2 * scale)))
    
    # Foot
    foot_w = int(70 * scale)
    draw.rounded_rectangle([w // 2 - foot_w, stand_bottom - int(6 * scale), w // 2 + foot_w, int(246 * scale)], radius=int(6 * scale), fill=(15, 23, 42, 255), outline=(56, 189, 248, 255), width=max(1, int(4 * scale)))

    # LEDs on top
    led_r = max(2, int(4 * scale))
    draw.ellipse([s_pad + int(8*scale), s_pad + int(8*scale), s_pad + int(8*scale) + led_r*2, s_pad + int(8*scale) + led_r*2], fill=(16, 185, 129, 255))
    draw.ellipse([s_pad + int(20*scale), s_pad + int(8*scale), s_pad + int(20*scale) + led_r*2, s_pad + int(8*scale) + led_r*2], fill=(56, 189, 248, 255))

    return img

def create_agent_icon(size):
    w, h = size
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    scale = w / 256.0

    # Outer Hexagonal / Rounded Chip Body
    pad = int(24 * scale)
    draw.rounded_rectangle([pad, pad, w - pad, h - pad], radius=int(32 * scale), fill=(15, 23, 42, 255), outline=(59, 130, 246, 255), width=max(1, int(6 * scale)))

    # Inner Chip Core
    core_pad = int(50 * scale)
    draw.rounded_rectangle([core_pad, core_pad, w - core_pad, h - core_pad], radius=int(18 * scale), fill=(30, 41, 59, 255), outline=(16, 185, 129, 255), width=max(1, int(4 * scale)))

    # External Connection Pins
    pin_len = int(14 * scale)
    pin_w = max(1, int(5 * scale))
    for i in range(4):
        offset = int((65 + i * 40) * scale)
        # Top pins
        draw.rectangle([offset, pad - pin_len, offset + pin_w, pad], fill=(59, 130, 246, 255))
        # Bottom pins
        draw.rectangle([offset, h - pad, offset + pin_w, h - pad + pin_len], fill=(59, 130, 246, 255))
        # Left pins
        draw.rectangle([pad - pin_len, offset, pad, offset + pin_w], fill=(59, 130, 246, 255))
        # Right pins
        draw.rectangle([w - pad, offset, w - pad + pin_len, offset + pin_w], fill=(59, 130, 246, 255))

    # Center Telemetry Radar Node
    center = (w // 2, h // 2)
    cr = int(36 * scale)
    draw.ellipse([center[0] - cr, center[1] - cr, center[0] + cr, center[1] + cr], fill=(6, 78, 59, 255), outline=(16, 185, 129, 255), width=max(1, int(4 * scale)))
    
    # Inner Glowing Pulse Dot
    inner_r = int(16 * scale)
    draw.ellipse([center[0] - inner_r, center[1] - inner_r, center[0] + inner_r, center[1] + inner_r], fill=(52, 211, 153, 255), outline=(255, 255, 255, 255), width=max(1, int(2 * scale)))

    # Radio Signal Waves
    wave_r = int(55 * scale)
    draw.arc([center[0] - wave_r, center[1] - wave_r, center[0] + wave_r, center[1] + wave_r], start=210, end=330, fill=(56, 189, 248, 255), width=max(1, int(3 * scale)))
    draw.arc([center[0] - wave_r, center[1] - wave_r, center[0] + wave_r, center[1] + wave_r], start=30, end=150, fill=(56, 189, 248, 255), width=max(1, int(3 * scale)))

    return img

def main():
    base_dir = r"C:\Users\ITSUPA\.gemini\antigravity\scratch\rac_industrial_monitor"
    
    # 1. Generate Monitor Icons
    monitor_imgs = [create_monitor_icon(s) for s in SIZES]
    monitor_ico_path = os.path.join(base_dir, "plantops_monitor.ico")
    monitor_imgs[0].save(monitor_ico_path, format="ICO", sizes=SIZES)
    print(f"Generated {monitor_ico_path}")

    # 2. Generate Agent Icons
    agent_imgs = [create_agent_icon(s) for s in SIZES]
    agent_ico_path = os.path.join(base_dir, "linenode_agent.ico")
    agent_imgs[0].save(agent_ico_path, format="ICO", sizes=SIZES)
    print(f"Generated {agent_ico_path}")

    # Copy to subdirectories
    for d in ["line_node_agent", "plantops_monitor", "installers", "WINDOWS_INSTALLERS", r"DEPLOY_PACKAGES\LINE_PC_AGENT", r"DEPLOY_PACKAGES\MONITOR_DASHBOARD"]:
        target_dir = os.path.join(base_dir, d)
        if os.path.isdir(target_dir):
            monitor_imgs[0].save(os.path.join(target_dir, "plantops_monitor.ico"), format="ICO", sizes=SIZES)
            agent_imgs[0].save(os.path.join(target_dir, "linenode_agent.ico"), format="ICO", sizes=SIZES)
            print(f"Copied icons to {target_dir}")

if __name__ == "__main__":
    main()
