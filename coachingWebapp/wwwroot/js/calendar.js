window.colorCalendarSlots = () => {
    const applyStyles = () => {
        const chips = document.querySelectorAll('.mud-cal-cell-template-chip');

        chips.forEach(chip => {
            const content = chip.querySelector('.mud-chip-content')?.textContent ?? "";

            chip.classList.remove('mud-chip-color-primary');
            chip.classList.remove('mud-chip-filled');

            if (content.includes("🟢")) {
                chip.classList.add("calendar-slot-available");
                chip.style.backgroundColor = "rgba(76, 175, 80, 0.15)";
                chip.style.border = "1px solid rgba(76, 175, 80, 0.4)";
            }
            else if (content.includes("🔴")) {
                chip.classList.add("calendar-slot-unavailable");
                chip.style.backgroundColor = "rgba(244, 67, 54, 0.15)";
                chip.style.border = "1px solid rgba(244, 67, 54, 0.4)";
            }
            else if (content.includes("⛔")) {
                chip.classList.add("calendar-slot-busy");
                chip.style.backgroundColor = "rgba(255, 152, 0, 0.15)";
                chip.style.border = "1px solid rgba(255, 152, 0, 0.4)";
            }

            chip.style.color = "#1a1a1a";
            chip.style.borderRadius = "6px";
            chip.style.padding = "6px 10px";
            chip.style.transition = "background-color 0.2s ease";
        });
    };

    const calendarRoot = document.querySelector('.mud-calendar');
    if (!calendarRoot) return;

    // Aplica logo à primeira
    applyStyles();

    // Observa mudanças no DOM do calendário
    const observer = new MutationObserver((mutationsList, observer) => {
        applyStyles();
    });

    observer.observe(calendarRoot, {
        childList: true,
        subtree: true,
    });
};
