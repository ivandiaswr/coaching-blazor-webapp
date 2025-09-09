window.colorCalendarSlots = () => {
    const applyStyles = () => {
        const chips = document.querySelectorAll('.mud-cal-cell-template-chip');

        chips.forEach(chip => {
            const content = chip.querySelector('.mud-chip-content')?.textContent ?? "";

            // Remove default MudBlazor styling
            chip.classList.remove('mud-chip-color-primary');
            chip.classList.remove('mud-chip-filled');

            // Apply enhanced styling based on slot type
            if (content.includes("🟢")) {
                chip.classList.add("calendar-slot-available");
                chip.style.backgroundColor = "rgba(76, 175, 80, 0.15)";
                chip.style.border = "2px solid rgba(76, 175, 80, 0.4)";
                chip.style.cursor = "pointer";

                // Add hover effect
                chip.addEventListener('mouseenter', () => {
                    chip.style.backgroundColor = "rgba(76, 175, 80, 0.25)";
                    chip.style.borderColor = "rgba(76, 175, 80, 0.6)";
                    chip.style.transform = "scale(1.02)";
                    chip.style.boxShadow = "0 4px 8px rgba(76, 175, 80, 0.2)";
                });

                chip.addEventListener('mouseleave', () => {
                    chip.style.backgroundColor = "rgba(76, 175, 80, 0.15)";
                    chip.style.borderColor = "rgba(76, 175, 80, 0.4)";
                    chip.style.transform = "scale(1)";
                    chip.style.boxShadow = "none";
                });
            }
            else if (content.includes("🔴")) {
                chip.classList.add("calendar-slot-unavailable");
                chip.style.backgroundColor = "rgba(244, 67, 54, 0.15)";
                chip.style.border = "2px solid rgba(244, 67, 54, 0.4)";
                chip.style.cursor = "not-allowed";
            }
            else if (content.includes("⛔")) {
                chip.classList.add("calendar-slot-busy");
                chip.style.backgroundColor = "rgba(255, 152, 0, 0.15)";
                chip.style.border = "2px solid rgba(255, 152, 0, 0.4)";
                chip.style.cursor = "not-allowed";
            }

            // Common styling
            chip.style.color = "#1a1a1a";
            chip.style.borderRadius = "8px";
            chip.style.padding = "8px 12px";
            chip.style.transition = "all 0.2s ease";
            chip.style.fontWeight = "600";
            chip.style.fontSize = "13px";
            chip.style.minHeight = "36px";
            chip.style.display = "flex";
            chip.style.alignItems = "center";
            chip.style.justifyContent = "center";
        });
    };

    const calendarRoot = document.querySelector('.mud-calendar');
    if (!calendarRoot) return;

    // Initial application
    applyStyles();

    // Watch for calendar updates
    const observer = new MutationObserver((mutationsList, observer) => {
        let shouldUpdate = false;
        for (const mutation of mutationsList) {
            if (mutation.type === 'childList' &&
                (mutation.addedNodes.length > 0 || mutation.removedNodes.length > 0)) {
                shouldUpdate = true;
                break;
            }
        }
        if (shouldUpdate) {
            // Debounce updates to avoid excessive calls
            setTimeout(applyStyles, 100);
        }
    });

    observer.observe(calendarRoot, {
        childList: true,
        subtree: true,
    });
};

// Enhanced scroll to hour function
window.scrollCalendarToHour = (calendarId, hour) => {
    try {
        const calendar = document.getElementById(calendarId);
        if (!calendar) return;

        // Find the hour row and scroll to it
        const hourElements = calendar.querySelectorAll('.mud-calendar-hour');
        const targetHour = Array.from(hourElements).find(el => {
            const timeText = el.textContent || '';
            return timeText.includes(`${hour}:00`) || timeText.includes(`${hour} AM`) || timeText.includes(`${hour} PM`);
        });

        if (targetHour) {
            targetHour.scrollIntoView({
                behavior: 'smooth',
                block: 'start'
            });
        } else {
            // Fallback: scroll to approximate position
            const scrollPosition = (hour - 8) * 60; // Assuming 60px per hour
            calendar.scrollTop = Math.max(0, scrollPosition);
        }
    } catch (error) {
        console.warn('Could not scroll to hour:', error);
    }
};

// Auto-focus available slots when calendar loads
window.focusAvailableSlots = () => {
    try {
        const availableSlots = document.querySelectorAll('.calendar-slot-available');
        if (availableSlots.length > 0) {
            // Add a subtle pulse animation to the first few available slots
            availableSlots.forEach((slot, index) => {
                if (index < 3) { // Only highlight first 3 slots
                    setTimeout(() => {
                        slot.style.animation = 'subtle-pulse 2s ease-in-out';
                    }, index * 500);
                }
            });
        }
    } catch (error) {
        console.warn('Could not focus available slots:', error);
    }
};

// Add CSS animation for subtle pulse and mobile enhancements
if (!document.getElementById('calendar-animations')) {
    const style = document.createElement('style');
    style.id = 'calendar-animations';
    style.textContent = `
        @keyframes subtle-pulse {
            0%, 100% { 
                box-shadow: 0 0 0 0 rgba(76, 175, 80, 0.4);
            }
            50% { 
                box-shadow: 0 0 0 10px rgba(76, 175, 80, 0);
            }
        }
        
        @keyframes mobile-tap {
            0% { transform: scale(1); }
            50% { transform: scale(0.95); }
            100% { transform: scale(1); }
        }
        
        .calendar-slot-available {
            position: relative;
        }
        
        .calendar-slot-available:after {
            content: '';
            position: absolute;
            top: -2px;
            left: -2px;
            right: -2px;
            bottom: -2px;
            background: linear-gradient(45deg, rgba(76, 175, 80, 0.2), rgba(76, 175, 80, 0.1));
            border-radius: 10px;
            z-index: -1;
            opacity: 0;
            transition: opacity 0.3s ease;
        }
        
        .calendar-slot-available:hover:after {
            opacity: 1;
        }
        
        /* Mobile touch improvements */
        .time-slot-btn {
            -webkit-tap-highlight-color: transparent;
            touch-action: manipulation;
            user-select: none;
        }
        
        .time-slot-btn:active {
            animation: mobile-tap 0.1s ease;
        }
        
        .time-slots-grid {
            touch-action: pan-y;
        }
        
        /* Better scrolling for mobile */
        .time-slots-section {
            -webkit-overflow-scrolling: touch;
            overflow-x: hidden;
        }
        
        /* Loading state improvements */
        .slots-loading {
            min-height: 100px;
        }
        
        /* Focus improvements for accessibility */
        .time-slot-btn:focus {
            outline: 3px solid rgba(102, 126, 234, 0.5);
            outline-offset: 2px;
        }
        
        .time-slot-btn:focus:not(:focus-visible) {
            outline: none;
        }
        
        /* Dynamic scroll indicators */
        .time-slots-section.has-scroll:not(.at-top)::before {
            opacity: 1;
        }
        
        .time-slots-section.has-scroll.at-top::before {
            opacity: 0;
        }
        
        .time-slots-section.has-scroll:not(.at-bottom)::after {
            opacity: 1;
        }
        
        .time-slots-section.has-scroll.at-bottom::after {
            opacity: 0;
        }
        
        /* Mobile scroll hint */
        @media (max-width: 768px) {
            .time-slots-section.has-scroll::after {
                background: linear-gradient(to top, 
                    rgba(255,255,255,1) 0%, 
                    rgba(255,255,255,0.8) 60%,
                    rgba(255,255,255,0) 100%);
                height: 30px;
            }
        }
    `;
    document.head.appendChild(style);
}

// Mobile touch enhancements
window.initMobileCalendarEnhancements = () => {
    try {
        // Add touch feedback to time slot buttons
        const addTouchFeedback = () => {
            const timeSlotButtons = document.querySelectorAll('.time-slot-btn');
            timeSlotButtons.forEach(button => {
                // Remove any existing listeners to prevent duplicates
                button.removeEventListener('touchstart', handleTouchStart);
                button.removeEventListener('touchend', handleTouchEnd);
                
                // Add touch event listeners
                button.addEventListener('touchstart', handleTouchStart, { passive: true });
                button.addEventListener('touchend', handleTouchEnd, { passive: true });
            });
        };
        
        const handleTouchStart = (e) => {
            const button = e.currentTarget;
            if (!button.disabled) {
                button.style.transform = 'scale(0.95)';
                button.style.transition = 'transform 0.1s ease';
            }
        };
        
        const handleTouchEnd = (e) => {
            const button = e.currentTarget;
            setTimeout(() => {
                button.style.transform = 'scale(1)';
            }, 100);
        };
        
        // Check if time slots section needs scroll indicators
        const checkScrollable = () => {
            const timeSlotsSection = document.querySelector('.time-slots-section');
            if (timeSlotsSection) {
                const isScrollable = timeSlotsSection.scrollHeight > timeSlotsSection.clientHeight;
                
                if (isScrollable) {
                    timeSlotsSection.classList.add('has-scroll');
                    
                    // Add scroll event listener to show/hide scroll indicators
                    timeSlotsSection.addEventListener('scroll', () => {
                        const { scrollTop, scrollHeight, clientHeight } = timeSlotsSection;
                        const isAtTop = scrollTop === 0;
                        const isAtBottom = scrollTop + clientHeight >= scrollHeight - 5;
                        
                        timeSlotsSection.classList.toggle('at-top', isAtTop);
                        timeSlotsSection.classList.toggle('at-bottom', isAtBottom);
                    });
                    
                    // Initial check
                    timeSlotsSection.classList.add('at-top');
                } else {
                    timeSlotsSection.classList.remove('has-scroll');
                }
            }
        };
        
        // Initialize touch feedback and scroll indicators
        addTouchFeedback();
        checkScrollable();
        
        // Re-apply when time slots are updated
        const observer = new MutationObserver(() => {
            setTimeout(() => {
                addTouchFeedback();
                checkScrollable();
            }, 100);
        });
        
        const timeSlotsSection = document.querySelector('.time-slots-section');
        if (timeSlotsSection) {
            observer.observe(timeSlotsSection, {
                childList: true,
                subtree: true
            });
        }
        
        // Prevent double-tap zoom on time slot buttons
        document.addEventListener('touchstart', (e) => {
            if (e.target.closest('.time-slot-btn')) {
                e.preventDefault();
            }
        }, { passive: false });
        
        // Improve scrolling behavior
        const modalContent = document.querySelector('.calendar-modal-content');
        if (modalContent) {
            modalContent.style.touchAction = 'pan-y';
        }
        
    } catch (error) {
        console.warn('Could not initialize mobile calendar enhancements:', error);
    }
};

// Auto-detect mobile and apply enhancements
window.isMobileDevice = () => {
    return (typeof window.orientation !== "undefined") || (navigator.userAgent.indexOf('IEMobile') !== -1);
};

// Enhanced viewport detection
window.getViewportInfo = () => {
    return {
        width: Math.max(document.documentElement.clientWidth || 0, window.innerWidth || 0),
        height: Math.max(document.documentElement.clientHeight || 0, window.innerHeight || 0),
        isMobile: window.innerWidth <= 768,
        isSmallMobile: window.innerWidth <= 480,
        isTouchDevice: 'ontouchstart' in window || navigator.maxTouchPoints > 0
    };
};

// Initialize mobile enhancements when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    if (window.isMobileDevice() || window.getViewportInfo().isMobile) {
        window.initMobileCalendarEnhancements();
    }
});

// Also initialize when calendar becomes visible
window.addEventListener('resize', () => {
    const viewport = window.getViewportInfo();
    if (viewport.isMobile || viewport.isTouchDevice) {
        window.initMobileCalendarEnhancements();
    }
});

// Smooth scroll polyfill for older browsers
if (!('scrollBehavior' in document.documentElement.style)) {
    window.smoothScrollPolyfill = (element, options) => {
        const start = element.scrollTop;
        const target = options.top;
        const distance = target - start;
        const duration = 300;
        let currentTime = 0;
        const increment = 20;

        const animateScroll = () => {
            currentTime += increment;
            const val = easeInOutQuad(currentTime, start, distance, duration);
            element.scrollTop = val;
            if (currentTime < duration) {
                setTimeout(animateScroll, increment);
            }
        };
        
        const easeInOutQuad = (t, b, c, d) => {
            t /= d / 2;
            if (t < 1) return c / 2 * t * t + b;
            t--;
            return -c / 2 * (t * (t - 2) - 1) + b;
        };
        
        animateScroll();
    };
}
