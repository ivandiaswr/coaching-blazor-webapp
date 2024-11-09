window.initializeSlider = function (element) {
    try {
        console.log("initializeSlider called");

        if (!element) {
            console.log("Element reference is null or undefined");
            return;
        }

        const scrollSpeed = 0.75; // Slightly slower for better readability
        let isPaused = false;
        let currentPosition = 0;

        // Calculate the width of a single set of items
        const items = element.children;
        const itemCount = items.length / 2;
        const itemWidth = items[0].offsetWidth;
        const gapWidth = 24; // 1.5rem = 24px (updated gap)
        const singleSetWidth = (itemWidth + gapWidth) * itemCount;

        function animate() {
            if (!isPaused) {
                currentPosition -= scrollSpeed;
                
                if (Math.abs(currentPosition) >= singleSetWidth) {
                    currentPosition += singleSetWidth;
                }
                
                element.style.transform = `translateX(${currentPosition}px)`;
            }
            requestAnimationFrame(animate);
        }

        // Ensure we have enough duplicated items
        const originalContent = element.innerHTML;
        element.innerHTML = originalContent + originalContent;

        // Start the animation
        requestAnimationFrame(animate);

        // Pause on hover with smooth transition
        element.addEventListener('mouseenter', () => {
            isPaused = true;
        });
        element.addEventListener('mouseleave', () => {
            isPaused = false;
        });

        // Handle window resize
        window.addEventListener('resize', () => {
            const newItemWidth = items[0].offsetWidth;
            const newSingleSetWidth = (newItemWidth + gapWidth) * itemCount;
            if (Math.abs(currentPosition) >= newSingleSetWidth) {
                currentPosition = 0;
            }
        });

    } catch (error) {
        console.error("Error in initializeSlider:", error);
    }
};