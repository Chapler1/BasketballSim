function scrollToBottom(element) {
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
}

function toggleNav() {
    var sidebar = document.getElementById('main-sidebar');
    var overlay = document.getElementById('nav-overlay');
    var btn     = document.getElementById('nav-toggle');
    if (!sidebar) return;
    var open = sidebar.classList.toggle('nav-open');
    if (overlay) overlay.classList.toggle('visible', open);
    if (btn)     btn.textContent = open ? '✕' : '☰';
}

function closeNav() {
    var sidebar = document.getElementById('main-sidebar');
    var overlay = document.getElementById('nav-overlay');
    var btn     = document.getElementById('nav-toggle');
    if (sidebar) sidebar.classList.remove('nav-open');
    if (overlay) overlay.classList.remove('visible');
    if (btn)     btn.textContent = '☰';
}
