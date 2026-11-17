import os
import shutil
from PIL import Image, ImageDraw

base_dir = r"C:\Users\ITSUPA\.gemini\antigravity\scratch\rac_industrial_monitor"
artifacts_dir = r"C:\Users\ITSUPA\.gemini\antigravity\brain\cfc8f59c-822b-49ae-8e56-7b0867344232"

scada_src = os.path.join(artifacts_dir, "plantops_scada_icon_1794810383660.jpg")
agent_src = os.path.join(artifacts_dir, "plantops_line_node_agent_icon_1794810391158.jpg")

sizes = [(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (24, 24), (16, 16)]

def make_squircle_icon(img_path, output_png, output_ico):
    im = Image.open(img_path).convert("RGBA")
    w, h = im.size
    
    # Create high-quality anti-aliased squircle mask
    scale = 4
    mask = Image.new('L', (w * scale, h * scale), 0)
    draw = ImageDraw.Draw(mask)
    radius = int(w * scale * 0.22) # smooth squircle corner
    draw.rounded_rectangle([0, 0, w * scale - 1, h * scale - 1], radius=radius, fill=255)
    mask = mask.resize((w, h), Image.Resampling.LANCZOS)
    
    # Apply mask
    im.putalpha(mask)
    
    # Save PNG
    im.save(output_png, "PNG")
    print(f"Saved PNG: {output_png}")
    
    # Generate multi-size ICO
    icon_images = []
    for s in sizes:
        resized = im.resize(s, Image.Resampling.LANCZOS)
        icon_images.append(resized)
    
    icon_images[0].save(output_ico, format='ICO', sizes=sizes, append_images=icon_images[1:])
    print(f"Saved ICO: {output_ico}")

# 1. Process SCADA Icon
scada_png = os.path.join(base_dir, "plantops_monitor.png")
scada_ico = os.path.join(base_dir, "plantops_monitor.ico")
make_squircle_icon(scada_src, scada_png, scada_ico)

# 2. Process Line Agent Icon
agent_png = os.path.join(base_dir, "linenode_agent.png")
agent_ico = os.path.join(base_dir, "linenode_agent.ico")
make_squircle_icon(agent_src, agent_png, agent_ico)

# Copy to destinations
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

print("All logos and icons processed successfully!")
