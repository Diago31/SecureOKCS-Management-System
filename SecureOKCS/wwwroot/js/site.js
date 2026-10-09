// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
// ==========================================
// ADMIN IDLE SESSION TIMEOUT
// ==========================================
(() => {
    const config =
        document.getElementById(
            "adminIdleTimeoutConfig");

    // Stop when the current user is
    // not an authenticated administrator
    if (!config) {
        return;
    }

    const timeoutForm =
        document.getElementById(
            "adminSessionTimeoutForm");

    const warningModalElement =
        document.getElementById(
            "adminIdleWarningModal");

    const countdownElement =
        document.getElementById(
            "adminIdleCountdown");

    const continueButton =
        document.getElementById(
            "adminContinueSessionButton");

    const signOutButton =
        document.getElementById(
            "adminSignOutNowButton");

    if (!timeoutForm ||
        !warningModalElement ||
        !countdownElement ||
        !continueButton ||
        !signOutButton) {
        return;
    }

    const timeoutMilliseconds =
        Number(
            config.dataset
                .timeoutMilliseconds) ||
        60000;

    const warningMilliseconds =
        Number(
            config.dataset
                .warningMilliseconds) ||
        45000;

    const keepAliveUrl =
        config.dataset.keepAliveUrl;

    const verificationTokenInput =
        timeoutForm.querySelector(
            'input[name="__RequestVerificationToken"]');

    const verificationToken =
        verificationTokenInput?.value;

    const activityStorageKey =
        "SecureOKCS.AdminLastActivity";

    const keepAliveInterval =
        60000;

    const activityThrottle =
        1000;

    let lastActivity =
        Date.now();

    let lastProcessedActivity =
        0;

    let lastKeepAliveRequest =
        Date.now();

    let warningTimer = null;
    let timeoutTimer = null;
    let countdownTimer = null;

    let isEndingSession = false;
    let keepAliveInProgress = false;

    const warningModal =
        bootstrap.Modal.getOrCreateInstance(
            warningModalElement);

    // Store activity so multiple browser
    // tabs share the same idle timer
    function saveLastActivity() {
        try {
            localStorage.setItem(
                activityStorageKey,
                lastActivity.toString());
        }
        catch {
            // The timer can continue when
            // local storage is unavailable
        }
    }

    function clearTimers() {
        if (warningTimer !== null) {
            window.clearTimeout(
                warningTimer);

            warningTimer = null;
        }

        if (timeoutTimer !== null) {
            window.clearTimeout(
                timeoutTimer);

            timeoutTimer = null;
        }

        if (countdownTimer !== null) {
            window.clearInterval(
                countdownTimer);

            countdownTimer = null;
        }
    }

    function hideWarning() {
        warningModal.hide();

        if (countdownTimer !== null) {
            window.clearInterval(
                countdownTimer);

            countdownTimer = null;
        }

        countdownElement.textContent =
            "15";
    }

    function endSession() {
        if (isEndingSession) {
            return;
        }

        isEndingSession = true;

        clearTimers();
        warningModal.hide();

        timeoutForm.requestSubmit();
    }

    function updateCountdown() {
        const elapsed =
            Date.now() - lastActivity;

        const remainingMilliseconds =
            Math.max(
                0,
                timeoutMilliseconds -
                elapsed);

        const remainingSeconds =
            Math.ceil(
                remainingMilliseconds /
                1000);

        countdownElement.textContent =
            remainingSeconds.toString();

        if (remainingMilliseconds <= 0) {
            endSession();
        }
    }

    function showWarning() {
        if (isEndingSession) {
            return;
        }

        updateCountdown();
        warningModal.show();

        if (countdownTimer !== null) {
            window.clearInterval(
                countdownTimer);
        }

        countdownTimer =
            window.setInterval(
                updateCountdown,
                1000);
    }

    function scheduleTimers() {
        clearTimers();

        if (isEndingSession) {
            return;
        }

        const elapsed =
            Date.now() - lastActivity;

        if (elapsed >=
            timeoutMilliseconds) {
            endSession();
            return;
        }

        if (elapsed >=
            warningMilliseconds) {
            showWarning();
        }
        else {
            const timeUntilWarning =
                warningMilliseconds -
                elapsed;

            warningTimer =
                window.setTimeout(
                    showWarning,
                    timeUntilWarning);
        }

        const timeUntilTimeout =
            timeoutMilliseconds -
            elapsed;

        timeoutTimer =
            window.setTimeout(
                endSession,
                timeUntilTimeout);
    }

    async function sendKeepAlive(
        forceRequest = false) {

        if (!keepAliveUrl ||
            !verificationToken ||
            keepAliveInProgress ||
            isEndingSession) {
            return;
        }

        const currentTime =
            Date.now();

        if (!forceRequest &&
            currentTime -
            lastKeepAliveRequest <
            keepAliveInterval) {
            return;
        }

        keepAliveInProgress = true;
        lastKeepAliveRequest =
            currentTime;

        const requestBody =
            new URLSearchParams();

        requestBody.append(
            "__RequestVerificationToken",
            verificationToken);

        try {
            const response =
                await fetch(
                    keepAliveUrl,
                    {
                        method: "POST",

                        credentials:
                            "same-origin",

                        headers: {
                            "Content-Type":
                                "application/x-www-form-urlencoded; charset=UTF-8",

                            "X-Requested-With":
                                "XMLHttpRequest"
                        },

                        body:
                            requestBody.toString()
                    });

            if (response.status === 401 ||
                response.status === 403 ||
                response.redirected) {

                window.location.assign(
                    "/Account/Login");

                return;
            }

            if (!response.ok) {
                console.warn(
                    "The administrator session " +
                    "could not be refreshed.");
            }
        }
        catch {
            console.warn(
                "The session keep-alive " +
                "request failed.");
        }
        finally {
            keepAliveInProgress =
                false;
        }
    }

    function registerActivity(
        forceKeepAlive = false) {

        if (isEndingSession) {
            return;
        }

        const currentTime =
            Date.now();

        if (!forceKeepAlive &&
            currentTime -
            lastProcessedActivity <
            activityThrottle) {
            return;
        }

        lastProcessedActivity =
            currentTime;

        lastActivity =
            currentTime;

        saveLastActivity();
        hideWarning();
        scheduleTimers();

        void sendKeepAlive(
            forceKeepAlive);
    }

    const activityEvents = [
        "pointermove",
        "pointerdown",
        "keydown",
        "scroll",
        "touchstart"
    ];

    activityEvents.forEach(
        eventName => {
            document.addEventListener(
                eventName,
                () => {
                    registerActivity();
                },
                {
                    passive: true
                });
        });

    continueButton.addEventListener(
        "click",
        () => {
            registerActivity(true);
        });

    signOutButton.addEventListener(
        "click",
        () => {
            endSession();
        });

    // Synchronize activity between
    // multiple browser tabs
    window.addEventListener(
        "storage",
        event => {
            if (event.key !==
                activityStorageKey ||
                !event.newValue) {
                return;
            }

            const storedActivity =
                Number(event.newValue);

            if (!Number.isFinite(
                storedActivity) ||
                storedActivity <=
                lastActivity) {
                return;
            }

            lastActivity =
                storedActivity;

            hideWarning();
            scheduleTimers();
        });

    // Recheck the session when returning
    // to a background browser tab
    document.addEventListener(
        "visibilitychange",
        () => {
            if (!document.hidden) {
                scheduleTimers();
            }
        });

    saveLastActivity();
    scheduleTimers();
})();