// Shared layout chrome: navbar and footer live here so pages do not redefine them.
// Pages include <div id="appNavbar"></div> and <div id="appFooter"></div>.
(function () {
    const pages = [
        { href: '/', label: 'Home', icon: 'bi-house-door' },
        { href: '/nodes.html', label: 'Nodes', icon: 'bi-hdd-network' },
        { href: '/pools.html', label: 'Pools', icon: 'bi-diagram-3' },
        { href: '/tests.html', label: 'Tests', icon: 'bi-activity' },
        { href: '/queue.html', label: 'Queue', icon: 'bi-list-check' },
        { href: '/server.html', label: 'Server', icon: 'bi-server' },
        { href: '/settings.html', label: 'Settings', icon: 'bi-gear' }
    ];

    const file = location.pathname.split('/').pop();
    const current = file === '' ? '/' : '/' + file;

    const links = pages.map(function (p) {
        const active = p.href === current;
        return '<li class="nav-item">' +
            '<a class="nav-link' + (active ? ' active' : '') + '"' + (active ? ' aria-current="page"' : '') +
            ' href="' + p.href + '"><i class="bi ' + p.icon + ' me-1"></i>' + p.label + '</a></li>';
    }).join('');

    const navbar = document.getElementById('appNavbar');
    if (navbar) {
        navbar.innerHTML =
            '<nav class="navbar navbar-expand-lg sticky-top bg-dark" data-bs-theme="dark">' +
            '  <div class="container">' +
            '    <a class="navbar-brand fw-semibold" href="/"><img src="/logo-32-inverted.png" alt="" width="24" height="24" class="me-2">Obicon</a>' +
            '    <button class="navbar-toggler" type="button" data-bs-toggle="collapse" data-bs-target="#mainNav"' +
            '            aria-controls="mainNav" aria-expanded="false" aria-label="Toggle navigation">' +
            '      <span class="navbar-toggler-icon"></span>' +
            '    </button>' +
            '    <div class="collapse navbar-collapse" id="mainNav">' +
            '      <ul class="navbar-nav">' + links + '</ul>' +
            '    </div>' +
            '  </div>' +
            '</nav>';
    }

    const footer = document.getElementById('appFooter');
    if (footer) {
        footer.innerHTML =
            '<footer class="py-4 mt-auto text-muted small">' +
            '  <div class="container d-flex justify-content-between">' +
            '    <span>Obicon &mdash; synthetic API monitoring</span>' +
            '    <span><a href="/server.html" class="link-secondary">Server</a> &middot; <a href="/settings.html" class="link-secondary">Settings</a></span>' +
            '  </div>' +
            '</footer>';
    }
})();
