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