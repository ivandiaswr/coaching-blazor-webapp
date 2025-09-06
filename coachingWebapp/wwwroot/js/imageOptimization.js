// Image optimization and lazy loading utilities
window.imageOptimization = {
    // Initialize image observers for better memory management
    initImageObserver: function () {
        if ('IntersectionObserver' in window) {
            const imageObserver = new IntersectionObserver((entries, observer) => {
                entries.forEach(entry => {
                    if (entry.isIntersecting) {
                        const img = entry.target;
                        if (img.dataset.src) {
                            img.src = img.dataset.src;
                            img.removeAttribute('data-src');
                        }
                        observer.unobserve(img);
                    }
                });
            }, {
                rootMargin: '50px 0px', // Load images 50px before they're visible
                threshold: 0.01
            });

            // Observe all images with data-src attribute
            document.querySelectorAll('img[data-src]').forEach(img => {
                imageObserver.observe(img);
            });
        }
    },

    // Preload critical images
    preloadCriticalImages: function (imageUrls) {
        if (Array.isArray(imageUrls)) {
            imageUrls.forEach(url => {
                const link = document.createElement('link');
                link.rel = 'preload';
                link.as = 'image';
                link.href = url;
                document.head.appendChild(link);
            });
        }
    },

    // Clean up images to free memory
    cleanupImages: function (containerSelector) {
        try {
            const container = document.querySelector(containerSelector);
            if (container) {
                const images = container.querySelectorAll('img');
                images.forEach(img => {
                    // Reset image source to free memory
                    img.src = 'data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iMSIgaGVpZ2h0PSIxIiB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciPjxyZWN0IHdpZHRoPSIxMDAlIiBoZWlnaHQ9IjEwMCUiIGZpbGw9InRyYW5zcGFyZW50Ii8+PC9zdmc+';
                    img.removeAttribute('srcset');
                });
            }
        } catch (error) {
            console.error('Error cleaning up images:', error);
        }
    },

    // Add error handling to images
    addImageErrorHandling: function () {
        document.addEventListener('error', function (e) {
            if (e.target.tagName === 'IMG') {
                // Replace broken images with a placeholder
                e.target.src = 'data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iMzAwIiBoZWlnaHQ9IjIwMCIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj48cmVjdCB3aWR0aD0iMTAwJSIgaGVpZ2h0PSIxMDAlIiBmaWxsPSIjZjBmMGYwIi8+PHRleHQgeD0iNTAlIiB5PSI1MCUiIGZvbnQtZmFtaWx5PSJBcmlhbCwgc2Fucy1zZXJpZiIgZm9udC1zaXplPSIxNCIgZmlsbD0iIzk5OSIgdGV4dC1hbmNob3I9Im1pZGRsZSIgZHk9Ii4zZW0iPkltYWdlIG5vdCBhdmFpbGFibGU8L3RleHQ+PC9zdmc+';
                e.target.alt = 'Image not available';
                console.warn('Failed to load image:', e.target.dataset.originalSrc || e.target.src);
            }
        }, true);
    },

    // Initialize all optimizations
    init: function () {
        this.initImageObserver();
        this.addImageErrorHandling();

        // Clean up images on page unload
        window.addEventListener('beforeunload', () => {
            this.cleanupImages('body');
        });
    }
};

// Auto-initialize when DOM is ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => window.imageOptimization.init());
} else {
    window.imageOptimization.init();
}
