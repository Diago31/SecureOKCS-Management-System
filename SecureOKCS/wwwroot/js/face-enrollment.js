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
            "faceEnrollmentForm"),

    video:
        document.getElementById(
            "faceVideo"),

    canvas:
        document.getElementById(
            "faceCanvas"),

    loading:
        document.getElementById(
            "cameraLoading"),

    loadingMessage:
        document.getElementById(
            "loadingMessage"),

    cameraMessage:
        document.getElementById(
            "cameraMessage"),

    scanStatus:
        document.getElementById(
            "scanStatus"),

    scanProgress:
        document.getElementById(
            "scanProgress"),

    progressText:
        document.getElementById(
            "progressText"),

    startButton:
        document.getElementById(
            "startCameraButton"),

    retryButton:
        document.getElementById(
            "retryButton"),

    saveButton:
        document.getElementById(
            "saveFaceButton"),

    descriptor:
        document.getElementById(
            "DescriptorJson"),

    liveness:
        document.getElementById(
            "LivenessScore"),

    consent:
        document.getElementById(
            "ConsentGiven")
};

const checkIds = [
    "checkFaceCount",
    "checkPosition",
    "checkQuality",
    "checkBlink",
    "checkLiveness"
];

const checkNumbers = {
    checkFaceCount: "1",
    checkPosition: "2",
    checkQuality: "3",
    checkBlink: "4",
    checkLiveness: "5"
};

let cameraStream = null;
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
    const statusClasses = [
        "bg-secondary",
        "bg-primary",
        "bg-success",
        "bg-danger",
        "bg-warning",
        "text-dark"
    ];

    elements.scanStatus.classList.remove(
        ...statusClasses);

    elements.scanStatus.classList.add(
        `bg-${type}`);

    if (type === "warning") {
        elements.scanStatus.classList.add(
            "text-dark");
    }

    elements.scanStatus.textContent = text;
}

function setMessage(message) {
    elements.cameraMessage.textContent =
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

function setCheck(id, passed) {
    const item =
        document.getElementById(id);

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
                : checkNumbers[id];
    }
}

function updateProgress(checkResults) {
    const passedCount =
        checkResults.filter(
            result => result).length;

    const percentage =
        Math.round(
            (passedCount /
                checkResults.length) *
            100);

    elements.scanProgress.style.width =
        `${percentage}%`;

    elements.scanProgress.setAttribute(
        "aria-valuenow",
        percentage.toString());

    elements.progressText.textContent =
        `${percentage}%`;

    if (percentage === 100) {
        elements.scanProgress.classList
            .remove("bg-info");

        elements.scanProgress.classList
            .add("bg-success");
    } else {
        elements.scanProgress.classList
            .remove("bg-success");

        elements.scanProgress.classList
            .add("bg-info");
    }
}

function resetEnrollmentState() {
    eyesPreviouslyOpen = false;
    blinkStartedAt = 0;
    blinkDetected = false;

    validFrameCount = 0;
    bestDescriptor = null;
    bestDescriptorScore = 0;
    bestLivenessScore = 0;

    elements.descriptor.value = "";
    elements.liveness.value = "0";

    elements.saveButton.disabled = true;

    for (const id of checkIds) {
        setCheck(id, false);
    }

    updateProgress([
        false,
        false,
        false,
        false,
        false
    ]);

    clearCanvas();
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

    drawFaceBox(face, true);
}

function stopCamera(keepCanvas = false) {
    scanning = false;

    if (cameraStream) {
        for (const track of
            cameraStream.getTracks()) {
            track.stop();
        }

        cameraStream = null;
    }

    elements.video.pause();
    elements.video.srcObject = null;

    if (!keepCanvas) {
        clearCanvas();
    }
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
        "Preparing face recognition...");

    await human.warmup();

    modelsLoaded = true;
}

async function openCamera() {
    if (!navigator.mediaDevices ||
        !navigator.mediaDevices.getUserMedia) {
        throw new Error(
            "Camera API is not supported.");
    }

    const constraints = {
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
    };

    cameraStream =
        await navigator.mediaDevices
            .getUserMedia(constraints);

    const videoReady =
        new Promise((resolve, reject) => {
            elements.video.onloadedmetadata =
                () => resolve();

            elements.video.onerror =
                () => reject(
                    new Error(
                        "Video could not be loaded."));
        });

    elements.video.srcObject =
        cameraStream;

    await videoReady;
    await elements.video.play();

    elements.canvas.width =
        elements.video.videoWidth;

    elements.canvas.height =
        elements.video.videoHeight;
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
        const blinkDuration =
            performance.now() -
            blinkStartedAt;

        if (blinkDuration >= 20 &&
            blinkDuration <= 1000) {
            blinkDetected = true;
        }

        blinkStartedAt = 0;
    }

    return blinkDetected;
}

function descriptorIsValid(descriptor) {
    return Array.isArray(descriptor) &&
        descriptor.length ===
        DESCRIPTOR_LENGTH &&
        descriptor.every(value =>
            Number.isFinite(value));
}

function getFacePosition(face) {
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
            Math.abs(centreX - 0.5) < 0.18 &&
            Math.abs(centreY - 0.48) < 0.22,

        correctSize:
            faceSize >= 0.25 &&
            faceSize <= 0.75
    };
}

function getGestures(result) {
    return Object.values(
        result.gesture ?? [])
        .map(gesture =>
            gesture.gesture)
        .filter(Boolean);
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
        return;
    }

    if (!positionPassed) {
        setMessage(
            "Centre your face inside the guide.");
        return;
    }

    if (!qualityPassed) {
        setMessage(
            "Move closer and use better lighting.");
        return;
    }

    if (!blinkPassed) {
        setMessage(
            "Look at the camera and blink naturally.");
        return;
    }

    if (!livenessPassed) {
        setMessage(
            "Hold still while liveness is checked.");
        return;
    }

    setMessage(
        "Face verified. Hold still...");
}

function processDetection(result) {
    if (!scanning) {
        return;
    }

    const faces =
        result.face ?? [];

    const faceCountPassed =
        faces.length === 1;

    setCheck(
        "checkFaceCount",
        faceCountPassed);

    if (!faceCountPassed) {
        setCheck("checkPosition", false);
        setCheck("checkQuality", false);
        setCheck("checkBlink", false);
        setCheck("checkLiveness", false);

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
        getFacePosition(face);

    const rotation =
        face.rotation?.angle;

    const rotationCentred =
        rotation &&
        Math.abs(rotation.yaw ?? 0) < 0.30 &&
        Math.abs(rotation.pitch ?? 0) < 0.30;

    const facingCentre =
        gestures.includes(
            "facing center") ||
        rotationCentred;

    const positionPassed =
        position.centred &&
        facingCentre;

    const confidenceScore =
        face.faceScore ??
        face.boxScore ??
        face.score ??
        0;

    const validDescriptor =
        descriptorIsValid(
            face.embedding);

    const qualityPassed =
        confidenceScore >=
        MIN_CONFIDENCE &&
        position.correctSize &&
        validDescriptor;

    const blinkPassed =
        detectBlink(gestures);

    const antiSpoofScore =
        face.real ?? 0;

    const livenessScore =
        face.live ?? 0;

    const livenessPassed =
        antiSpoofScore >=
        MIN_LIVENESS &&
        livenessScore >=
        MIN_LIVENESS;

    setCheck(
        "checkPosition",
        positionPassed);

    setCheck(
        "checkQuality",
        qualityPassed);

    setCheck(
        "checkBlink",
        blinkPassed);

    setCheck(
        "checkLiveness",
        livenessPassed);

    const checks = [
        faceCountPassed,
        positionPassed,
        qualityPassed,
        blinkPassed,
        livenessPassed
    ];

    updateProgress(checks);

    updateInstruction(
        faceCountPassed,
        positionPassed,
        qualityPassed,
        blinkPassed,
        livenessPassed);

    const currentFrameValid =
        checks.every(Boolean) &&
        validDescriptor;

    drawFaceBox(
        face,
        currentFrameValid);

    if (!currentFrameValid) {
        validFrameCount = 0;
        return;
    }

    validFrameCount++;

    const currentScore =
        confidenceScore +
        antiSpoofScore +
        livenessScore;

    if (currentScore >
        bestDescriptorScore) {
        bestDescriptorScore =
            currentScore;

        bestDescriptor =
            [...face.embedding];

        bestLivenessScore =
            Math.min(
                antiSpoofScore,
                livenessScore);
    }

    if (validFrameCount >=
        REQUIRED_VALID_FRAMES) {
        completeEnrollment(face);
    }
}

function completeEnrollment(face) {
    if (!bestDescriptor ||
        !descriptorIsValid(
            bestDescriptor)) {
        handleError(
            new Error(
                "Valid descriptor was not produced."));
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
        "Scan Complete",
        "success");

    setMessage(
        "Face scan completed successfully.");

    elements.startButton.hidden = true;
    elements.retryButton.hidden = false;

    updateSaveButton();
}

function updateSaveButton() {
    const descriptorReady =
        elements.descriptor.value
            .length > 0;

    elements.saveButton.disabled =
        !descriptorReady ||
        !elements.consent.checked;
}

function getCameraErrorMessage(error) {
    if (error?.name ===
        "NotAllowedError") {
        return "Camera permission was denied. " +
            "Please allow camera access.";
    }

    if (error?.name ===
        "NotFoundError") {
        return "No camera was detected.";
    }

    if (error?.name ===
        "NotReadableError") {
        return "The camera is being used " +
            "by another application.";
    }

    return "Face scanner could not start. " +
        "Check the camera and internet connection.";
}

function handleError(error) {
    console.error(
        "Face enrollment error:",
        error);

    stopCamera(false);
    setLoading(false);

    setStatus(
        "Scan Failed",
        "danger");

    setMessage(
        getCameraErrorMessage(error));

    elements.startButton.disabled = false;
    elements.startButton.hidden = false;

    elements.retryButton.hidden = true;
    elements.saveButton.disabled = true;
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

        processDetection(result);
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

async function startEnrollment() {
    stopCamera(false);
    resetEnrollmentState();

    elements.startButton.disabled = true;
    elements.retryButton.hidden = true;

    setStatus(
        "Preparing",
        "warning");

    setMessage(
        "Preparing secure face scanner...");

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
    startEnrollment);

elements.retryButton.addEventListener(
    "click",
    startEnrollment);

elements.consent.addEventListener(
    "change",
    updateSaveButton);

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

        if (!elements.consent.checked) {
            event.preventDefault();

            setStatus(
                "Consent Required",
                "warning");

            setMessage(
                "Biometric consent is required.");

            return;
        }

        elements.saveButton.disabled = true;
        elements.saveButton.textContent =
            "Saving Face ID...";
    });

window.addEventListener(
    "beforeunload",
    () => stopCamera(false));