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

function googleLogin(clientId, onSuccessCallback, onFailureCallback) {
    google.accounts.oauth2.initTokenClient({
        client_id: clientId,
        scope: 'https://www.googleapis.com/auth/calendar https://www.googleapis.com/auth/calendar.events',
        callback: (response) => {
            if (response.error) {
                onFailureCallback(response);
            } else {
                onSuccessCallback(response.access_token);
            }
        },
    }).requestAccessToken();
}
