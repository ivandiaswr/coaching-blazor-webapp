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

// Force video to reload its source - useful when filtering changes content
window.reloadVideo = function (videoId) {
    try {
        const video = document.getElementById(videoId);
        if (video) {
            video.pause();
            video.currentTime = 0;
            video.load(); // This forces the video to reload its source
            return Promise.resolve();
        }
    } catch (error) {
        console.error('Error reloading video:', error);
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

// Play video by element ID and ensure autoplay
window.playVideo = function (videoId) {
    try {
        const video = document.getElementById(videoId);
        if (video) {
            // Load video source if not already loaded
            if (video.readyState === 0) {
                video.load();
            }

            // Add event listener to ensure controls are visible
            video.addEventListener('loadedmetadata', function () {
                video.controls = true;
            });

            return video.play().then(() => {
                console.log(`Video ${videoId} started playing`);
            }).catch(error => {
                console.error(`Error playing video ${videoId}:`, error);
            });
        }
    } catch (error) {
        console.error('Error playing video:', error);
    }
}

// Pause video by element ID
window.pauseVideo = function (videoId) {
    try {
        const video = document.getElementById(videoId);
        if (video) {
            video.pause();
            console.log(`Video ${videoId} paused`);
            return Promise.resolve();
        }
    } catch (error) {
        console.error('Error pausing video:', error);
        return Promise.reject(error);
    }
}