// Video optimization utilities for better memory management
window.videoOptimization = {
    // Track video elements for cleanup
    activeVideos: new Set(),
    observers: new Map(),

    // Setup intersection observer to pause videos when not visible
    setupVideoObserver: function (videoElement) {
        if (!videoElement || !('IntersectionObserver' in window)) {
            return;
        }

        try {
            this.activeVideos.add(videoElement);

            const observer = new IntersectionObserver((entries) => {
                entries.forEach(entry => {
                    const video = entry.target;
                    if (entry.isIntersecting) {
                        // Video is visible, play it
                        this.safeVideoPlay(video);
                    } else {
                        // Video is not visible, pause it to save memory
                        this.safeVideoPause(video);
                    }
                });
            }, {
                threshold: 0.25, // Play when 25% visible
                rootMargin: '50px'
            });

            observer.observe(videoElement);
            this.observers.set(videoElement, observer);

            // Handle page visibility changes
            this.setupPageVisibilityHandler(videoElement);
        } catch (error) {
            console.error('Error setting up video observer:', error);
        }
    },

    // Setup page visibility handler to pause videos when page is hidden
    setupPageVisibilityHandler: function (videoElement) {
        const handleVisibilityChange = () => {
            if (document.hidden) {
                this.safeVideoPause(videoElement);
            } else if (this.isVideoInViewport(videoElement)) {
                this.safeVideoPlay(videoElement);
            }
        };

        document.addEventListener('visibilitychange', handleVisibilityChange);

        // Store reference for cleanup
        if (!videoElement.visibilityHandler) {
            videoElement.visibilityHandler = handleVisibilityChange;
        }
    },

    // Safe video play with error handling
    safeVideoPlay: function (video) {
        try {
            if (video && video.paused && video.readyState >= 3) {
                const playPromise = video.play();
                if (playPromise !== undefined) {
                    playPromise.catch(error => {
                        console.warn('Video play failed:', error);
                    });
                }
            }
        } catch (error) {
            console.warn('Error playing video:', error);
        }
    },

    // Safe video pause with error handling
    safeVideoPause: function (video) {
        try {
            if (video && !video.paused) {
                video.pause();
            }
        } catch (error) {
            console.warn('Error pausing video:', error);
        }
    },

    // Check if video is in viewport
    isVideoInViewport: function (video) {
        if (!video) return false;

        try {
            const rect = video.getBoundingClientRect();
            return (
                rect.top < window.innerHeight &&
                rect.bottom > 0 &&
                rect.left < window.innerWidth &&
                rect.right > 0
            );
        } catch (error) {
            return false;
        }
    },

    // Clean up video resources
    cleanupVideo: function (videoElement) {
        try {
            if (!videoElement) return;

            // Remove from tracking
            this.activeVideos.delete(videoElement);

            // Stop and reset video
            this.safeVideoPause(videoElement);

            // Reset video source to free memory
            if (videoElement.src) {
                videoElement.removeAttribute('src');
                videoElement.load();
            }

            // Clean up observer
            const observer = this.observers.get(videoElement);
            if (observer) {
                observer.disconnect();
                this.observers.delete(videoElement);
            }

            // Remove visibility change handler
            if (videoElement.visibilityHandler) {
                document.removeEventListener('visibilitychange', videoElement.visibilityHandler);
                delete videoElement.visibilityHandler;
            }

        } catch (error) {
            console.error('Error cleaning up video:', error);
        }
    },

    // Clean up all videos (called on page unload)
    cleanupAllVideos: function () {
        try {
            this.activeVideos.forEach(video => {
                this.cleanupVideo(video);
            });
            this.activeVideos.clear();
            this.observers.clear();
        } catch (error) {
            console.error('Error cleaning up all videos:', error);
        }
    },

    // Reduce video quality based on connection speed
    adaptVideoQuality: function (videoElement) {
        try {
            if ('connection' in navigator) {
                const connection = navigator.connection;
                const effectiveType = connection.effectiveType;

                if (effectiveType === 'slow-2g' || effectiveType === '2g') {
                    // For slow connections, reduce quality by removing preload
                    videoElement.preload = 'none';
                    console.log('Reduced video preload for slow connection');
                } else if (effectiveType === '3g') {
                    // For 3G, use metadata preload
                    videoElement.preload = 'metadata';
                }
            }
        } catch (error) {
            console.warn('Could not adapt video quality:', error);
        }
    },

    // Initialize all video optimizations
    init: function () {
        // Clean up on page unload
        window.addEventListener('beforeunload', () => {
            this.cleanupAllVideos();
        });

        // Handle page focus/blur for additional memory savings
        window.addEventListener('blur', () => {
            this.activeVideos.forEach(video => {
                this.safeVideoPause(video);
            });
        });

        console.log('Video optimization initialized');
    }
};

// Auto-initialize
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => window.videoOptimization.init());
} else {
    window.videoOptimization.init();
}
