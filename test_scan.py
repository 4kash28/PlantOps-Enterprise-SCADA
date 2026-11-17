import socket
import time
import urllib.request
import json
from concurrent.futures import ThreadPoolExecutor, as_completed

def get_local_ip():
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
    return "127.0.0.1"

def probe_ip(ip, port):
    try:
        # Fast socket check first
        s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        s.settimeout(0.3)
        res = s.connect_ex((ip, port))
        s.close()
        if res == 0:
            # Confirm it is PlantOps
            url = f"http://{ip}:{port}/api/network-interfaces"
            req = urllib.request.Request(url, headers={"User-Agent": "PlantOpsDiscovery/2.4"})
            with urllib.request.urlopen(req, timeout=0.8) as resp:
                data = json.loads(resp.read().decode("utf-8"))
                if "server_port" in data or "primary_ip" in data:
                    return ip
    except Exception:
        pass
    return None

def test_discovery():
    local_ip = get_local_ip()
    print(f"Local IP: {local_ip}")
    parts = local_ip.split(".")
    if len(parts) != 4 or local_ip.startswith("127."):
        print("Invalid local IP")
        return
    subnet = ".".join(parts[:3])
    print(f"Scanning subnet: {subnet}.1 - 254 on port 8090...")
    
    start = time.perf_counter()
    found = None
    with ThreadPoolExecutor(max_workers=60) as executor:
        futures = {executor.submit(probe_ip, f"{subnet}.{h}", 8090): h for h in range(1, 255)}
        for future in as_completed(futures):
            res = future.result()
            if res:
                found = res
                print(f"[FOUND!] Server discovered at: {found}")
                break
    elapsed = time.perf_counter() - start
    print(f"Scan finished in {elapsed:.2f} seconds. Result: {found}")

test_discovery()
