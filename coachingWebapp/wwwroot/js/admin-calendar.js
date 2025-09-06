window.colorAdminCalendarSessions = () => {
    const applyStyles = () => {
        const chips = document.querySelectorAll('.mud-cal-cell-template-chip');

        chips.forEach(chip => {
            const content = chip.querySelector('.mud-chip-content')?.textContent ?? "";

            // Remove default MudBlazor classes
            chip.classList.remove('mud-chip-color-primary');
            chip.classList.remove('mud-chip-filled');

            if (content.includes("🟢")) {
                // Green for completed sessions
                chip.style.backgroundColor = "rgba(76, 175, 80, 0.2)";
                chip.style.border = "1px solid rgba(76, 175, 80, 0.6)";
                chip.style.color = "#2e7d32";
                chip.setAttribute('data-session-status', 'completed');
            }
            else if (content.includes("🔴")) {
                // Red for missed/no-show sessions
                chip.style.backgroundColor = "rgba(244, 67, 54, 0.2)";
                chip.style.border = "1px solid rgba(244, 67, 54, 0.6)";
                chip.style.color = "#c62828";
                chip.setAttribute('data-session-status', 'missed');
            }
            else if (content.includes("🟡")) {
                // Yellow for unscheduled requests
                chip.style.backgroundColor = "rgba(255, 193, 7, 0.2)";
                chip.style.border = "1px solid rgba(255, 193, 7, 0.6)";
                chip.style.color = "#f57c00";
                chip.setAttribute('data-session-status', 'unscheduled');
            }
            else if (content.includes("⚪")) {
                // Light gray for upcoming sessions
                chip.style.backgroundColor = "rgba(158, 158, 158, 0.15)";
                chip.style.border = "1px solid rgba(158, 158, 158, 0.5)";
                chip.style.color = "#616161";
                chip.setAttribute('data-session-status', 'upcoming');
            }

            // Common styles for all sessions
            chip.style.borderRadius = "6px";
            chip.style.padding = "6px 10px";
            chip.style.transition = "all 0.2s ease";
            chip.style.fontWeight = "500";
            chip.style.cursor = "pointer";
        });
    };

    const calendarRoot = document.querySelector('#adminDashboard .mud-calendar');
    if (!calendarRoot) return;

    applyStyles();

    // Set up observer to re-apply styles when calendar content changes
    const observer = new MutationObserver((mutationsList) => {
        applyStyles();
    });

    observer.observe(calendarRoot, {
        childList: true,
        subtree: true,
    });
};
