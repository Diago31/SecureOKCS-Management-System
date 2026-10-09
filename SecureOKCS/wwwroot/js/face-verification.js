import {
    Human
} from "https://cdn.jsdelivr.net/npm/@vladmandic/human@3.3.6/dist/human.esm.js";

const DESCRIPTOR_LENGTH = 1024;
const MIN_CONFIDENCE = 0.65;
const MIN_LIVENESS = 0.65;
const REQUIRED_VALID_FRAMES = 3;

const human = new Human({
    backend: "webgl",

    modelBasePath:
        "https://cdn.jsdelivr.net/npm/@vladmandic/human@3.3.6/models/",

    cacheSensitivity: 0.01,

    filter: {
        enabled: true,
        equalization: true,
        flip: false
    },

    face: {
        enabled: true,

        detector: {
            rotation: true,
            maxDetected: 2,
            minConfidence: MIN_CONFIDENCE
        },

        mesh: {
            enabled: true
        },

        iris: {
            enabled: true
        },

        description: {
            enabled: true
        },

        emotion: {
            enabled: false
        },

        antispoof: {
            enabled: true
        },

        liveness: {
            enabled: true
        }
    },

    body: {
        enabled: false
    },

    hand: {
        enabled: false
    },

    object: {
        enabled: false
    },

    segmentation: {
        enabled: false
    },

    gesture: {
        enabled: true
    }
});

const elements = {
    form:
        document.getElementById(
            "faceVerificationForm"),

    video:
        document.getElementById(
            "verificationVideo"),

    canvas:
        document.getElementById(
            "verificationCanvas"),

    loading:
        document.getElementById(
            "verificationLoading"),

    loadingMessage:
        document.getElementById(
            "verificationLoadingMessage"),

    message:
        document.getElementById(
            "verificationMessage"),

    status:
        document.getElementById(
            "verificationStatus"),

    progress:
        document.getElementById(
            "verificationProgress"),

    progressText:
        document.getElementById(
            "verificationProgressText"),

    startButton:
        document.getElementById(
            "startVerificationButton"),

    retryButton:
        document.getElementById(
            "retryVerificationButton"),

    verifyButton:
        document.getElementById(
            "verifyFaceButton"),

    descriptor:
        document.getElementById(
            "DescriptorJson"),

    liveness:
        document.getElementById(
            "LivenessScore")
};

const checks = {
    faceCount: {
        id: "verifyFaceCount",
        number: "1"
    },

    position: {
        id: "verifyPosition",
        number: "2"
    },

    quality: {
        id: "verifyQuality",
        number: "3"
    },

    blink: {
        id: "verifyBlink",
        number: "4"
    },

    liveness: {
        id: "verifyLiveness",
        number: "5"
    }
};

let stream = null;
let modelsLoaded = false;
let scanning = false;
let detectionBusy = false;

let eyesPreviouslyOpen = false;
let blinkStartedAt = 0;
let blinkDetected = false;

let validFrameCount = 0;
let bestDescriptor = null;
let bestDescriptorScore = 0;
let bestLivenessScore = 0;

function setStatus(text, type) {
    elements.status.classList.remove(
        "bg-secondary",
        "bg-primary",
        "bg-success",
        "bg-danger",
        "bg-warning",
        "text-dark");

    elements.status.classList.add(
        `bg-${type}`);

    if (type === "warning") {
        elements.status.classList.add(
            "text-dark");
    }

    elements.status.textContent = text;
}

function setMessage(message) {
    elements.message.textContent =
        message;
}

function setLoading(show, message = "") {
    elements.loading.classList.toggle(
        "d-none",
        !show);

    if (message) {
        elements.loadingMessage.textContent =
            message;
    }
}

function setCheck(check, passed) {
    const item =
        document.getElementById(
            check.id);

    if (!item) {
        return;
    }

    item.classList.toggle(
        "passed",
        passed);

    const indicator =
        item.querySelector(
            ".check-indicator");

    if (indicator) {
        indicator.textContent =
            passed
                ? "✓"
                : check.number;
    }
}

function updateProgress(results) {
    const passed =
        results.filter(
            result => result).length;

    const percentage =
        Math.round(
            (passed / results.length) *
            100);

    elements.progress.style.width =
        `${percentage}%`;

    elements.progress.setAttribute(
        "aria-valuenow",
        percentage.toString());

    elements.progressText.textContent =
        `${percentage}%`;

    elements.progress.classList.toggle(
        "bg-success",
        percentage === 100);

    elements.progress.classList.toggle(
        "bg-info",
        percentage !== 100);
}

function clearCanvas() {
    const context =
        elements.canvas.getContext("2d");

    if (!context) {
        return;
    }

    context.clearRect(
        0,
        0,
        elements.canvas.width,
        elements.canvas.height);
}

function strokeFaceBox(
    context,
    face,
    passed) {
    if (!face ||
        !Array.isArray(face.box)) {
        return;
    }

    const [
        x,
        y,
        width,
        height
    ] = face.box;

    context.save();

    context.strokeStyle =
        passed
            ? "#22c55e"
            : "#38bdf8";

    context.lineWidth = 5;

    context.shadowColor =
        passed
            ? "#22c55e"
            : "#38bdf8";

    context.shadowBlur = 12;

    context.strokeRect(
        x,
        y,
        width,
        height);

    context.restore();
}

function drawFaceBox(face, passed) {
    const context =
        elements.canvas.getContext("2d");

    if (!context) {
        return;
    }

    context.clearRect(
        0,
        0,
        elements.canvas.width,
        elements.canvas.height);

    strokeFaceBox(
        context,
        face,
        passed);
}

function drawCapturedFrame(face) {
    const context =
        elements.canvas.getContext("2d");

    if (!context) {
        return;
    }

    context.clearRect(
        0,
        0,
        elements.canvas.width,
        elements.canvas.height);

    context.drawImage(
        elements.video,
        0,
        0,
        elements.canvas.width,
        elements.canvas.height);

    strokeFaceBox(
        context,
        face,
        true);
}

function stopCamera(keepCanvas = false) {
    scanning = false;

    if (stream) {
        for (const track of
            stream.getTracks()) {
            track.stop();
        }

        stream = null;
    }

    elements.video.pause();
    elements.video.srcObject = null;

    if (!keepCanvas) {
        clearCanvas();
    }
}

function resetVerification() {
    eyesPreviouslyOpen = false;
    blinkStartedAt = 0;
    blinkDetected = false;

    validFrameCount = 0;
    bestDescriptor = null;
    bestDescriptorScore = 0;
    bestLivenessScore = 0;

    elements.descriptor.value = "";
    elements.liveness.value = "0";

    elements.verifyButton.disabled =
        true;

    setCheck(checks.faceCount, false);
    setCheck(checks.position, false);
    setCheck(checks.quality, false);
    setCheck(checks.blink, false);
    setCheck(checks.liveness, false);

    updateProgress([
        false,
        false,
        false,
        false,
        false
    ]);

    clearCanvas();
}

async function loadModels() {
    if (modelsLoaded) {
        return;
    }

    setLoading(
        true,
        "Loading secure face models...");

    await human.load();

    setLoading(
        true,
        "Preparing face verification...");

    await human.warmup();

    modelsLoaded = true;
}

async function openCamera() {
    if (!navigator.mediaDevices ||
        !navigator.mediaDevices.getUserMedia) {
        throw new Error(
            "Camera API is unavailable.");
    }

    stream =
        await navigator.mediaDevices
            .getUserMedia({
                audio: false,

                video: {
                    facingMode: "user",

                    width: {
                        ideal: 1280
                    },

                    height: {
                        ideal: 720
                    }
                }
            });

    const videoReady =
        new Promise((resolve, reject) => {
            elements.video.onloadedmetadata =
                () => resolve();

            elements.video.onerror =
                () => reject(
                    new Error(
                        "Camera video failed."));
        });

    elements.video.srcObject =
        stream;

    await videoReady;
    await elements.video.play();

    elements.canvas.width =
        elements.video.videoWidth;

    elements.canvas.height =
        elements.video.videoHeight;
}

function descriptorIsValid(descriptor) {
    return Array.isArray(descriptor) &&
        descriptor.length ===
        DESCRIPTOR_LENGTH &&
        descriptor.every(value =>
            Number.isFinite(value));
}

function getGestures(result) {
    return Object.values(
        result.gesture ?? [])
        .map(gesture =>
            gesture.gesture)
        .filter(Boolean);
}

function detectBlink(gestures) {
    const eyesClosed =
        gestures.includes(
            "blink left eye") ||
        gestures.includes(
            "blink right eye");

    if (!eyesClosed) {
        eyesPreviouslyOpen = true;
    }

    if (eyesClosed &&
        eyesPreviouslyOpen &&
        blinkStartedAt === 0) {
        blinkStartedAt =
            performance.now();
    }

    if (!eyesClosed &&
        blinkStartedAt > 0) {
        const duration =
            performance.now() -
            blinkStartedAt;

        if (duration >= 20 &&
            duration <= 1000) {
            blinkDetected = true;
        }

        blinkStartedAt = 0;
    }

    return blinkDetected;
}

function getPosition(face) {
    if (!face ||
        !Array.isArray(face.box) ||
        elements.video.videoWidth <= 0 ||
        elements.video.videoHeight <= 0) {
        return {
            centred: false,
            correctSize: false
        };
    }

    const [
        x,
        y,
        width,
        height
    ] = face.box;

    const centreX =
        (x + width / 2) /
        elements.video.videoWidth;

    const centreY =
        (y + height / 2) /
        elements.video.videoHeight;

    const faceSize =
        Math.min(
            width /
            elements.video.videoWidth,
            height /
            elements.video.videoHeight);

    return {
        centred:
            Math.abs(centreX - 0.5) <
            0.18 &&
            Math.abs(centreY - 0.48) <
            0.22,

        correctSize:
            faceSize >= 0.25 &&
            faceSize <= 0.75
    };
}

function updateInstruction(
    faceCountPassed,
    positionPassed,
    qualityPassed,
    blinkPassed,
    livenessPassed) {
    if (!faceCountPassed) {
        setMessage(
            "Make sure only one face is visible.");
    } else if (!positionPassed) {
        setMessage(
            "Centre your face inside the guide.");
    } else if (!qualityPassed) {
        setMessage(
            "Move closer and improve the lighting.");
    } else if (!blinkPassed) {
        setMessage(
            "Look at the camera and blink naturally.");
    } else if (!livenessPassed) {
        setMessage(
            "Hold still while liveness is checked.");
    } else {
        setMessage(
            "Identity scan ready. Hold still...");
    }
}

function processResult(result) {
    if (!scanning) {
        return;
    }

    const faces =
        result.face ?? [];

    const faceCountPassed =
        faces.length === 1;

    setCheck(
        checks.faceCount,
        faceCountPassed);

    if (!faceCountPassed) {
        setCheck(checks.position, false);
        setCheck(checks.quality, false);
        setCheck(checks.blink, false);
        setCheck(checks.liveness, false);

        blinkDetected = false;
        blinkStartedAt = 0;
        eyesPreviouslyOpen = false;
        validFrameCount = 0;

        updateProgress([
            false,
            false,
            false,
            false,
            false
        ]);

        drawFaceBox(null, false);

        updateInstruction(
            false,
            false,
            false,
            false,
            false);

        return;
    }

    const face = faces[0];

    const gestures =
        getGestures(result);

    const position =
        getPosition(face);

    const rotation =
        face.rotation?.angle;

    const rotationCentred =
        rotation &&
        Math.abs(
            rotation.yaw ?? 0) < 0.30 &&
        Math.abs(
            rotation.pitch ?? 0) < 0.30;

    const facingCentre =
        gestures.includes(
            "facing center") ||
        rotationCentred;

    const positionPassed =
        position.centred &&
        facingCentre;

    const confidence =
        face.faceScore ??
        face.boxScore ??
        face.score ??
        0;

    const validDescriptor =
        descriptorIsValid(
            face.embedding);

    const qualityPassed =
        confidence >=
        MIN_CONFIDENCE &&
        position.correctSize &&
        validDescriptor;

    const blinkPassed =
        detectBlink(gestures);

    const antiSpoofScore =
        face.real ?? 0;

    const liveScore =
        face.live ?? 0;

    const livenessPassed =
        antiSpoofScore >=
        MIN_LIVENESS &&
        liveScore >=
        MIN_LIVENESS;

    setCheck(
        checks.position,
        positionPassed);

    setCheck(
        checks.quality,
        qualityPassed);

    setCheck(
        checks.blink,
        blinkPassed);

    setCheck(
        checks.liveness,
        livenessPassed);

    const results = [
        faceCountPassed,
        positionPassed,
        qualityPassed,
        blinkPassed,
        livenessPassed
    ];

    updateProgress(results);

    updateInstruction(
        faceCountPassed,
        positionPassed,
        qualityPassed,
        blinkPassed,
        livenessPassed);

    const currentFrameValid =
        results.every(Boolean) &&
        validDescriptor;

    drawFaceBox(
        face,
        currentFrameValid);

    if (!currentFrameValid) {
        validFrameCount = 0;
        return;
    }

    validFrameCount++;

    const descriptorScore =
        confidence +
        antiSpoofScore +
        liveScore;

    if (descriptorScore >
        bestDescriptorScore) {
        bestDescriptorScore =
            descriptorScore;

        bestDescriptor =
            [...face.embedding];

        bestLivenessScore =
            Math.min(
                antiSpoofScore,
                liveScore);
    }

    if (validFrameCount >=
        REQUIRED_VALID_FRAMES) {
        completeVerification(face);
    }
}

function completeVerification(face) {
    if (!bestDescriptor ||
        !descriptorIsValid(
            bestDescriptor)) {
        handleError(
            new Error(
                "No valid face descriptor."));
        return;
    }

    elements.descriptor.value =
        JSON.stringify(
            bestDescriptor);

    elements.liveness.value =
        bestLivenessScore.toFixed(4);

    drawCapturedFrame(face);
    stopCamera(true);

    setLoading(false);

    setStatus(
        "Ready",
        "success");

    setMessage(
        "Face captured. Press Verify and Login.");

    elements.startButton.hidden = true;
    elements.retryButton.hidden = false;
    elements.verifyButton.disabled = false;
}

function cameraErrorMessage(error) {
    if (error?.name ===
        "NotAllowedError") {
        return "Camera permission was denied.";
    }

    if (error?.name ===
        "NotFoundError") {
        return "No camera was detected.";
    }

    if (error?.name ===
        "NotReadableError") {
        return "Camera is being used by " +
            "another application.";
    }

    return "Verification could not start. " +
        "Check the camera and internet.";
}

function handleError(error) {
    console.error(
        "Face verification error:",
        error);

    stopCamera(false);
    setLoading(false);

    setStatus(
        "Failed",
        "danger");

    setMessage(
        cameraErrorMessage(error));

    elements.startButton.disabled = false;
    elements.startButton.hidden = false;
    elements.retryButton.hidden = true;
    elements.verifyButton.disabled = true;
}

async function detectionLoop() {
    if (!scanning) {
        return;
    }

    if (detectionBusy) {
        requestAnimationFrame(
            detectionLoop);
        return;
    }

    detectionBusy = true;

    try {
        const result =
            await human.detect(
                elements.video);

        processResult(result);
    } catch (error) {
        handleError(error);
        return;
    } finally {
        detectionBusy = false;
    }

    if (scanning) {
        requestAnimationFrame(
            detectionLoop);
    }
}

async function startVerification() {
    stopCamera(false);
    resetVerification();

    elements.startButton.disabled = true;
    elements.retryButton.hidden = true;

    setStatus(
        "Preparing",
        "warning");

    setMessage(
        "Preparing secure verification...");

    try {
        setLoading(
            true,
            "Requesting camera access...");

        await openCamera();

        await loadModels();

        setLoading(false);

        scanning = true;

        setStatus(
            "Scanning",
            "primary");

        setMessage(
            "Centre your face and blink naturally.");

        detectionLoop();
    } catch (error) {
        handleError(error);
    }
}

elements.startButton.addEventListener(
    "click",
    startVerification);

elements.retryButton.addEventListener(
    "click",
    startVerification);

elements.form.addEventListener(
    "submit",
    event => {
        if (!elements.descriptor.value) {
            event.preventDefault();

            setStatus(
                "Scan Required",
                "danger");

            setMessage(
                "Complete the face scan first.");

            return;
        }

        elements.verifyButton.disabled = true;

        elements.verifyButton.textContent =
            "Verifying Identity...";
    });

window.addEventListener(
    "beforeunload",
    () => stopCamera(false));