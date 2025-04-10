﻿document.querySelector('.mobile-menu-btn').addEventListener('click', function() {
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

