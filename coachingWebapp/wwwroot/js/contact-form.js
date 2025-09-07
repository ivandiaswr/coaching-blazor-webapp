// Contact Form Enhanced Functionality

window.updateFormProgress = function (step, progressWidth) {
    // Update progress bar
    const progressFill = document.getElementById('progress-fill');
    if (progressFill) {
        progressFill.style.width = progressWidth;
    }

    // Update step indicators
    const steps = document.querySelectorAll('.step');
    steps.forEach((stepEl, index) => {
        if (index + 1 <= step) {
            stepEl.classList.add('active');
        } else {
            stepEl.classList.remove('active');
        }
    });

    // Add subtle animation to the active step
    const activeStep = document.getElementById(`step-${step}`);
    if (activeStep) {
        activeStep.style.transform = 'scale(1.05)';
        setTimeout(() => {
            activeStep.style.transform = 'scale(1)';
        }, 200);
    }
};

// Initialize form enhancements when DOM is loaded
document.addEventListener('DOMContentLoaded', function () {

    // Add focus and blur animations to form inputs
    const formInputs = document.querySelectorAll('.enhanced-input input, .enhanced-select, .enhanced-textarea textarea');

    formInputs.forEach(input => {
        input.addEventListener('focus', function () {
            this.closest('.mud-input-root, .mud-select, .mud-paper')?.classList.add('input-focused');
        });

        input.addEventListener('blur', function () {
            this.closest('.mud-input-root, .mud-select, .mud-paper')?.classList.remove('input-focused');
        });
    });

    // Add typewriter effect to form placeholders
    const placeholderInputs = document.querySelectorAll('input[placeholder]');
    placeholderInputs.forEach(input => {
        const originalPlaceholder = input.placeholder;
        let currentIndex = 0;
        let isTyping = true;

        function typewriterEffect() {
            if (isTyping) {
                if (currentIndex < originalPlaceholder.length) {
                    input.placeholder = originalPlaceholder.substring(0, currentIndex + 1);
                    currentIndex++;
                    setTimeout(typewriterEffect, 100);
                } else {
                    isTyping = false;
                    setTimeout(typewriterEffect, 2000);
                }
            } else {
                if (currentIndex > 0) {
                    input.placeholder = originalPlaceholder.substring(0, currentIndex - 1);
                    currentIndex--;
                    setTimeout(typewriterEffect, 50);
                } else {
                    isTyping = true;
                    setTimeout(typewriterEffect, 1000);
                }
            }
        }

        // Start typewriter effect only if input is not focused
        if (document.activeElement !== input) {
            setTimeout(() => typewriterEffect(), Math.random() * 2000);
        }

        input.addEventListener('focus', () => {
            input.placeholder = originalPlaceholder;
        });
    });

    // Add floating label animation
    const inputContainers = document.querySelectorAll('.mud-input-root');
    inputContainers.forEach(container => {
        const input = container.querySelector('input');
        const label = container.querySelector('label');

        if (input && label) {
            function checkFloat() {
                if (input.value !== '' || document.activeElement === input) {
                    label.style.transform = 'translateY(-1.5rem) scale(0.75)';
                    label.style.color = 'var(--accent-orange)';
                } else {
                    label.style.transform = 'translateY(0) scale(1)';
                    label.style.color = '#666';
                }
            }

            input.addEventListener('focus', checkFloat);
            input.addEventListener('blur', checkFloat);
            input.addEventListener('input', checkFloat);

            // Initial check
            checkFloat();
        }
    });

    // Add success confetti effect (simple version)
    window.showSuccessConfetti = function () {
        const confetti = document.createElement('div');
        confetti.style.position = 'fixed';
        confetti.style.top = '50%';
        confetti.style.left = '50%';
        confetti.style.width = '10px';
        confetti.style.height = '10px';
        confetti.style.backgroundColor = 'var(--accent-orange)';
        confetti.style.borderRadius = '50%';
        confetti.style.zIndex = '9999';
        confetti.style.pointerEvents = 'none';

        document.body.appendChild(confetti);

        // Animate confetti
        let x = Math.random() * window.innerWidth;
        let y = Math.random() * window.innerHeight;
        let vx = (Math.random() - 0.5) * 10;
        let vy = (Math.random() - 0.5) * 10;

        const animate = () => {
            x += vx;
            y += vy;
            vy += 0.2; // gravity

            confetti.style.left = x + 'px';
            confetti.style.top = y + 'px';

            if (y > window.innerHeight) {
                document.body.removeChild(confetti);
            } else {
                requestAnimationFrame(animate);
            }
        };

        animate();
    };

    // Create multiple confetti pieces for success
    window.celebrateBooking = function () {
        for (let i = 0; i < 15; i++) {
            setTimeout(() => {
                window.showSuccessConfetti();
            }, i * 100);
        }
    };
});

// Add CSS for enhanced animations
const style = document.createElement('style');
style.textContent = `
    .input-focused {
        transform: translateY(-2px) !important;
        box-shadow: 0 8px 25px rgba(255, 107, 53, 0.15) !important;
    }
    
    .step {
        transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
    }
    
    .mud-input-root label {
        transition: all 0.3s ease;
    }
    
    .typewriter-cursor {
        animation: blink 1s infinite;
    }
    
    @keyframes blink {
        0%, 50% { opacity: 1; }
        51%, 100% { opacity: 0; }
    }
`;

document.head.appendChild(style);
