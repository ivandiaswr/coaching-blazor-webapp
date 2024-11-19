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
        let direction = -1; // -1 for left, 1 for right

        // Calculate width of items and container
        const items = Array.from(element.children);
        const itemWidth = items[0].offsetWidth;
        const gapWidth = 24; // 1.5rem = 24px
        const totalItems = items.length;
        const totalWidth = (itemWidth + gapWidth) * totalItems;
        const containerWidth = element.parentElement.offsetWidth;

        function animate() {
            if (!isPaused) {
                currentPosition += scrollSpeed * direction;

                // Reverse direction when reaching the ends
                if (currentPosition <= containerWidth - totalWidth) {
                    direction = 1; // Change direction to right
                    currentPosition = containerWidth - totalWidth; // Correct position
                } else if (currentPosition >= 0) {
                    direction = -1; // Change direction to left
                    currentPosition = 0; // Correct position
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
            // Recalculate widths on resize
            const newItemWidth = items[0].offsetWidth;
            const newTotalWidth = (newItemWidth + gapWidth) * totalItems;
            const newContainerWidth = element.parentElement.offsetWidth;

            // Update positions
            currentPosition = (currentPosition / totalWidth) * newTotalWidth;
            totalWidth = newTotalWidth;
            containerWidth = newContainerWidth;

            // Ensure currentPosition is within bounds
            if (currentPosition < containerWidth - totalWidth) {
                currentPosition = containerWidth - totalWidth;
                direction = 1;
            } else if (currentPosition > 0) {
                currentPosition = 0;
                direction = -1;
            }
        });

    } catch (error) {
        console.error("Error in initializeSlider:", error);
    }
};
