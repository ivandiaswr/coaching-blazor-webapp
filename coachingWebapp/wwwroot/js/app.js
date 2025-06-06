document.querySelector('.mobile-menu-btn').addEventListener('click', function() {
    document.querySelector('.nav-ul').classList.toggle('active');
  });

window.scrollToTop = function () {
    // console.log("scrollToTop called");
    requestAnimationFrame(() => {
        try {
            const scrollableContainers = document.querySelectorAll('.mud-table-container, .mud-grid, .mud-tab-panel, main');
            scrollableContainers.forEach(container => {
                container.scrollTop = 0;
            });
            window.scrollTo({
                top: 0,
                left: 0,
                behavior: 'smooth'
            });
        } catch (error) {
            console.error("Error in scrollToTop:", error);
        }
    });
};

window.scrollToFragment = function () {
    // console.log("scrollToFragment called");
    const fragment = window.location.hash;
    if (fragment) {
        requestAnimationFrame(() => {
            try {
                const element = document.querySelector(fragment);
                if (element) {
                    element.scrollIntoView({
                        behavior: 'smooth',
                        block: 'start'
                    });
                } else {
                    console.warn(`Fragment ${fragment} not found in DOM, scrolling to top`);
                    window.scrollToTop();
                }
            } catch (error) {
                console.error("Error in scrollToFragment:", error);
            }
        });
    } else {
        console.log("No fragment, scrolling to top");
        window.scrollToTop();
    }
};

let lastUrl = window.location.href;

document.addEventListener('DOMContentLoaded', () => {
    setTimeout(() => {
        const fragment = window.location.hash;
        if (fragment) {
            window.scrollToFragment();
        } else {
            window.scrollToTop();
        }
    }, 200);

    setInterval(() => {
        const currentUrl = window.location.href;
        if (currentUrl !== lastUrl) {
            console.log(`URL changed from ${lastUrl} to ${currentUrl}`);
            lastUrl = currentUrl;
            setTimeout(() => {
                const fragment = window.location.hash;
                if (fragment) {
                    window.scrollToFragment();
                } else {
                    window.scrollToTop();
                }
            }, 200);
        }
    }, 100);
});

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

window.downloadFile = function(url, fileName, callback) {
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.onerror = function() {
        callback('File not found or inaccessible.');
    };
    link.onclick = function() {
        setTimeout(() => {
            callback(null); // Success
        }, 1000);
    };
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};

window.focusElement = (elementRef) => {
    const container = document.querySelector(`[data-blazor-id="${elementRef.id}"]`);
    if (container) {
        const input = container.querySelector('input');
        if (input) {
            input.focus();
        } else {
            console.error("Input element not found within container:", container);
        }
    } else {
        console.error("Container element not found for ElementReference:", elementRef);
    }
};

window.scrollToBottom = (elementRef) => {
    const element = document.querySelector(`[data-blazor-id="${elementRef.id}"]`);
    if (element) {
        element.scrollTop = element.scrollHeight;
    } else {
        console.error("Element not found for ElementReference:", elementRef);
    }
};

window.VideoCallHelpers = {
    triggerFileInput: function (elementId) {
        const input = document.getElementById(elementId);
        if (input) {
            input.click();
        } else {
            console.warn("File input not found:", elementId);
        }
    }
};

window.scrollCalendarToHour = function (elementId, hour) {
    const el = document.getElementById(elementId);
    if (!el) return;

    el.scrollTop = hour * estimatedPixelsPerHour;
};


// Call login and logout APIs through JS so the cookies are set on client side and not on server side
window.login = function (loginModel) {
    return fetch('/api/account/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(loginModel)
    })
    .then(response => {
        if (response.ok) {
            return response.json().then(data => {
                if (data.role === "Admin") {
                    window.location.href = "/AdminDashboard";
                } else {
                    window.location.href = "/UserDashboard";
                }
                return { success: true };
            });
        } else {
            return response.text().then(text => {
                let errorMsg = "Login failed.";
                try {
                    const parsed = JSON.parse(text);
                    errorMsg = parsed.detail || text;
                } catch {
                    errorMsg = text;
                }
                return { success: false, error: errorMsg };
            });
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
            window.location.href = '/';
        } else {
            return response.text().then(text => {
                return { success: false, error: text };
            });
        }
    });
};

