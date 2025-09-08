// Enhanced Video Call System with Performance Optimizations
let hubConnection = null;
let peerConnection = null;
let localStream = null;
let sessionId = null;
let isScreenSharing = false;
let dotNetRef = null;
let isNegotiating = false;
let pendingIceCandidates = [];
let isCallStarted = false;
let isLocalStreamReady = false;
let otherParticipantPresent = false;
let connectionQuality = 'unknown';
let lastBytesReceived = 0;
let lastBytesSent = 0;
let statsInterval = null;

// Enhanced RTC Configuration with multiple STUN servers for better connectivity
const rtcConfig = {
    iceServers: [
        { urls: "stun:stun.l.google.com:19302" },
        { urls: "stun:stun1.l.google.com:19302" },
        { urls: "stun:stun.cloudflare.com:3478" }
    ],
    iceCandidatePoolSize: 10,
    bundlePolicy: 'max-bundle',
    rtcpMuxPolicy: 'require'
};

// Performance monitoring
const performanceMetrics = {
    connectionTime: null,
    firstVideoFrame: null,
    audioLatency: null
};

export async function init(sessId, dotNetReference) {
    if (hubConnection || peerConnection || localStream) {
        await cleanup();
    }

    sessionId = sessId;
    dotNetRef = dotNetReference;
    performanceMetrics.connectionTime = performance.now();

    try {
        window.isCurrentUserAdmin = await dotNetRef.invokeMethodAsync("IsCurrentUserAdmin");
    } catch (err) {
        console.error("Failed to determine user role, defaulting to client:", err);
        window.isCurrentUserAdmin = false;
    }

    // Enhanced SignalR connection with better error handling
    hubConnection = new signalR.HubConnectionBuilder()
        .withUrl("/videoHub")
        .withAutomaticReconnect([0, 2000, 10000, 30000]) // Custom retry delays
        .configureLogging(signalR.LogLevel.Information)
        .build();

    // Event handlers
    hubConnection.on("ReceiveSignal", onReceiveSignal);
    hubConnection.on("ParticipantJoined", onParticipantJoined);
    hubConnection.on("ParticipantLeft", onParticipantLeft);
    hubConnection.on("ReceiveChatMessage", (userName, timestamp, message, userRole) => {
        dotNetRef.invokeMethodAsync("OnChatMessageReceived", userName, timestamp, message, userRole);
        // Scroll to bottom of chat
        setTimeout(() => {
            const chatBox = document.getElementById("chat-box");
            if (chatBox) {
                chatBox.scrollTop = chatBox.scrollHeight;
            }
        }, 100);
    });
    hubConnection.on("ReceiveFileAttachment", (userName, timestamp, fileName, base64Data, contentType) => {
        dotNetRef.invokeMethodAsync("OnFileAttachmentReceived", userName, timestamp, fileName, base64Data, contentType);
    });

    // Enhanced connection event handlers
    hubConnection.onreconnecting(error => {
        console.warn("SignalR connection lost. Reconnecting...", error);
        updateConnectionIndicator('connecting');
        dotNetRef?.invokeMethodAsync("OnConnectionStatusChanged", "reconnecting");
    });

    hubConnection.onreconnected(async connectionId => {
        console.log("SignalR reconnected. Re-joining session...");
        updateConnectionIndicator('connected');
        dotNetRef?.invokeMethodAsync("OnConnectionStatusChanged", "connected");
        if (hubConnection) {
            await hubConnection.invoke("JoinSession", sessionId);
        }
    });

    hubConnection.onclose(error => {
        console.error("SignalR connection closed:", error);
        updateConnectionIndicator('disconnected');
        dotNetRef?.invokeMethodAsync("OnConnectionStatusChanged", "disconnected");
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync("OnCallEnded");
        }
    });

    try {
        await hubConnection.start();
        await hubConnection.invoke("JoinSession", sessionId);
        updateConnectionIndicator('connected');
        dotNetRef?.invokeMethodAsync("OnConnectionStatusChanged", "connected");

        // Performance metric
        const connectionTime = performance.now() - performanceMetrics.connectionTime;
        console.log(`SignalR connection established in ${connectionTime.toFixed(2)}ms`);
    } catch (err) {
        console.error("Failed to connect to SignalR hub:", err);
        updateConnectionIndicator('disconnected');
        dotNetRef?.invokeMethodAsync("OnConnectionStatusChanged", "error");
    }
}

// Helper function to update connection indicator
function updateConnectionIndicator(status) {
    const localIndicator = document.querySelector('#localVideo').parentElement.querySelector('.connection-indicator');
    const remoteIndicator = document.querySelector('#remoteVideo').parentElement.querySelector('.connection-indicator');

    if (localIndicator) {
        localIndicator.className = `connection-indicator ${status}`;
    }
    if (remoteIndicator) {
        remoteIndicator.className = `connection-indicator ${status}`;
    }
}

// Enhanced media constraints for better quality
function getOptimalMediaConstraints() {
    return {
        audio: {
            echoCancellation: true,
            noiseSuppression: true,
            autoGainControl: true,
            sampleRate: 48000,
            channelCount: 2
        },
        video: {
            width: { ideal: 1280, max: 1920 },
            height: { ideal: 720, max: 1080 },
            frameRate: { ideal: 30, max: 60 },
            facingMode: "user"
        }
    };
}

// Performance monitoring
function startStatsMonitoring() {
    if (statsInterval) {
        clearInterval(statsInterval);
    }

    statsInterval = setInterval(async () => {
        if (peerConnection && peerConnection.connectionState === 'connected') {
            try {
                const stats = await peerConnection.getStats();
                stats.forEach(stat => {
                    if (stat.type === 'inbound-rtp' && stat.mediaType === 'video') {
                        const currentBytesReceived = stat.bytesReceived || 0;
                        const bytesDelta = currentBytesReceived - lastBytesReceived;
                        const bitrate = (bytesDelta * 8) / 1000; // kbps

                        // Update quality indicator
                        updateVideoQualityIndicator(bitrate, stat.frameWidth, stat.frameHeight);
                        lastBytesReceived = currentBytesReceived;
                    }
                });
            } catch (error) {
                console.warn("Stats monitoring error:", error);
            }
        }
    }, 1000);
}

function updateVideoQualityIndicator(bitrate, width, height) {
    const qualityElement = document.querySelector('.quality-indicator');
    if (!qualityElement) return;

    let quality = 'LOW';
    let className = 'quality-low';

    if (bitrate > 1000 && width >= 1280) {
        quality = 'HD';
        className = 'quality-hd';
    } else if (bitrate > 500 && width >= 640) {
        quality = 'SD';
        className = 'quality-sd';
    }

    qualityElement.textContent = `${quality} ${width}x${height}`;
    qualityElement.className = `quality-indicator ${className}`;
}

export function setCallStarted(started) {
    isCallStarted = started;
    console.log(`Call state updated: isCallStarted = ${isCallStarted}`);

    if (started) {
        startStatsMonitoring();
    } else {
        if (statsInterval) {
            clearInterval(statsInterval);
            statsInterval = null;
        }
    }
}

export async function startCall() {
    if (!hubConnection || hubConnection.state !== signalR.HubConnectionState.Connected) {
        console.warn("Hub not connected, attempting to initialize.");
        await init(sessionId, dotNetRef);
    }

    // Clean up existing stream
    if (localStream) {
        localStream.getTracks().forEach(track => track.stop());
        localStream = null;
    }

    try {
        // Request optimal media with fallback
        const constraints = getOptimalMediaConstraints();
        performanceMetrics.firstVideoFrame = performance.now();

        try {
            localStream = await navigator.mediaDevices.getUserMedia(constraints);
        } catch (error) {
            console.warn("Failed to get optimal media, falling back to basic constraints:", error);
            // Fallback to basic constraints
            localStream = await navigator.mediaDevices.getUserMedia({
                video: true,
                audio: { echoCancellation: true, noiseSuppression: true }
            });
        }

        const localVideo = document.getElementById("localVideo");
        if (localVideo) {
            localVideo.srcObject = localStream;
            // Ensure local video is muted to prevent audio feedback/echo
            localVideo.muted = true;
            localVideo.volume = 0;

            // Add connection indicator
            addConnectionIndicator(localVideo.parentElement);

            // Add quality indicator for local stream
            addQualityIndicator(localVideo.parentElement);

            // Performance metric
            localVideo.onloadeddata = () => {
                const videoTime = performance.now() - performanceMetrics.firstVideoFrame;
                console.log(`First video frame rendered in ${videoTime.toFixed(2)}ms`);
            };

            // Add a small delay to prevent interruption errors
            await new Promise(resolve => setTimeout(resolve, 100));
            localVideo.play().catch(e => console.error("Local video play failed:", e));
        }

        isLocalStreamReady = true;
        // Notify Blazor UI
        dotNetRef.invokeMethodAsync("OnCallStarted");
        dotNetRef.invokeMethodAsync("OnLocalStreamActive", true);

        // If admin and another participant is already present, initiate WebRTC offer
        if (window.isCurrentUserAdmin && otherParticipantPresent) {
            console.log("Admin local stream ready and participant present, initiating WebRTC.");
            await initiateWebRTCIfReady("Client");
        }

        // Start monitoring
        startStatsMonitoring();

    } catch (err) {
        console.error("Error starting call:", err);
        dotNetRef.invokeMethodAsync("OnLocalStreamActive", false);

        // Provide user-friendly error messages
        let errorMessage = "Failed to access camera/microphone";
        if (err.name === 'NotFoundError') {
            errorMessage = "Camera or microphone not found";
        } else if (err.name === 'NotAllowedError') {
            errorMessage = "Please allow camera and microphone access";
        } else if (err.name === 'NotReadableError') {
            errorMessage = "Camera or microphone is being used by another application";
        }

        dotNetRef.invokeMethodAsync("OnError", errorMessage);
    }
}

// Helper functions for UI indicators
function addConnectionIndicator(container) {
    let indicator = container.querySelector('.connection-indicator');
    if (!indicator) {
        indicator = document.createElement('div');
        indicator.className = 'connection-indicator disconnected';
        container.appendChild(indicator);
    }
    return indicator;
}

function addQualityIndicator(container) {
    let indicator = container.querySelector('.quality-indicator');
    if (!indicator) {
        indicator = document.createElement('div');
        indicator.className = 'quality-indicator';
        indicator.textContent = 'Connecting...';
        container.appendChild(indicator);
    }
    return indicator;
}

export async function endCall() {
    try {
        await cleanup();
        dotNetRef.invokeMethodAsync("OnCallEnded");
        console.log("Call ended successfully");
    } catch (error) {
        console.error("Error ending call:", error);
    }
}

async function cleanupForReconnection() {
    console.log("Cleaning up for reconnection...");

    // Stop stats monitoring
    if (statsInterval) {
        clearInterval(statsInterval);
        statsInterval = null;
    }

    if (peerConnection) {
        peerConnection.onicecandidate = null;
        peerConnection.ontrack = null;
        peerConnection.onconnectionstatechange = null;
        peerConnection.ondatachannel = null;

        if (peerConnection.connectionState !== 'closed') {
            peerConnection.close();
        }
        peerConnection = null;
    }

    isNegotiating = false;
    pendingIceCandidates = [];

    const remoteVideo = document.getElementById("remoteVideo");
    if (remoteVideo) {
        remoteVideo.pause();
        remoteVideo.srcObject = null;
        remoteVideo.load(); // Reset video element

        // Update indicators
        const indicator = remoteVideo.parentElement.querySelector('.connection-indicator');
        if (indicator) {
            indicator.className = 'connection-indicator disconnected';
        }

        const qualityIndicator = remoteVideo.parentElement.querySelector('.quality-indicator');
        if (qualityIndicator) {
            qualityIndicator.textContent = 'Disconnected';
            qualityIndicator.className = 'quality-indicator quality-low';
        }
    }

    if (dotNetRef) {
        dotNetRef.invokeMethodAsync("OnRemoteStreamDisconnected");
    }

    console.log("Cleanup for reconnection complete.");
}

export async function cleanup() {
    console.log("Starting complete cleanup...");

    // Stop stats monitoring
    if (statsInterval) {
        clearInterval(statsInterval);
        statsInterval = null;
    }

    if (localStream) {
        localStream.getTracks().forEach(track => {
            track.stop();
            console.log(`Stopped ${track.kind} track`);
        });
        localStream = null;
        isLocalStreamReady = false;
    }

    // Clear local video
    const localVideo = document.getElementById("localVideo");
    if (localVideo) {
        localVideo.pause();
        localVideo.srcObject = null;
        localVideo.load();

        // Clean up indicators
        const indicators = localVideo.parentElement.querySelectorAll('.connection-indicator, .quality-indicator');
        indicators.forEach(indicator => indicator.remove());
    }

    await cleanupForReconnection();

    if (hubConnection) {
        try {
            await hubConnection.stop();
            console.log("SignalR connection stopped");
        } catch (error) {
            console.warn("Error stopping SignalR connection:", error);
        }
        hubConnection = null;
    }

    // Reset state variables
    isCallStarted = false;
    otherParticipantPresent = false;
    isScreenSharing = false;
    connectionQuality = 'unknown';

    // Reset performance metrics
    Object.keys(performanceMetrics).forEach(key => {
        performanceMetrics[key] = null;
    });

    console.log("Complete cleanup finished");
}

async function ensurePeerConnection() {
    if (!peerConnection || peerConnection.connectionState === 'closed' || peerConnection.connectionState === 'failed') {
        console.log("Creating new PeerConnection with enhanced configuration.");
        peerConnection = new RTCPeerConnection(rtcConfig);

        // Enhanced connection state monitoring
        peerConnection.onconnectionstatechange = event => {
            console.log(`Peer Connection State Change: ${peerConnection.connectionState}`);
            updateConnectionIndicator(peerConnection.connectionState === 'connected' ? 'connected' : 'connecting');

            if (peerConnection.connectionState === 'connected') {
                console.log("Peer connection established successfully");
                startStatsMonitoring();
            } else if (peerConnection.connectionState === 'failed') {
                console.warn("Peer connection failed, attempting ICE restart");
                // Attempt to restart ICE, which can sometimes recover the connection
                peerConnection.restartIce();
            } else if (peerConnection.connectionState === 'disconnected') {
                console.warn("Peer connection disconnected");
                if (statsInterval) {
                    clearInterval(statsInterval);
                    statsInterval = null;
                }
            }
        };

        // ICE connection state monitoring
        peerConnection.oniceconnectionstatechange = () => {
            console.log(`ICE Connection State: ${peerConnection.iceConnectionState}`);
            if (peerConnection.iceConnectionState === 'failed') {
                console.warn("ICE connection failed");
                dotNetRef?.invokeMethodAsync("OnConnectionError", "Connection failed");
            }
        };

        // Enhanced ICE candidate handling
        peerConnection.onicecandidate = event => {
            if (event.candidate && hubConnection && hubConnection.state === signalR.HubConnectionState.Connected) {
                console.log("Sending ICE candidate:", event.candidate.type);
                hubConnection.invoke("SendSignal", sessionId,
                    JSON.stringify({ type: 'ice-candidate', candidate: event.candidate }))
                    .catch(e => console.error("Failed to send ICE candidate:", e));
            } else if (event.candidate === null) {
                console.log("ICE gathering complete");
            }
        };

        // Enhanced remote stream handling
        peerConnection.ontrack = event => {
            console.log(`Remote ${event.track.kind} track received.`);
            const remoteVideo = document.getElementById("remoteVideo");
            if (remoteVideo && event.streams && event.streams[0]) {
                remoteVideo.srcObject = event.streams[0];

                // Add indicators
                addConnectionIndicator(remoteVideo.parentElement);
                addQualityIndicator(remoteVideo.parentElement);

                // Performance tracking
                if (event.track.kind === 'video') {
                    event.track.onended = () => {
                        console.log("Remote video track ended");
                        updateConnectionIndicator('disconnected');
                    };

                    // Track first frame
                    remoteVideo.onloadeddata = () => {
                        console.log("Remote video first frame loaded");
                        updateConnectionIndicator('connected');
                        dotNetRef.invokeMethodAsync("OnRemoteStreamConnected");
                    };
                }

                remoteVideo.play().catch(e => console.error("Remote video play failed:", e));
            }
        };

        // Data channel for future enhancements (file transfer, etc.)
        peerConnection.ondatachannel = event => {
            const channel = event.channel;
            console.log("Data channel received:", channel.label);

            channel.onopen = () => console.log("Data channel opened");
            channel.onclose = () => console.log("Data channel closed");
            channel.onmessage = event => console.log("Data channel message:", event.data);
        };

        // Add local stream tracks if available
        if (localStream) {
            console.log("Adding local stream tracks to PeerConnection.");
            localStream.getTracks().forEach((track, index) => {
                const sender = peerConnection.addTrack(track, localStream);
                console.log(`Added ${track.kind} track ${index + 1}`);

                // Enable adaptability for better quality adjustment
                if (track.kind === 'video') {
                    const params = sender.getParameters();
                    if (!params.encodings) {
                        params.encodings = [{}];
                    }
                    // Enable automatic bitrate adjustment
                    params.encodings[0].adaptivePtime = true;
                    sender.setParameters(params).catch(e =>
                        console.warn("Failed to set adaptive parameters:", e));
                }
            });
        }
    }
}

async function processQueuedIceCandidates() {
    while (pendingIceCandidates.length > 0) {
        const candidate = pendingIceCandidates.shift();
        try {
            if (peerConnection && peerConnection.remoteDescription) {
                await peerConnection.addIceCandidate(new RTCIceCandidate(candidate));
            } else {
                pendingIceCandidates.unshift(candidate);
                break;
            }
        } catch (e) {
            console.error("Error adding received ICE candidate", e);
        }
    }
}

async function onReceiveSignal(messageJson) {
    const signal = JSON.parse(messageJson);

    if (isNegotiating && signal.type !== 'ice-candidate') {
        console.warn(`Negotiation is in progress. Ignoring ${signal.type} signal.`);
        return;
    }

    try {
        // If client receives offer but hasn't started local stream, auto-initiate startCall
        if (signal.type === 'offer' && !window.isCurrentUserAdmin && !isLocalStreamReady) {
            console.log("Client not ready for offer, initializing local stream.");
            await startCall();
        }
        await ensurePeerConnection();
        if (signal.type === 'offer') {
            console.log("Received offer.");
            isNegotiating = true;
            await peerConnection.setRemoteDescription(new RTCSessionDescription({
                type: signal.type,
                sdp: signal.sdp
            }));
            await processQueuedIceCandidates();
            console.log("Creating answer.");
            const answer = await peerConnection.createAnswer();
            await peerConnection.setLocalDescription(answer);
            if (hubConnection) {
                console.log("Sending answer.");
                await hubConnection.invoke("SendSignal", sessionId,
                    JSON.stringify({ type: 'answer', sdp: peerConnection.localDescription.sdp }));
            }
            isNegotiating = false;
        } else if (signal.type === 'answer') {
            console.log("Received answer.");
            isNegotiating = true;
            await peerConnection.setRemoteDescription(new RTCSessionDescription({
                type: signal.type,
                sdp: signal.sdp
            }));
            await processQueuedIceCandidates();
            isNegotiating = false;
        } else if (signal.type === 'ice-candidate') {
            console.log("Received ICE candidate.");
            if (peerConnection.remoteDescription) {
                await peerConnection.addIceCandidate(new RTCIceCandidate(signal.candidate));
            } else {
                console.log("Queuing ICE candidate.");
                pendingIceCandidates.push(signal.candidate);
            }
        }
    } catch (e) {
        console.error("Error handling signal:", e);
        isNegotiating = false;
    }
}

export function toggleMic() {
    if (localStream) {
        const audioTrack = localStream.getAudioTracks()[0];
        if (audioTrack) {
            audioTrack.enabled = !audioTrack.enabled;
            return !audioTrack.enabled;
        }
    }
    return false;
}

export function toggleCamera() {
    if (localStream) {
        const videoTrack = localStream.getVideoTracks()[0];
        if (videoTrack) {
            videoTrack.enabled = !videoTrack.enabled;
            return !videoTrack.enabled;
        }
    }
    return false;
}

export async function shareScreen() {
    if (!isScreenSharing) {
        try {
            const screenStream = await navigator.mediaDevices.getDisplayMedia({ video: true });
            const screenTrack = screenStream.getVideoTracks()[0];

            const sender = peerConnection.getSenders().find(s => s.track && s.track.kind === 'video');
            if (sender) {
                sender.replaceTrack(screenTrack);
            } else {
                peerConnection.addTrack(screenTrack, localStream);
            }

            const localVideoTrack = localStream.getVideoTracks()[0];
            localStream.removeTrack(localVideoTrack);
            localStream.addTrack(screenTrack);


            screenTrack.onended = () => {
                stopScreenShare(screenTrack);
            };

            isScreenSharing = true;
        } catch (err) {
            console.error("Screen share failed:", err);
        }
    } else {
        const screenTrack = localStream.getVideoTracks().find(t => t.getSettings().displaySurface);
        await stopScreenShare(screenTrack);
    }
    return isScreenSharing;
}

async function stopScreenShare(screenTrack) {
    if (screenTrack) {
        screenTrack.stop();
        localStream.removeTrack(screenTrack);
    }

    const newStream = await navigator.mediaDevices.getUserMedia({ video: true });
    const newVideoTrack = newStream.getVideoTracks()[0];
    localStream.addTrack(newVideoTrack);

    const sender = peerConnection.getSenders().find(s => s.track && s.track.kind === 'video');
    if (sender) {
        sender.replaceTrack(newVideoTrack);
    }

    // Ensure local video remains muted after screen share
    const localVideo = document.getElementById("localVideo");
    if (localVideo) {
        localVideo.muted = true;
        localVideo.volume = 0;
    }

    isScreenSharing = false;
}

export function sendChatMessage(userName, message) {
    if (hubConnection && hubConnection.state === signalR.HubConnectionState.Connected) {
        hubConnection.invoke("SendChatMessage", sessionId, userName, message);
    }
}

export function sendFileAttachment(fileName, base64Content, contentType, userName) {
    if (hubConnection && hubConnection.state === signalR.HubConnectionState.Connected) {
        hubConnection.invoke("SendFileAttachment", sessionId, userName, fileName, base64Content, contentType);
    }
}

async function onParticipantJoined(participantType) {
    // Mark that another participant is present
    otherParticipantPresent = true;
    dotNetRef.invokeMethodAsync("OnOtherParticipantChanged", true);
    if (window.isCurrentUserAdmin) {
        if (isCallStarted && isLocalStreamReady) {
            console.log("Admin detected another participant, initiating WebRTC.");
            await initiateWebRTCIfReady(participantType);
        } else {
            console.log("Admin: Waiting for local stream to be ready before initiating WebRTC.");
        }
    }
}

async function initiateWebRTCIfReady(participantType) {
    if (!isCallStarted || !isLocalStreamReady) {
        console.log("Skipping WebRTC initiation: Call or local stream not started.");
        return;
    }
    if (isNegotiating) {
        console.log("Negotiation already in progress. Skipping initiation.");
        return;
    }
    console.log(`Initiating WebRTC for ${participantType}...`);
    await cleanupForReconnection();
    await ensurePeerConnection();
    isNegotiating = true;
    try {
        console.log("Creating offer.");
        const offer = await peerConnection.createOffer({
            offerToReceiveAudio: 1,
            offerToReceiveVideo: 1
        });
        await peerConnection.setLocalDescription(offer);
        if (hubConnection) {
            console.log("Sending offer.");
            await hubConnection.invoke("SendSignal", sessionId,
                JSON.stringify({ type: 'offer', sdp: peerConnection.localDescription.sdp }));
        }
    } catch (e) {
        console.error("Error initiating WebRTC:", e);
    } finally {
        isNegotiating = false;
    }
}

function onParticipantLeft(participantType) {
    console.log(`Participant left: ${participantType}. Cleaning up for reconnection.`);
    // Mark that other participant has left
    otherParticipantPresent = false;
    dotNetRef.invokeMethodAsync("OnOtherParticipantChanged", false);
    // Add a small delay before cleanup to ensure proper state management
    setTimeout(() => {
        cleanupForReconnection();
    }, 100);
}

export function getConnectionState() {
    return {
        hub: hubConnection ? hubConnection.state : 'disconnected',
        peer: peerConnection ? peerConnection.connectionState : 'disconnected'
    };
}

export function toggleFullscreen() {
    const remoteVideo = document.getElementById('remoteVideo');
    if (!remoteVideo) {
        console.error('Remote video element not found');
        return;
    }

    if (!document.fullscreenElement) {
        // Enter fullscreen
        if (remoteVideo.requestFullscreen) {
            remoteVideo.requestFullscreen();
        } else if (remoteVideo.webkitRequestFullscreen) {
            remoteVideo.webkitRequestFullscreen();
        } else if (remoteVideo.msRequestFullscreen) {
            remoteVideo.msRequestFullscreen();
        }
    } else {
        // Exit fullscreen
        if (document.exitFullscreen) {
            document.exitFullscreen();
        } else if (document.webkitExitFullscreen) {
            document.webkitExitFullscreen();
        } else if (document.msExitFullscreen) {
            document.msExitFullscreen();
        }
    }
}

export function setRemoteVideoVolume(volume) {
    const remoteVideo = document.getElementById('remoteVideo');
    if (remoteVideo) {
        remoteVideo.volume = volume;
        console.log(`Remote video volume set to: ${volume}`);
    } else {
        console.error('Remote video element not found');
    }
}