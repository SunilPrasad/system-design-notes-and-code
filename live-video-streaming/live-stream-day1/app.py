from pathlib import Path
from fastapi import FastAPI, Request, HTTPException
from fastapi.responses import FileResponse
from fastapi.staticfiles import StaticFiles
from fastapi import FastAPI, Request
from ffmpeg_streamer import FFmpegStreamer

BASE = Path(__file__).parent.resolve()
PUBLIC_DIR = BASE / "public"
UPLOADS_DIR = BASE / "uploads"
OUT_DIR = BASE / "out"

UPLOADS_DIR.mkdir(exist_ok=True)
OUT_DIR.mkdir(exist_ok=True)

app = FastAPI(title="Live Stream - Option A (ffmpeg-python)")

# Serve index.html + JS
app.mount("/static", StaticFiles(directory=PUBLIC_DIR), name="static")

app.mount(
    "/out",
    StaticFiles(directory="out"),   # your .ts and .m3u8 files
    name="out"
)


@app.get("/")
def index():
    return FileResponse(PUBLIC_DIR / "index.html")


# ---- HLS Playlist Helpers ---------------------------------------------------

def ensure_playlist():
    playlist = OUT_DIR / "stream.m3u8"
    if not playlist.exists():
        playlist.write_text(
            "#EXTM3U\n"
            "#EXT-X-VERSION:3\n"
            "#EXT-X-TARGETDURATION:6\n"
            "#EXT-X-MEDIA-SEQUENCE:0\n"
        )
    return playlist


def append_segment_to_playlist(ts_name: str, duration: float = 2.0):
    playlist = ensure_playlist()
    with playlist.open("a", encoding="utf-8") as f:
        f.write(f"#EXTINF:{duration:.3f},\n")
        f.write(f"{ts_name}\n")


# ---- Ingest Endpoint --------------------------------------------------------



streams = {}   # stream registry

@app.post("/ingest/{stream_id}")
async def ingest_chunk(stream_id: str, request: Request):
    body = await request.body()
    if not body:
        return {"error": "empty"}

    # Create streamer if not exists
    if stream_id not in streams:
        streams[stream_id] = FFmpegStreamer(stream_id)

    # Push chunk
    streams[stream_id].push_chunk(body)

    return {"ok": True, "bytes": len(body)}

