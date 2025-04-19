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