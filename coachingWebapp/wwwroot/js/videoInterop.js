window.playVideoById = function (videoId) {
    try {
        const video = document.getElementById(videoId);
        if (video) {
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
            return video.pause();
        }
    } catch (error) {
        console.error('Error pausing video:', error);
    }
}