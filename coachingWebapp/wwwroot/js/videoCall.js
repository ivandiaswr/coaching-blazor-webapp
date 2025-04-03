window.VideoCall = (function(){
    console.log("VideoCall module loaded");
    let connection = null;        // SignalR HubConnection
    let peerConnection = null;    // RTCPeerConnection
    let localStream = null;
    let sessionId = null;

    // STUN/TURN configuration for RTCPeerConnection (replace with your TURN server for prod)
    const rtcConfig = { 
        iceServers: [ 
            { urls: "stun:stun.l.google.com:19302" } 
            // { urls: "turn:your.turn.server", username: "...", credential: "..." }
        ] 
    };

    // Initialize SignalR connection and join session group
    async function init(sessId) {
        sessionId = sessId;
        connection = new signalR.HubConnectionBuilder().withUrl("/videoHub").build();
        // Set up handler to receive signaling messages from server
        connection.on("ReceiveSignal", onReceiveSignal);
        await connection.start();
        console.log("SignalR connected.");
        // Join the specific session group on the hub
        await connection.invoke("JoinSession", sessionId);
        console.log("Joined SignalR group for session " + sessionId);
    }

    // Handle incoming signaling messages from SignalR
    async function onReceiveSignal(messageJson) {
        const message = JSON.parse(messageJson);
        if (message.sdp) {
            if (message.sdp.type === "offer") {
                console.log("Received SDP offer");
                await ensurePeerConnection();  // create RTCPeerConnection and local stream if not already
                await peerConnection.setRemoteDescription(new RTCSessionDescription(message.sdp));
                // Create an answer back to the caller
                const answer = await peerConnection.createAnswer();
                await peerConnection.setLocalDescription(answer);
                // Send the answer via SignalR
                await connection.invoke("SendSignal", sessionId, JSON.stringify({ "sdp": answer }));
                console.log("Sent SDP answer");
            } else if (message.sdp.type === "answer") {
                console.log("Received SDP answer");
                await peerConnection.setRemoteDescription(new RTCSessionDescription(message.sdp));
                // At this point, the call is established from both ends
            }
        } else if (message.candidate) {
            console.log("Received ICE candidate");
            if (peerConnection) {
                try {
                    await peerConnection.addIceCandidate(new RTCIceCandidate(message.candidate));
                } catch (err) {
                    console.error("Error adding ICE candidate", err);
                }
            }
        }
    }

    // Ensure peerConnection and local stream are initialized
    async function ensurePeerConnection() {
        if (!peerConnection) {
            peerConnection = new RTCPeerConnection(rtcConfig);
            // When local ICE candidates found, send them to other peer
            peerConnection.onicecandidate = event => {
                if (event.candidate) {
                    connection.invoke("SendSignal", sessionId, JSON.stringify({ "candidate": event.candidate }));
                }
            };
            // When a remote stream arrives, show it in the remote video element
            peerConnection.ontrack = event => {
                const remoteVideo = document.getElementById("remoteVideo");
                if (event.streams && event.streams[0]) {
                    remoteVideo.srcObject = event.streams[0];
                }
            };
            // If not already got local stream, do so (for answering side).
            if (!localStream) {
                localStream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true });
                document.getElementById("localVideo").srcObject = localStream;
            }
            // Add all local tracks to peer connection (so they get sent to the other peer)
            localStream.getTracks().forEach(track => {
                peerConnection.addTrack(track, localStream);
            });
            console.log("PeerConnection created and local stream added");
            // onnegotiationneeded will trigger offer if this side initiates
            peerConnection.onnegotiationneeded = async () => {
                console.log("Negotiation needed - creating offer");
                const offer = await peerConnection.createOffer();
                await peerConnection.setLocalDescription(offer);
                // Send the SDP offer to the other peer via SignalR
                await connection.invoke("SendSignal", sessionId, JSON.stringify({ "sdp": offer }));
                console.log("Sent SDP offer");
            };
        }
    }

    // Public method to start a call (for the caller)
    async function startCall() {
        if (!connection) {
            console.error("SignalR connection not established. Call init first.");
            return;
        }
        // Get local media (if not obtained already)
        if (!localStream) {
            try {
                localStream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true });
            } catch (err) {
                console.error("Error accessing media devices.", err);
                alert("Could not access camera/microphone: " + err);
                return;
            }
            document.getElementById("localVideo").srcObject = localStream;
        }
        await ensurePeerConnection();
        // For the caller, adding tracks and setting up peerConnection will automatically trigger 
        // the onnegotiationneeded event, sending the offer.
    }

    // Public method to end the call
    function endCall() {
        if (peerConnection) {
            peerConnection.close();
            peerConnection = null;
        }
        if (localStream) {
            localStream.getTracks().forEach(t => t.stop());
            localStream = null;
        }
        if (connection) {
            connection.stop(); // disconnect SignalR
            connection = null;
        }
        console.log("Call ended and resources cleaned up");
    }

    // Expose public methods
    return {
        init: init,
        startCall: startCall,
        endCall: endCall
    };
})();
