window.initializeSlider = function (element) {
    try {
        console.log("initializeSlider called");

        if (!element) {
            console.log("Element reference is null or undefined");
            return;
        }

        const scrollSpeed = 0.40;
        let isPaused = false;
        let currentPosition = 0;

        // Calculate width of items and container
        const items = Array.from(element.children);
        const itemWidth = items[0].offsetWidth;
        const gapWidth = 24; // 1.5rem = 24px
        const totalWidth = (itemWidth + gapWidth) * items.length;

        function animate() {
            if (!isPaused) {
                currentPosition -= scrollSpeed;
                
                // Reset position when reaching the end
                if (Math.abs(currentPosition) >= totalWidth / 2) {
                    currentPosition = 0;
                }
                
                element.style.transform = `translateX(${currentPosition}px)`;
            }
            requestAnimationFrame(animate);
        }

        // Start animation loop
        requestAnimationFrame(animate);

        // Pause on hover
        element.addEventListener('mouseenter', () => { isPaused = true; });
        element.addEventListener('mouseleave', () => { isPaused = false; });

        // Adjust on window resize
        window.addEventListener('resize', () => {
            const newTotalWidth = (itemWidth + gapWidth) * items.length;
            if (Math.abs(currentPosition) >= newTotalWidth / 2) {
                currentPosition = 0;
            }
        });

    } catch (error) {
        console.error("Error in initializeSlider:", error);
    }
};
