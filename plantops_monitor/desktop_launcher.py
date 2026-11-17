"""
PlantOps Monitor - Desktop Application Launcher
Spawns the local FastAPI SCADA server and launches a 100% native Windows desktop application window
powered by Microsoft Edge WebView2 Control (WinForms/WPF) with Windows System Tray integration.
"""

import sys
import os
import time
import socket
import logging
import threading
import subprocess
import shutil
import webbrowser
from pathlib import Path

# Safe NullWriter for Windows windowed execution (where sys.stdout / sys.stderr are None)
class SafeNullWriter:
    def write(self, s): pass
    def flush(self): pass
    def isatty(self): return False

if sys.stdout is None:
    sys.stdout = SafeNullWriter()
if sys.stderr is None:
    sys.stderr = SafeNullWriter()

# Application Directories
if getattr(sys, 'frozen', False):
    APP_DIR = Path(sys.executable).parent.resolve()
    BUNDLE_DIR = Path(getattr(sys, '_MEIPASS', APP_DIR))
else:
    APP_DIR = Path(__file__).parent.resolve()
    BUNDLE_DIR = APP_DIR

if str(APP_DIR) not in sys.path:
    sys.path.insert(0, str(APP_DIR))

# File and Console Logging
log_file = APP_DIR / "plantops_monitor.log"
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] [PlantOpsLauncher] %(message)s",
    handlers=[
        logging.FileHandler(str(log_file), encoding="utf-8", mode="a"),
        logging.StreamHandler(sys.stdout)
    ]
)
logger = logging.getLogger("PlantOpsLauncher")

import uvicorn
from server import app, SERVER_CONFIG

# System Tray Imports
try:
    import pystray
    from PIL import Image, ImageDraw
    HAS_TRAY = True
except ImportError:
    HAS_TRAY = False


def get_resource_path(relative_path: str) -> str:
    """Handles PyInstaller bundled extraction directory."""
    if hasattr(sys, '_MEIPASS'):
        return os.path.join(sys._MEIPASS, relative_path)
    return os.path.join(os.path.dirname(os.path.abspath(__file__)), relative_path)


def get_lan_ip() -> str:
    """Discovers LAN IP address for local network access."""
    try:
        import psutil
        for iface, addrs in psutil.net_if_addrs().items():
            for a in addrs:
                if a.family == socket.AF_INET:
                    ip = a.address
                    if not ip.startswith('127.') and not ip.startswith('169.254.'):
                        return ip
    except Exception:
        pass
    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        s.connect(("8.8.8.8", 80))
        ip = s.getsockname()[0]
        s.close()
        return ip
    except Exception:
        return "127.0.0.1"


def is_server_running(port: int) -> bool:
    """Checks if the local server is already running and accepting HTTP connections."""
    try:
        with socket.create_connection(("127.0.0.1", port), timeout=0.5):
            return True
    except Exception:
        return False


def run_uvicorn_thread(host: str, port: int):
    """Runs Uvicorn in background daemon thread with exception logging."""
    logger.info(f"Starting internal SCADA HTTP/WebSocket server on {host}:{port}...")
    try:
        config = uvicorn.Config(app=app, host=host, port=port, log_config=None, log_level="warning")
        server = uvicorn.Server(config)
        server.run()
    except Exception as e:
        logger.exception(f"Uvicorn server encountered an exception: {e}")


def wait_for_server(port: int, timeout: float = 25.0) -> bool:
    """Waits until the HTTP server is accepting connections."""
    start = time.time()
    while time.time() - start < timeout:
        try:
            with socket.create_connection(("127.0.0.1", port), timeout=0.3):
                return True
        except (ConnectionRefusedError, OSError):
            time.sleep(0.2)
    return False


def find_browser_executable() -> str:
    """Finds Microsoft Edge or Chrome executable for standalone app window mode fallback."""
    candidates = [
        r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
        shutil.which("msedge"),
        r"C:\Program Files\Google\Chrome\Application\chrome.exe",
        r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
        shutil.which("chrome"),
    ]
    for c in candidates:
        if c and os.path.isfile(c):
            return c
    return ""


def launch_scada_window_fallback(url: str):
    """Fallback launcher using standalone app mode."""
    browser_exe = find_browser_executable()
    if browser_exe:
        logger.info(f"Launching SCADA Dedicated Window fallback via {browser_exe}...")
        try:
            cmd = [browser_exe, f"--app={url}", "--start-maximized"]
            subprocess.Popen(cmd)
            return True
        except Exception as e:
            logger.warning(f"Failed to launch fallback app window: {e}")
    webbrowser.open(url)
    return True


def launch_native_scada_window(url: str, title: str = "PlantOps™ Enterprise SCADA - Industrial Workstation v2.4"):
    """
    Launches a 100% native Windows desktop application window using embedded WebView2.
    It is NOT an external browser; it runs inside the native PlantOpsMonitor.exe process.
    """
    try:
        import webview
        logger.info("Initializing Native Windows Application Shell via pywebview (WebView2)...")
        
        ico_candidates = [
            get_resource_path("plantops_monitor.ico"),
            APP_DIR / "plantops_monitor.ico",
            BUNDLE_DIR / "plantops_monitor.ico"
        ]
        ico_path = None
        for c in ico_candidates:
            if c and os.path.isfile(str(c)):
                ico_path = str(c)
                break

        window = webview.create_window(
            title=title,
            url=url,
            width=1440,
            height=900,
            min_size=(1024, 600),
            confirm_close=False,
            text_select=False,
            zoomable=False
        )
        
        logger.info("Starting native Windows message loop...")
        webview.start(icon=ico_path, debug=False)
        return True
    except Exception as e:
        logger.warning(f"Native pywebview shell encountered exception: {e}. Falling back to browser app mode.")
        return launch_scada_window_fallback(url)


class PlantOpsTrayDaemon:
    """Manages the Windows System Tray icon for the PlantOps SCADA Server."""

    def __init__(self, port: int, dashboard_url: str):
        self.port = port
        self.dashboard_url = dashboard_url
        self.tray_icon = None
        self.running = True

    def get_icon_image(self):
        candidates = [
            APP_DIR / "plantops_monitor.ico",
            BUNDLE_DIR / "plantops_monitor.ico",
            APP_DIR / "static" / "favicon.ico",
            BUNDLE_DIR / "static" / "favicon.ico"
        ]
        for c in candidates:
            if c.is_file():
                try:
                    return Image.open(c).resize((64, 64))
                except Exception:
                    pass

        img = Image.new('RGBA', (64, 64), (15, 23, 42, 255))
        draw = ImageDraw.Draw(img)
        draw.ellipse((4, 4, 60, 60), fill=(16, 185, 129, 255), outline=(56, 189, 248, 255), width=3)
        return img

    def on_open_window(self, icon=None, item=None):
        launch_native_scada_window(self.dashboard_url)

    def on_open_browser(self, icon=None, item=None):
        webbrowser.open(self.dashboard_url)

    def on_export_csv(self, icon=None, item=None):
        webbrowser.open(f"{self.dashboard_url}/api/downtime-logs/export-csv")

    def on_view_log(self, icon=None, item=None):
        if log_file.is_file():
            startupinfo = None
            if sys.platform == "win32":
                startupinfo = subprocess.STARTUPINFO()
            subprocess.Popen(["notepad.exe", str(log_file)], startupinfo=startupinfo)

    def on_exit(self, icon=None, item=None):
        logger.info("User requested exit via System Tray.")
        self.running = False
        if self.tray_icon:
            self.tray_icon.stop()
        os._exit(0)

    def run(self):
        if not HAS_TRAY:
            return

        menu = pystray.Menu(
            pystray.MenuItem("PlantOps Enterprise SCADA", None, enabled=False),
            pystray.Menu.SEPARATOR,
            pystray.MenuItem("🖥️ Open SCADA Workstation", self.on_open_window, default=True),
            pystray.MenuItem("🌐 Open in Web Browser", self.on_open_browser),
            pystray.MenuItem("📊 Export Stoppage Audit CSV", self.on_export_csv),
            pystray.MenuItem("📄 View Server Log", self.on_view_log),
            pystray.Menu.SEPARATOR,
            pystray.MenuItem("🛑 Exit PlantOps Monitor", self.on_exit)
        )

        self.tray_icon = pystray.Icon(
            name="PlantOpsMonitor",
            icon=self.get_icon_image(),
            title=f"PlantOps SCADA Workstation (Port {self.port})",
            menu=menu
        )
        self.tray_icon.run()


def main():
    import argparse
    parser = argparse.ArgumentParser(description="PlantOps Monitor - Desktop Workstation Application")
    parser.add_argument("--port", type=int, default=8090, help="Port to listen on (default: 8090)")
    parser.add_argument("--host", default="0.0.0.0", help="Binding IP (default: 0.0.0.0 for LAN)")
    parser.add_argument("--no-window", action="store_true", help="Run in headless server-only mode")
    args = parser.parse_args()

    port = args.port or SERVER_CONFIG.get("server", {}).get("port", 8090)
    host = args.host or SERVER_CONFIG.get("server", {}).get("host", "0.0.0.0")
    lan_ip = get_lan_ip()
    dashboard_url = f"http://127.0.0.1:{port}"

    # Set Windows AppUserModelID
    if sys.platform == "win32":
        try:
            import ctypes
            ctypes.windll.shell32.SetCurrentProcessExplicitAppUserModelID("PlantOps.EnterpriseSCADA.Monitor.v24")
        except Exception:
            pass

    # SINGLE INSTANCE HANDLING: If server is already running, just bring up the native window!
    if is_server_running(port):
        logger.info(f"PlantOps SCADA server is already active on port {port}. Opening native workstation window.")
        launch_native_scada_window(dashboard_url)
        sys.exit(0)

    logger.info("=" * 72)
    logger.info("  PLANTOPS™ ENTERPRISE SCADA // RAC ASSEMBLY & PACKAGING DIVISION")
    logger.info(f"  Internal Ingestion Bus  : 127.0.0.1:{port}")
    logger.info(f"  LAN Access for Line PCs : http://{lan_ip}:{port}")
    logger.info("=" * 72)

    # Launch server in background thread
    server_thread = threading.Thread(target=run_uvicorn_thread, args=(host, port), daemon=True)
    server_thread.start()

    # Wait for server readiness (up to 25s for cold start)
    if not wait_for_server(port, timeout=25.0):
        logger.warning(f"Server check timed out on port {port}. Attempting to launch native UI anyway.")
    else:
        logger.info(f"Internal server verified active and ready on port {port}!")

    if args.no_window:
        logger.info("Running in headless server mode. Entering persistent loop.")
        try:
            while True:
                time.sleep(1)
        except KeyboardInterrupt:
            logger.info("Shutdown requested.")
            sys.exit(0)

    # Start Windows System Tray Daemon in a background daemon thread
    if HAS_TRAY:
        try:
            daemon = PlantOpsTrayDaemon(port, dashboard_url)
            tray_thread = threading.Thread(target=daemon.run, daemon=True)
            tray_thread.start()
        except Exception as e:
            logger.warning(f"System tray daemon encountered notice: {e}")

    # Launch 100% native desktop application window (runs main GUI loop)
    launch_native_scada_window(dashboard_url)

    logger.info("PlantOps SCADA native workstation closed. Exiting.")
    os._exit(0)


if __name__ == "__main__":
    main()
