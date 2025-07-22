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

const rtcConfig = {
    iceServers: [
        { urls: "stun:stun.l.google.com:19302" }
    ]
};

export async function init(sessId, dotNetReference) {
    if (hubConnection || peerConnection || localStream) {
        await cleanup();
    }

    sessionId = sessId;
    dotNetRef = dotNetReference;

    try {
        window.isCurrentUserAdmin = await dotNetRef.invokeMethodAsync("IsCurrentUserAdmin");
    } catch (err) {
        console.error("Failed to determine user role, defaulting to client:", err);
        window.isCurrentUserAdmin = false;
    }

    hubConnection = new signalR.HubConnectionBuilder()
        .withUrl("/videoHub")
        .withAutomaticReconnect()
        .build();

    hubConnection.on("ReceiveSignal", onReceiveSignal);
    hubConnection.on("ParticipantJoined", onParticipantJoined);
    hubConnection.on("ParticipantLeft", onParticipantLeft);
    hubConnection.on("ReceiveChatMessage", (userName, timestamp, message, userRole) => {
        dotNetRef.invokeMethodAsync("OnChatMessageReceived", userName, timestamp, message, userRole);
    });
    hubConnection.on("ReceiveFileAttachment", (userName, timestamp, fileName, base64Data, contentType) => {
        dotNetRef.invokeMethodAsync("OnFileAttachmentReceived", userName, timestamp, fileName, base64Data, contentType);
    });


    hubConnection.onreconnecting(error => console.warn("SignalR connection lost. Reconnecting...", error));
    hubConnection.onreconnected(async connectionId => {
        console.log("SignalR reconnected. Re-joining session...");
        if (hubConnection) {
            await hubConnection.invoke("JoinSession", sessionId);
        }
    });
    hubConnection.onclose(error => {
        console.error("SignalR connection closed:", error);
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync("OnCallEnded");
        }
    });

    try {
        await hubConnection.start();
        await hubConnection.invoke("JoinSession", sessionId);
    } catch (err) {
        console.error("Failed to connect to SignalR hub:", err);
    }
}

export function setCallStarted(started) {
    isCallStarted = started;
    console.log(`Call state updated: isCallStarted = ${isCallStarted}`);
}

export async function startCall() {
    if (!hubConnection || hubConnection.state !== signalR.HubConnectionState.Connected) {
        console.warn("Hub not connected, attempting to initialize.");
        await init(sessionId, dotNetRef);
    }
    if (localStream) {
        localStream.getTracks().forEach(track => track.stop());
        localStream = null;
    }
    try {
        localStream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true });
        const localVideo = document.getElementById("localVideo");
        if (localVideo) {
            localVideo.srcObject = localStream;
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
    } catch (err) {
        console.error("Error starting call:", err);
        dotNetRef.invokeMethodAsync("OnLocalStreamActive", false);
    }
}

export async function endCall() {
    await cleanup();
    dotNetRef.invokeMethodAsync("OnCallEnded");
}

async function cleanupForReconnection() {
    console.log("Cleaning up for reconnection...");
    if (peerConnection) {
        peerConnection.onicecandidate = null;
        peerConnection.ontrack = null;
        peerConnection.onconnectionstatechange = null;
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
    }
    if (dotNetRef) {
        dotNetRef.invokeMethodAsync("OnRemoteStreamDisconnected");
    }
    console.log("Cleanup for reconnection complete.");
}

export async function cleanup() {
    if (localStream) {
        localStream.getTracks().forEach(track => track.stop());
        localStream = null;
        isLocalStreamReady = false;
    }
    // Clear local video
    const localVideo = document.getElementById("localVideo");
    if (localVideo) {
        localVideo.pause();
        localVideo.srcObject = null;
        localVideo.load();
    }
    await cleanupForReconnection();
    if (hubConnection) {
        await hubConnection.stop();
        hubConnection = null;
    }
    isCallStarted = false;
    otherParticipantPresent = false;
}

async function ensurePeerConnection() {
    if (!peerConnection || peerConnection.connectionState === 'closed' || peerConnection.connectionState === 'failed') {
        console.log("Creating new PeerConnection.");
        peerConnection = new RTCPeerConnection(rtcConfig);

        peerConnection.onconnectionstatechange = event => {
            console.log(`Peer Connection State Change: ${peerConnection.connectionState}`);
            if (peerConnection.connectionState === 'failed') {
                // Attempt to restart ICE, which can sometimes recover the connection
                peerConnection.restartIce();
            }
        };

        peerConnection.onicecandidate = event => {
            if (event.candidate && hubConnection && hubConnection.state === signalR.HubConnectionState.Connected) {
                const signal = { type: 'ice-candidate', candidate: event.candidate };
                console.log("Sending ICE candidate:", signal);
                hubConnection.invoke("SendSignal", sessionId, JSON.stringify(signal));
            }
        };
        peerConnection.ontrack = event => {
            console.log("Remote track received.");
            const remoteVideo = document.getElementById("remoteVideo");
            if (remoteVideo && event.streams && event.streams[0]) {
                console.log("Attaching remote stream to video element.");
                remoteVideo.srcObject = event.streams[0];
                // Add a small delay and clear any existing src to prevent interruption
                setTimeout(() => {
                    remoteVideo.play().catch(e => console.error("Remote video play failed:", e));
                }, 100);
                dotNetRef.invokeMethodAsync("OnRemoteStreamConnected");
            }
        };
        if (localStream) {
            console.log("Adding local stream tracks to PeerConnection.");
            localStream.getTracks().forEach(track => peerConnection.addTrack(track, localStream));
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