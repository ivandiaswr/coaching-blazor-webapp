window.detectReload = function() {
    if (performance.navigation.type === 1) {
        document.getElementById('reload-indicator').style.display = 'block';
        setTimeout(() => {
            document.getElementById('reload-indicator').style.display = 'none';
        }, 3000);
    }
}

window.addEventListener('submit', function(e) {
    console.log('Form submitted:', e.target);
});