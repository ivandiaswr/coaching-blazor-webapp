window.playVideoById = function (videoId) {
    try {
        const video = document.getElementById(videoId);
        if (video) {
            // Load video source only when playing to save memory
            if (video.readyState === 0) {
                video.load();
            }
            return video.play();
        }
    } catch (error) {
        console.error('Error playing video:', error);
    }
}

window.pauseVideoById = function (videoId) {
    try {
        const video = document.getElementById(videoId);
        if (video) {
            video.pause();
            // Reset to beginning to free some memory
            video.currentTime = 0;
            return Promise.resolve();
        }
    } catch (error) {
        console.error('Error pausing video:', error);
    }
}

// Cleanup function for videos when component is destroyed
window.cleanupVideos = function (videoIds) {
    try {
        // Handle both single string and array of strings
        const ids = Array.isArray(videoIds) ? videoIds : [videoIds];

        ids.forEach(videoId => {
            const video = document.getElementById(videoId);
            if (video) {
                video.pause();
                video.currentTime = 0;
                video.src = '';
                video.load();
            }
        });
    } catch (error) {
        console.error('Error cleaning up videos:', error);
    }
}