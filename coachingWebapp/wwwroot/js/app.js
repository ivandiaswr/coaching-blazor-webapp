document.querySelector('.mobile-menu-btn').addEventListener('click', function() {
    document.querySelector('.nav-ul').classList.toggle('active');
  });

window.scrollToFragment = () => {
    const hash = window.location.hash;
    if (hash) {
        const element = document.getElementById(hash.substring(1));
        if (element) {
            element.scrollIntoView({ behavior: 'smooth' });
        }
    }
}

// Call login and logout APIs through JS so the cookies are set on client side and not on server side
window.login = function (loginModel) {
    return fetch('/api/account/login', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(loginModel)
    }).then(response => {
        if (response.ok) {
            return { success: true };
        } else {
            return response.text().then(text => { return { success: false, error: text }; });
        }
    });
};

window.logout = function () {
    return fetch('/api/account/logout', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        }
    }).then(response => {
        if (response.ok) {
            return { success: true };
        } else {
            return response.text().then(text => { return { success: false, error: text }; });
        }
    });
};

function setTitle(title) {
    document.title = title;
}

function navigateToAbout() {
    window.location.href = "/about/meet-itala";
}

function saveAsFile(fileName, contentBase64) {
    const link = document.createElement('a');
    link.download = fileName;
    link.href = "data:application/vnd.openxmlformats-officedocument.spreadsheetml.sheet;base64," + contentBase64;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
}

function downloadGift(url) {
    const link = document.createElement('a');
    const urlObject = new URL(url, window.location.origin);
    const downloadName = urlObject.searchParams.get('downloadName');
    link.href = urlObject.origin + urlObject.pathname;
    link.download = downloadName || url.split('/').pop(); 
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
}

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
