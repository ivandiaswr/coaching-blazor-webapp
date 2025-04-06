window.VideoCall = (() => {

    console.log("VideoCall module loading...");

    let hubConnection = null;
    let peerConnection = null;  
    let localStream = null;      
    let sessionId = null;

    // needs STUN/TURN configs for users with nat/firewalls 
    const rtcConfig = {
        iceServers: [
            { urls: "stun:stun.l.google.com:19302" }
        ]
    };

    async function init(sessId) {
        sessionId = sessId;
        console.log("Initializing VideoCall for session:", sessionId);

        // Builds and starts the connection
        hubConnection = new signalR.HubConnectionBuilder()
            .withUrl("/videoHub")
            .build();

        hubConnection.on("ReceiveSignal", onReceiveSignal);

        try {
            await hubConnection.start();

            await hubConnection.invoke("JoinSession", sessionId);
        } 
        catch (err) {
            console.error("Failed to connect to SignalR hub:", err);
        }
    }

    async function startCall() {
        if (!hubConnection) {
            await init(sessionId);
        }
    
        if (!localStream) {
            try {
                localStream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true });
                document.getElementById("localVideo").srcObject = localStream;
            } catch (err) {
                console.error("Error accessing media devices.", err);
                alert("Could not access camera/microphone: " + err);
                return;
            }
        }
    
        await ensurePeerConnection();
    }

    function endCall() {
        if (peerConnection) {
            peerConnection.close();
            peerConnection = null;
        }
        if (localStream) {
            localStream.getTracks().forEach(t => t.stop());
            localStream = null;
        }
        if (hubConnection) {
            hubConnection.stop();
            hubConnection = null;
        }

        document.getElementById("localVideo").srcObject = null;
        document.getElementById("remoteVideo").srcObject = null;
    }

    async function ensurePeerConnection() {
        if (peerConnection) return;

        peerConnection = new RTCPeerConnection(rtcConfig);

        peerConnection.onicecandidate = event => {
            if (event.candidate) {
                hubConnection.invoke("SendSignal", sessionId, 
                    JSON.stringify({ candidate: event.candidate }));
            }
        };

        peerConnection.ontrack = event => {
            const remoteVideo = document.getElementById("remoteVideo");
            if (event.streams && event.streams[0]) {
                remoteVideo.srcObject = event.streams[0];
            }
        };

        if (localStream) {
            localStream.getTracks().forEach(track => {
                peerConnection.addTrack(track, localStream);
            });
        }

        peerConnection.onnegotiationneeded = async () => {
            try {
                const offer = await peerConnection.createOffer();
                await peerConnection.setLocalDescription(offer);

                await hubConnection.invoke("SendSignal", sessionId, JSON.stringify({ sdp: offer }));
            } catch (err) {
                console.error("Error during negotiation/offering:", err);
            }
        };
    }

    async function onReceiveSignal(messageJson) {
        const message = JSON.parse(messageJson);

        if (message.sdp) {
            if (message.sdp.type === "offer") {
                await ensurePeerConnection();
                await peerConnection.setRemoteDescription(new RTCSessionDescription(message.sdp));

                const answer = await peerConnection.createAnswer();
                await peerConnection.setLocalDescription(answer);

                await hubConnection.invoke("SendSignal", sessionId, JSON.stringify({ sdp: answer }));
            }
            else if (message.sdp.type === "answer") {
                console.log("Received SDP answer");
                await peerConnection.setRemoteDescription(new RTCSessionDescription(message.sdp));
            }
        }
        else if (message.candidate) {
            // We got an ICE candidate
            if (peerConnection) {
                try {
                    await peerConnection.addIceCandidate(new RTCIceCandidate(message.candidate));
                } catch (err) {
                    console.error("Error adding ICE candidate:", err);
                }
            }
        }
    }

    function toggleMic() {
        if (!localStream) return;
    
        const audioTrack = localStream.getAudioTracks()[0];
        if (audioTrack) {
            audioTrack.enabled = !audioTrack.enabled;
        }
    }
    
    function toggleCamera() {
        if (!localStream) return;
    
        const videoTrack = localStream.getVideoTracks()[0];
        if (videoTrack) {
            videoTrack.enabled = !videoTrack.enabled;
        }
    }
    

    async function shareScreen() {
        if (!peerConnection) {
            console.warn("No peer connection");
            return;
        }
    
        try {
            const screenStream = await navigator.mediaDevices.getDisplayMedia({ video: true });
    
            const screenTrack = screenStream.getVideoTracks()[0];
            const sender = peerConnection.getSenders().find(s => s.track.kind === "video");
    
            if (sender) {
                sender.replaceTrack(screenTrack);
    
                screenTrack.onended = async () => {
                    const cameraTrack = localStream.getVideoTracks()[0];
                    if (cameraTrack) {
                        await sender.replaceTrack(cameraTrack);
                    }
                };
            }
        } catch (err) {
            console.error("Error sharing screen:", err);
        }
    }
    
    return {
        init,
        startCall,
        endCall,
        toggleMic,
        toggleCamera,
        shareScreen
    };
})();