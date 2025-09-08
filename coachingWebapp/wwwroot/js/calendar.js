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

// Add CSS animation for subtle pulse
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
    `;
    document.head.appendChild(style);
}
