const startBtn = document.getElementById("startBtn");
const stopBtn = document.getElementById("stopBtn");
const preview = document.getElementById("preview");
const logEl = document.getElementById("log");

let mediaRecorder;
let chunkIndex = 0;

function log(msg) {
    logEl.textContent += msg + "\n";
}

startBtn.onclick = async () => {
    // Ask for camera + mic
    const stream = await navigator.mediaDevices.getUserMedia({
        video: true,
        audio: true
    });

    preview.srcObject = stream;

    // Create a MediaRecorder (WebM format)
    mediaRecorder = new MediaRecorder(stream, {
        mimeType: "video/webm; codecs=vp8,opus"
    });


    const streamId = "demo"; // any id you like

    mediaRecorder.ondataavailable = async (event) => {
        if (event.data && event.data.size > 0) {
            chunkIndex++;

            const url = `/ingest/${streamId}`;
            const res = await fetch(url, {
                method: "POST",
                headers: { "Content-Type": "application/octet-stream" },
                body: event.data
            });

            const info = await res.json();
            console.log("Uploaded:", info);
        }
    };

    // Start recording and emit chunk every 2s
    mediaRecorder.start(500);



    startBtn.disabled = true;
    stopBtn.disabled = false;
    log("Recording started...");
};

stopBtn.onclick = () => {
    mediaRecorder.stop();
    stopBtn.disabled = true;
    startBtn.disabled = false;
    log("Recording stopped.");
};
