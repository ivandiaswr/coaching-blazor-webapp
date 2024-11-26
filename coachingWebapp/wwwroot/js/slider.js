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

        // Get precise item width including margins
        const itemStyle = getComputedStyle(items[0]);
        const itemWidth = items[0].offsetWidth + parseFloat(itemStyle.marginLeft) + parseFloat(itemStyle.marginRight);

        const totalItems = items.length;
        const gapWidth = 0; // Already included in itemWidth
        let totalWidth = itemWidth * totalItems;
        let containerWidth = element.parentElement.offsetWidth;

        function animate() {
            if (!isPaused) {
                currentPosition += scrollSpeed * direction;

                // Reverse direction when the last item is fully visible
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
            containerWidth = element.parentElement.offsetWidth;

            // Ensure currentPosition is within bounds
            if (currentPosition <= containerWidth - totalWidth) {
                currentPosition = containerWidth - totalWidth;
                direction = 1;
            } else if (currentPosition >= 0) {
                currentPosition = 0;
                direction = -1;
            }
        });

    } catch (error) {
        console.error("Error in initializeSlider:", error);
    }
};
