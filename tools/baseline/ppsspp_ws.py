"""Minimal stdlib WebSocket client for the PPSSPP debugger API (no third-party deps).

PPSSPP serves the debugger at ws://127.0.0.1:<port>/debugger. The port is 8080 when free,
otherwise an ephemeral one; find_port() locates it from the process listening sockets.
Each request is a JSON object {"event": name, ...}; replies carry the same "event" name.
"""
import base64, json, os, socket, struct, subprocess, re


def find_port(pid=None):
    out = subprocess.run(["lsof", "-nP", "-iTCP", "-sTCP:LISTEN", "-a", "-c", "PPSSPPSDL"],
                         capture_output=True, text=True).stdout
    for line in out.splitlines()[1:]:
        m = re.search(r":(\d+) \(LISTEN\)", line)
        if m and (pid is None or str(pid) in line.split()[1:2]):
            return int(m.group(1))
    return None


class Debugger:
    def __init__(self, port, host="127.0.0.1", timeout=10):
        self.s = socket.create_connection((host, port), timeout=timeout)
        key = base64.b64encode(os.urandom(16)).decode()
        self.s.sendall((f"GET /debugger HTTP/1.1\r\nHost: {host}:{port}\r\nUpgrade: websocket\r\n"
                        f"Connection: Upgrade\r\nSec-WebSocket-Key: {key}\r\n"
                        f"Sec-WebSocket-Version: 13\r\n\r\n").encode())
        buf = b""
        while b"\r\n\r\n" not in buf:
            buf += self.s.recv(1)
        if b" 101 " not in buf.split(b"\r\n")[0]:
            raise RuntimeError("websocket upgrade failed: " + buf.decode(errors="replace"))
        self.rbuf = b""

    def _read(self, n):
        while len(self.rbuf) < n:
            c = self.s.recv(65536)
            if not c:
                raise EOFError("socket closed")
            self.rbuf += c
        d, self.rbuf = self.rbuf[:n], self.rbuf[n:]
        return d

    def _send(self, text):
        data = text.encode()
        hdr = bytearray([0x81])
        n = len(data)
        if n < 126: hdr.append(0x80 | n)
        elif n < 65536: hdr += bytes([0x80 | 126]) + struct.pack(">H", n)
        else: hdr += bytes([0x80 | 127]) + struct.pack(">Q", n)
        mask = os.urandom(4)
        self.s.sendall(bytes(hdr) + mask + bytes(b ^ mask[i % 4] for i, b in enumerate(data)))

    def _recv(self):
        msg = b""
        while True:
            b0, b1 = self._read(2)
            n = b1 & 0x7F
            if n == 126: n = struct.unpack(">H", self._read(2))[0]
            elif n == 127: n = struct.unpack(">Q", self._read(8))[0]
            payload = self._read(n)
            op = b0 & 0x0F
            if op == 8: raise EOFError("close frame")
            if op == 9: continue
            msg += payload
            if b0 & 0x80: return msg.decode()

    def call(self, event, **kw):
        """Send a request and return the first reply with the same event name."""
        self._send(json.dumps({"event": event, **kw}))
        while True:
            r = json.loads(self._recv())
            if r.get("event") == event or r.get("event") == "error":
                return r

    def read_mem(self, addr, size):
        r = self.call("memory.read", address=addr, size=size)
        if "base64" not in r:
            raise RuntimeError(f"memory.read {addr:#x}: {r}")
        return base64.b64decode(r["base64"])

    def u16(self, addr): return struct.unpack("<H", self.read_mem(addr, 2))[0]
    def i16(self, addr): return struct.unpack("<h", self.read_mem(addr, 2))[0]
    def u32(self, addr): return struct.unpack("<I", self.read_mem(addr, 4))[0]

    def press(self, button, duration=2):
        """Press a PSP button for `duration` frames (cross, circle, start, up, ...)."""
        return self.call("input.buttons.press", button=button, duration=duration)
