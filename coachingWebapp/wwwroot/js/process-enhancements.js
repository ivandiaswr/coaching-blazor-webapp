// Process Page Enhanced Interactions
document.addEventListener('DOMContentLoaded', function () {
    // Initialize animations on scroll
    initScrollAnimations();

    // Enhanced CTA button interactions
    enhanceCTAButton();

    // Smooth scrolling for anchor links
    initSmoothScrolling();

    // Progressive content loading
    initProgressiveLoading();
});

function initScrollAnimations() {
    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('animate-in');

                // Add staggered animation for outcome cards
                if (entry.target.classList.contains('outcomes-grid')) {
                    const cards = entry.target.querySelectorAll('.outcome-card');
                    cards.forEach((card, index) => {
                        setTimeout(() => {
                            card.style.animation = `slideInUp 0.6s ease-out forwards`;
                            card.style.animationDelay = `${index * 0.1}s`;
                        }, 100);
                    });
                }

                // Add staggered animation for journey steps
                if (entry.target.classList.contains('journey-steps')) {
                    const steps = entry.target.querySelectorAll('.journey-step');
                    steps.forEach((step, index) => {
                        setTimeout(() => {
                            step.style.animation = `slideInLeft 0.8s ease-out forwards`;
                            step.style.animationDelay = `${index * 0.2}s`;
                        }, 200);
                    });
                }
            }
        });
    }, {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    });

    // Observe all content sections
    document.querySelectorAll('.content-section').forEach(section => {
        observer.observe(section);
    });

    // Observe specific elements
    document.querySelectorAll('.outcomes-grid, .journey-steps').forEach(element => {
        observer.observe(element);
    });
}

function enhanceCTAButton() {
    const ctaButton = document.querySelector('.cta-button');
    if (ctaButton) {
        // Add ripple effect
        ctaButton.addEventListener('click', function (e) {
            const ripple = document.createElement('span');
            const rect = this.getBoundingClientRect();
            const size = Math.max(rect.width, rect.height);
            const x = e.clientX - rect.left - size / 2;
            const y = e.clientY - rect.top - size / 2;

            ripple.style.width = ripple.style.height = size + 'px';
            ripple.style.left = x + 'px';
            ripple.style.top = y + 'px';
            ripple.classList.add('ripple-effect');

            this.appendChild(ripple);

            setTimeout(() => {
                ripple.remove();
            }, 600);
        });

        // Add hover sound effect (optional - can be enabled)
        ctaButton.addEventListener('mouseenter', function () {
            // Uncomment to add hover sound
            // playHoverSound();
        });
    }
}

function initSmoothScrolling() {
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function (e) {
            e.preventDefault();
            const target = document.querySelector(this.getAttribute('href'));
            if (target) {
                target.scrollIntoView({
                    behavior: 'smooth',
                    block: 'start'
                });
            }
        });
    });
}

function initProgressiveLoading() {
    // Add loading states for images
    const images = document.querySelectorAll('img');
    images.forEach(img => {
        if (!img.complete) {
            img.style.opacity = '0';
            img.style.transition = 'opacity 0.3s ease';

            img.addEventListener('load', function () {
                this.style.opacity = '1';
            });
        }
    });
}

// Utility function for playing hover sounds (optional feature)
function playHoverSound() {
    // Create a subtle hover sound
    const audioContext = new (window.AudioContext || window.webkitAudioContext)();
    const oscillator = audioContext.createOscillator();
    const gainNode = audioContext.createGain();

    oscillator.connect(gainNode);
    gainNode.connect(audioContext.destination);

    oscillator.frequency.setValueAtTime(800, audioContext.currentTime);
    oscillator.frequency.exponentialRampToValueAtTime(600, audioContext.currentTime + 0.1);

    gainNode.gain.setValueAtTime(0, audioContext.currentTime);
    gainNode.gain.linearRampToValueAtTime(0.1, audioContext.currentTime + 0.01);
    gainNode.gain.exponentialRampToValueAtTime(0.001, audioContext.currentTime + 0.1);

    oscillator.start(audioContext.currentTime);
    oscillator.stop(audioContext.currentTime + 0.1);
}

// Add scroll progress indicator
function addScrollProgress() {
    const progressBar = document.createElement('div');
    progressBar.id = 'scroll-progress';
    progressBar.style.cssText = `
        position: fixed;
        top: 0;
        left: 0;
        width: 0%;
        height: 3px;
        background: linear-gradient(90deg, #ff6200, #ff8c42);
        z-index: 9999;
        transition: width 0.1s ease;
    `;
    document.body.appendChild(progressBar);

    window.addEventListener('scroll', () => {
        const winScroll = document.body.scrollTop || document.documentElement.scrollTop;
        const height = document.documentElement.scrollHeight - document.documentElement.clientHeight;
        const scrolled = (winScroll / height) * 100;
        progressBar.style.width = scrolled + '%';
    });
}

// Initialize scroll progress on load
document.addEventListener('DOMContentLoaded', addScrollProgress);