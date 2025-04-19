window.VideoCall = (() => {
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

        if (hubConnection && hubConnection.state !== signalR.HubConnectionState.Disconnected) {
            console.log("🛑 Existing hubConnection still active. Stopping...");
            await hubConnection.stop();
        }
    
        hubConnection = new signalR.HubConnectionBuilder()
            .withUrl("/videoHub")
            .withAutomaticReconnect([0, 2000, 10000, 30000])
            .configureLogging(signalR.LogLevel.Information)
            .build();
    
        hubConnection.on("ReceiveSignal", onReceiveSignal);
        hubConnection.on("ReceiveChatMessage", onReceiveChatMessage);
        hubConnection.on("ReceiveFileAttachment", onReceiveFileAttachment);
    
        hubConnection.onreconnecting((error) => {
            console.warn("SignalR connection lost. Reconnecting...", error);
        });
    
        hubConnection.onreconnected(async (connectionId) => {
            console.log("SignalR reconnected. Connection ID:", connectionId);
        
            try {
                await hubConnection.stop().then(async () => {
                    await hubConnection.start();
                    await hubConnection.invoke("JoinSession", sessionId);
                    console.log("✅ Rejoined session after full reconnect");
                }).catch(err => console.error("❌ Failed to re-establish connection:", err));
                
                console.log("✅ Successfully rejoined session");
            } catch (err) {
                console.error("❌ Failed to rejoin session:", err);
            }
        });      
    
        hubConnection.onclose((error) => {
            console.error("SignalR connection closed:", error);
        });
    
        try {
            await hubConnection.start();
            console.log("SignalR connected. Connection ID:", hubConnection.connectionId);
            await hubConnection.stop().then(async () => {
                await hubConnection.start();
                await hubConnection.invoke("JoinSession", sessionId);
                console.log("✅ Rejoined session after full reconnect");
            }).catch(err => console.error("❌ Failed to re-establish connection:", err));
            
        } catch (err) {
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
        if (hubConnection) {
            hubConnection.stop();
            hubConnection = null;
        }
        if (localStream) {
            localStream.getTracks().forEach(t => t.stop());
            localStream = null;
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
            // ICE candidate
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

    function sendChatMessage(userName, message) {
        if (!hubConnection || hubConnection.state !== signalR.HubConnectionState.Connected) {
            console.warn("Hub not connected. Cannot send message.");
            return;
        }
    
        hubConnection.invoke("SendChatMessage", sessionId, userName, message)
            .catch(err => console.error("Error sending chat message:", err));
    }

    function onReceiveChatMessage(userName, timestamp, message) {
        const chatContainer = document.getElementById("chatMessages");
        if (chatContainer) {
            const shouldAutoScroll =
                chatContainer.scrollTop + chatContainer.clientHeight >= chatContainer.scrollHeight - 50;
    
            const messageDiv = document.createElement("div");
            messageDiv.innerHTML = `<strong>[${timestamp}] ${userName}:</strong> ${message}`;
            chatContainer.appendChild(messageDiv);
    
            if (shouldAutoScroll) {
                chatContainer.scrollTop = chatContainer.scrollHeight;
            }
        }
    }  
    
    function sendFileAttachment(fileName, base64Content, contentType) {
        if (!hubConnection || hubConnection.state !== signalR.HubConnectionState.Connected) {
            console.warn("Hub not connected. Cannot send file.");
            return;
        }

        if (!hubConnection) {
            console.warn("Hub connection not established");
            return;
        }
    
        if (!fileName || !base64Content || !contentType) {
            console.error("Invalid file attachment data:", { fileName, base64Content, contentType });
            return;
        }
    
        const timestamp = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
        const chatContainer = document.getElementById("chatMessages");
        if (!chatContainer) {
            console.warn("Chat container not found");
            return;
        }
    
        try {
            const extensionMap = {
                "image/png": ".png",
                "image/jpeg": ".jpg",
                "application/pdf": ".pdf",
                "text/plain": ".txt",
                "application/msword": ".doc",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document": ".docx",
            };
    
            let downloadFileName = fileName;
            if (!downloadFileName.includes('.')) {
                const extension = extensionMap[contentType] || `.${contentType.split('/').pop()}`;
                downloadFileName += extension;
            }
    
            const binaryData = Uint8Array.from(atob(base64Content), c => c.charCodeAt(0));
            const blob = new Blob([binaryData], { type: contentType });
            const url = URL.createObjectURL(blob);
    
            const fileSize = (binaryData.length / 1024).toFixed(2);
            const sizeText = fileSize < 1024 ? `${fileSize} KB` : `${(fileSize / 1024).toFixed(2)} MB`;
    
            const messageDiv = document.createElement("div");
            const fileIcon = contentType.startsWith('image/') ? '🖼️' : contentType === 'application/pdf' ? '📄' : '📎';
            messageDiv.innerHTML = `
                <strong>[${timestamp}] You:</strong> 
                <a href="${url}" download="${downloadFileName}" title="Download ${downloadFileName}">
                    ${downloadFileName} (${sizeText}) ${fileIcon}
                </a>
            `;
    
            const shouldAutoScroll = chatContainer.scrollTop + chatContainer.clientHeight >= chatContainer.scrollHeight - 50;
            chatContainer.appendChild(messageDiv);
            if (shouldAutoScroll) {
                chatContainer.scrollTop = chatContainer.scrollHeight;
            }
    
            hubConnection.invoke("SendFileAttachment", sessionId, fileName, base64Content, contentType)
                .then(() => console.log("File attachment sent to hub"))
                .catch(err => console.error("Error sending attachment:", err));
    
        } catch (err) {
            console.error("Error processing file attachment:", err);
        }
    }

    function onReceiveFileAttachment(userName, timestamp, fileName, base64Data, contentType) {
        const chatContainer = document.getElementById("chatMessages");
        if (chatContainer) {
            const shouldAutoScroll = 
                chatContainer.scrollTop + chatContainer.clientHeight >= chatContainer.scrollHeight - 50;

            const fileDiv = document.createElement("div");
            const dataUrl = `data:${contentType};base64,${base64Data}`;
            fileDiv.innerHTML = `<strong>[${timestamp}] ${userName}:</strong> <a href="${dataUrl}" download="${fileName}">${fileName}</a>`;
            chatContainer.appendChild(fileDiv);

            if (shouldAutoScroll) {
                chatContainer.scrollTop = chatContainer.scrollHeight;
            }
        }
    }

    return {
        init,
        startCall,
        endCall,
        toggleMic,
        toggleCamera,
        shareScreen,
        sendChatMessage,
        sendFileAttachment
    };
})();