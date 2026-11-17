import os
import math
from PIL import Image, ImageDraw, ImageFilter

base_dir = r"C:\Users\ITSUPA\.gemini\antigravity\scratch\rac_industrial_monitor"
artifacts_dir = r"C:\Users\ITSUPA\.gemini\antigravity\brain\cfc8f59c-822b-49ae-8e56-7b0867344232"

# 4x Supersampling resolution for razor-sharp vector rendering
S = 4
CANVAS = 1024 * S # 4096 x 4096

def create_scada_logo():
    # 4096 x 4096 canvas
    im = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
    draw = ImageDraw.Draw(im)
    
    # 1. Base Squircle (Rounded Square)
    pad = 48 * S
    rad = 220 * S
    sq_box = [pad, pad, CANVAS - pad, CANVAS - pad]
    
    # Modern Deep Slate Background
    bg_color = (15, 23, 42, 255) # #0f172a
    draw.rounded_rectangle(sq_box, radius=rad, fill=bg_color)
    
    # Subtle inner border for premium depth
    border_color = (51, 65, 85, 255) # #334155
    border_w = 12 * S
    draw.rounded_rectangle(sq_box, radius=rad, outline=border_color, width=border_w)
    
    # 2. Minimalist Factory Geometry (Translucent Fill & Clean Cyan Outlines)
    # Coordinates normalized to 1024, scaled by S
    # Sawtooth industrial silhouette:
    poly_pts = [
        (260 * S, 720 * S), # Bottom Left
        (260 * S, 520 * S), # Left wall up
        (420 * S, 360 * S), # First roof peak
        (420 * S, 440 * S), # First roof drop
        (580 * S, 280 * S), # Second high roof peak
        (580 * S, 380 * S), # Second roof drop
        (760 * S, 380 * S), # Tower top horizontal
        (760 * S, 720 * S), # Bottom Right
    ]
    # Subtle azure translucent silhouette fill
    factory_fill = (2, 132, 199, 45) # 18% opacity #0284c7
    draw.polygon(poly_pts, fill=factory_fill)
    
    # Outline of factory
    outline_color = (14, 165, 233, 200) # #0ea5e9
    draw.line(poly_pts + [poly_pts[0]], fill=outline_color, width=8 * S, joint="round")
    
    # Factory baseline
    draw.line([(220 * S, 720 * S), (804 * S, 720 * S)], fill=(71, 85, 105, 255), width=8 * S)

    # 3. Precision Telemetry Pulse Wave (Vibrant & High-Contrast)
    # Starts left, cuts through center of factory, terminates right
    wave_pts = [
        (190 * S, 540 * S),
        (350 * S, 540 * S),
        (390 * S, 540 * S),
        (430 * S, 440 * S), # Small blip up
        (470 * S, 640 * S), # Blip down
        (520 * S, 290 * S), # Master high peak
        (570 * S, 680 * S), # Deep rebound valley
        (620 * S, 430 * S), # Recovery peak
        (660 * S, 540 * S), # Settle
        (834 * S, 540 * S), # Flatline right
    ]
    
    # Outer glow / shadow for pulse
    glow_color = (2, 132, 199, 120)
    draw.line(wave_pts, fill=glow_color, width=28 * S, joint="round")
    
    # Crisp white core pulse line
    pulse_color = (255, 255, 255, 255)
    draw.line(wave_pts, fill=pulse_color, width=16 * S, joint="round")
    
    # 4. Status Beacon at High Peak (520*S, 290*S)
    px, py = 520 * S, 290 * S
    # Emerald green beacon halo
    draw.ellipse([px - 44*S, py - 44*S, px + 44*S, py + 44*S], fill=(16, 185, 129, 255))
    # Pure white center dot
    draw.ellipse([px - 20*S, py - 20*S, px + 20*S, py + 20*S], fill=(255, 255, 255, 255))
    
    # Second smaller status beacon at (620*S, 430*S)
    px2, py2 = 620 * S, 430 * S
    draw.ellipse([px2 - 24*S, py2 - 24*S, px2 + 24*S, py2 + 24*S], fill=(56, 189, 248, 255))
    draw.ellipse([px2 - 10*S, py2 - 10*S, px2 + 10*S, py2 + 10*S], fill=(255, 255, 255, 255))

    # Downsample to 1024x1024 using Lanczos for clean anti-aliasing
    final = im.resize((1024, 1024), Image.Resampling.LANCZOS)
    return final

def create_agent_logo():
    # 4096 x 4096 canvas
    im = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
    draw = ImageDraw.Draw(im)
    
    # 1. Base Squircle (Rounded Square)
    pad = 48 * S
    rad = 220 * S
    sq_box = [pad, pad, CANVAS - pad, CANVAS - pad]
    
    # Modern Deep Slate Background
    bg_color = (15, 23, 42, 255) # #0f172a
    draw.rounded_rectangle(sq_box, radius=rad, fill=bg_color)
    
    # Subtle inner border for premium depth
    border_color = (51, 65, 85, 255) # #334155
    border_w = 12 * S
    draw.rounded_rectangle(sq_box, radius=rad, outline=border_color, width=border_w)
    
    cx, cy = CANVAS // 2, CANVAS // 2
    
    # 2. Minimalist Chip Body (Centered Edge Node)
    chip_w = 340 * S
    chip_box = [cx - chip_w // 2, cy - chip_w // 2, cx + chip_w // 2, cy + chip_w // 2]
    chip_rad = 54 * S
    
    # Chip body fill
    draw.rounded_rectangle(chip_box, radius=chip_rad, fill=(30, 41, 59, 255))
    # Chip outline
    draw.rounded_rectangle(chip_box, radius=chip_rad, outline=(2, 132, 199, 255), width=14 * S)
    
    # 3. External Connection Pins (3 on each side)
    pin_len = 65 * S
    pin_w = 14 * S
    offsets = [-90 * S, 0, 90 * S]
    pin_color = (56, 189, 248, 255) # #38bdf8
    
    for off in offsets:
        # Left pins
        draw.rounded_rectangle([chip_box[0] - pin_len, cy + off - pin_w // 2, chip_box[0], cy + off + pin_w // 2], radius=4*S, fill=pin_color)
        # Right pins
        draw.rounded_rectangle([chip_box[2], cy + off - pin_w // 2, chip_box[2] + pin_len, cy + off + pin_w // 2], radius=4*S, fill=pin_color)
        # Top pins
        draw.rounded_rectangle([cx + off - pin_w // 2, chip_box[1] - pin_len, cx + off + pin_w // 2, chip_box[1]], radius=4*S, fill=pin_color)
        # Bottom pins
        draw.rounded_rectangle([cx + off - pin_w // 2, chip_box[3], cx + off + pin_w // 2, chip_box[3] + pin_len], radius=4*S, fill=pin_color)
        
    # 4. Concentric Circuitry / Core Ring inside Chip
    inner_rad = 90 * S
    draw.ellipse([cx - inner_rad, cy - inner_rad, cx + inner_rad, cy + inner_rad], outline=(14, 165, 233, 180), width=8 * S)
    
    # Center Status Core (Emerald Green Active Telemetry Indicator)
    core_rad = 48 * S
    draw.ellipse([cx - core_rad, cy - core_rad, cx + core_rad, cy + core_rad], fill=(16, 185, 129, 255))
    draw.ellipse([cx - 20 * S, cy - 20 * S, cx + 20 * S, cy + 20 * S], fill=(255, 255, 255, 255))
    
    # 5. Radiating Wireless Telemetry Signal Waves (Arcs in Top-Right Corner)
    # Radiating from the chip corner toward the upper right
    arc_center = (cx + chip_w // 2 - 20*S, cy - chip_w // 2 + 20*S)
    arc_widths = 14 * S
    
    # Arc 1 (Inner green telemetry wave)
    r1 = 120 * S
    draw.arc([arc_center[0] - r1, arc_center[1] - r1, arc_center[0] + r1, arc_center[1] + r1], start=-85, end=-5, fill=(16, 185, 129, 255), width=arc_widths)
    
    # Arc 2 (Middle cyan wave)
    r2 = 190 * S
    draw.arc([arc_center[0] - r2, arc_center[1] - r2, arc_center[0] + r2, arc_center[1] + r2], start=-85, end=-5, fill=(56, 189, 248, 255), width=arc_widths)
    
    # Arc 3 (Outer azure wave)
    r3 = 260 * S
    draw.arc([arc_center[0] - r3, arc_center[1] - r3, arc_center[0] + r3, arc_center[1] + r3], start=-85, end=-5, fill=(125, 211, 252, 230), width=arc_widths)

    # Downsample to 1024x1024 using Lanczos
    final = im.resize((1024, 1024), Image.Resampling.LANCZOS)
    return final

# Generate
print("Generating Minimalist SCADA Logo...")
scada_img = create_scada_logo()
scada_png = os.path.join(base_dir, "plantops_monitor.png")
scada_ico = os.path.join(base_dir, "plantops_monitor.ico")
scada_artifact = os.path.join(artifacts_dir, "plantops_scada_minimal_logo.png")

scada_img.save(scada_png, "PNG")
scada_img.save(scada_artifact, "PNG")

# Generate ICO
sizes = [(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (24, 24), (16, 16)]
scada_icons = [scada_img.resize(s, Image.Resampling.LANCZOS) for s in sizes]
scada_icons[0].save(scada_ico, format='ICO', sizes=sizes, append_images=scada_icons[1:])
print("SCADA Minimalist Logo saved!")

print("Generating Minimalist Line Agent Logo...")
agent_img = create_agent_logo()
agent_png = os.path.join(base_dir, "linenode_agent.png")
agent_ico = os.path.join(base_dir, "linenode_agent.ico")
agent_artifact = os.path.join(artifacts_dir, "plantops_line_agent_minimal_logo.png")

agent_img.save(agent_png, "PNG")
agent_img.save(agent_artifact, "PNG")

agent_icons = [agent_img.resize(s, Image.Resampling.LANCZOS) for s in sizes]
agent_icons[0].save(agent_ico, format='ICO', sizes=sizes, append_images=agent_icons[1:])
print("Line Agent Minimalist Logo saved!")

# Copy to destinations
import shutil
dest_dirs = [
    os.path.join(base_dir, "WINDOWS_INSTALLERS"),
    os.path.join(base_dir, "DotNetApp"),
    os.path.join(base_dir, "LineNodeAgent"),
    os.path.join(base_dir, "Release_ScadaDashboard"),
    os.path.join(base_dir, "Release_LineNodeAgent")
]

for d in dest_dirs:
    if os.path.exists(d):
        shutil.copy2(scada_ico, os.path.join(d, "plantops_monitor.ico"))
        shutil.copy2(scada_png, os.path.join(d, "plantops_monitor.png"))
        shutil.copy2(agent_ico, os.path.join(d, "linenode_agent.ico"))
        shutil.copy2(agent_png, os.path.join(d, "linenode_agent.png"))
        print(f"Updated icons in: {d}")

print("Completed successfully!")
