import socket
import threading
import cv2
import time
import sys
import os
import uuid


class CameraCapture:
    def __init__(self, image_folder, camera_index=0, frame_width=1280, frame_height=720):
        self.image_folder = image_folder
        self.camera_index = camera_index
        self.frame_width = frame_width
        self.frame_height = frame_height
        self.capturing = False
        self.interval_ms = 5000
        self.running = True
        self.cap = None
        self.client_conn = None
        self.conn_lock = threading.Lock()
        self.sequence = 0
        self.boot_monotonic = time.monotonic()
        self.session_id = uuid.uuid4().hex[:8]

    def start_camera(self):
        self.cap = cv2.VideoCapture(self.camera_index)
        self.cap.set(cv2.CAP_PROP_FRAME_WIDTH, self.frame_width)
        self.cap.set(cv2.CAP_PROP_FRAME_HEIGHT, self.frame_height)

    def capture_loop(self):
        os.makedirs(self.image_folder, exist_ok=True)
        self.start_camera()

        while self.running:
            if self.capturing:
                # Flush camera buffer
                for _ in range(3):
                    self.cap.read()

                ret, img = self.cap.read()
                if ret:
                    self.sequence += 1
                    uptime = time.monotonic() - self.boot_monotonic
                    filename = f"{self.session_id}_{self.sequence:06d}.jpg"
                    filepath = os.path.join(self.image_folder, filename)
                    cv2.imwrite(filepath, img)
                    print(f"Captured: {filename}", flush=True)
                    self._notify_captured(filename, self.sequence, uptime)

                time.sleep(self.interval_ms / 1000.0)
            else:
                time.sleep(0.1)

        if self.cap:
            self.cap.release()

    def _notify_captured(self, filename, sequence, uptime_seconds):
        with self.conn_lock:
            if self.client_conn is not None:
                try:
                    msg = f"CAPTURED|{filename}|{sequence}|{uptime_seconds:.3f}\n"
                    self.client_conn.send(msg.encode("utf-8"))
                except Exception as e:
                    print(f"Notify error: {e}", flush=True)

    def handle_client(self, conn):
        with self.conn_lock:
            self.client_conn = conn
        buf = b""
        while self.running:
            try:
                data = conn.recv(1024)
                if not data:
                    break

                buf += data
                while b"\n" in buf:
                    line, buf = buf.split(b"\n", 1)
                    command = line.decode("utf-8").strip()
                    if not command:
                        continue

                    parts = command.split()
                    cmd = parts[0].upper()

                    if cmd == "START":
                        self.capturing = True
                        conn.send(b"OK\n")
                        print("Capture started", flush=True)
                    elif cmd == "STOP":
                        self.capturing = False
                        conn.send(b"OK\n")
                        print("Capture stopped", flush=True)
                    elif cmd == "INTERVAL" and len(parts) > 1:
                        self.interval_ms = int(parts[1])
                        conn.send(b"OK\n")
                        print(f"Interval set to {self.interval_ms}ms", flush=True)
                    elif cmd == "QUIT":
                        self.running = False
                        conn.send(b"OK\n")
                        break
                    else:
                        conn.send(b"UNKNOWN\n")
            except Exception as e:
                print(f"Client error: {e}", flush=True)
                break
        with self.conn_lock:
            self.client_conn = None
        conn.close()


def main():
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 9998
    image_folder = sys.argv[2] if len(sys.argv) > 2 else "/tmp/botimages"
    camera_index = int(sys.argv[3]) if len(sys.argv) > 3 else 0
    frame_width = int(sys.argv[4]) if len(sys.argv) > 4 else 1280
    frame_height = int(sys.argv[5]) if len(sys.argv) > 5 else 720

    camera = CameraCapture(image_folder, camera_index, frame_width, frame_height)

    capture_thread = threading.Thread(target=camera.capture_loop, daemon=True)
    capture_thread.start()

    server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    server.bind(("127.0.0.1", port))
    server.listen(1)
    print(f"Camera server listening on port {port}", flush=True)

    while camera.running:
        try:
            server.settimeout(1.0)
            conn, addr = server.accept()
            print(f"Client connected: {addr}", flush=True)
            camera.handle_client(conn)
        except socket.timeout:
            continue
        except Exception as e:
            print(f"Server error: {e}", flush=True)
            break

    server.close()
    camera.running = False
    capture_thread.join(timeout=5)
    print("Camera server shut down", flush=True)


if __name__ == "__main__":
    main()
