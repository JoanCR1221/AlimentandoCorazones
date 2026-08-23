export function loadSettings(key) {
    try {
        const raw = localStorage.getItem(key);
        if (!raw) {
            return null;
        }
        return JSON.parse(raw);
    } catch {
        return null;
    }
}

export function saveSettings(key, settings) {
    localStorage.setItem(key, JSON.stringify(settings));
    applyAppearance(settings);
}

export function applyAppearance(settings) {
    const html = document.documentElement;
    html.setAttribute('data-escala', settings.escala ?? 'normal');
    html.setAttribute('data-alto-contraste', settings.altoContraste ? 'true' : 'false');
    html.setAttribute('data-theme', settings.esOscuro ? 'dark' : 'light');
    html.lang = 'es';
}
