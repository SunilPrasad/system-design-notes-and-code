import subprocess
import threading
import queue
from pathlib import Path

OUT_DIR = Path("out")

class FFmpegStreamer:
    def __init__(self, stream_id: str):
        self.stream_id = stream_id
        self.queue = queue.Queue()
        self.running = True

        # Prepare output folder
        self.stream_dir = OUT_DIR
        self.stream_dir.mkdir(exist_ok=True, parents=True)

        # Start ffmpeg process with stdin pipe
        self.process = subprocess.Popen(
            [
                "ffmpeg",
                "-re",                  # real-time input
                "-i", "pipe:0",         # stdin input
                "-c:v", "libx264",
                "-preset", "veryfast",
                "-tune", "zerolatency",
                "-c:a", "aac",
                "-f", "hls",
                "-hls_time", "2",
                "-hls_list_size", "6",
                "-hls_flags", "delete_segments",
                str(self.stream_dir / "stream.m3u8")
            ],
            stdin=subprocess.PIPE,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )

        # Background thread to feed each chunk into FFmpeg stdin
        self.worker = threading.Thread(target=self._run_worker, daemon=True)
        self.worker.start()

    def _run_worker(self):
        while self.running:
            chunk = self.queue.get()
            if chunk is None:
                break
            try:
                self.process.stdin.write(chunk)
                self.process.stdin.flush()
            except Exception:
                break

    def push_chunk(self, data: bytes):
        self.queue.put(data)

    def stop(self):
        self.running = False
        self.queue.put(None)
        try:
            self.process.stdin.close()
        except:
            pass
        self.process.terminate()
