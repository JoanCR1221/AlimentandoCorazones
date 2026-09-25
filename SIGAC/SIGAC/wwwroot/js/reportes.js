// Descarga e impresión de los archivos que arma IExportacionService (PDF/Excel).
// Blazor Server no puede escribir en el disco del cliente por su cuenta: el
// archivo llega como base64 y estas funciones son las que de verdad lo bajan
// o lo mandan a imprimir en el navegador.

function aBytes(base64) {
    const binario = atob(base64);
    const bytes = new Uint8Array(binario.length);
    for (let i = 0; i < binario.length; i++) {
        bytes[i] = binario.charCodeAt(i);
    }
    return bytes;
}

export function descargarArchivo(nombreArchivo, tipoMime, base64) {
    const blob = new Blob([aBytes(base64)], { type: tipoMime });
    const url = URL.createObjectURL(blob);

    const enlace = document.createElement('a');
    enlace.href = url;
    enlace.download = nombreArchivo;
    document.body.appendChild(enlace);
    enlace.click();
    document.body.removeChild(enlace);

    URL.revokeObjectURL(url);
}

// El @onclick de Blazor Server viaja al servidor antes de poder volver a
// llamar acá: para cuando imprimirPdf() corre, ya no hay "gesto de usuario"
// reciente y window.open() se bloquearía. Por eso el botón de imprimir se
// engancha con un listener nativo, fuera del ciclo de eventos de Blazor, que
// sí corre de forma síncrona sobre el clic real y abre la pestaña en blanco
// a tiempo; imprimirPdf() solo le carga el PDF una vez que ya está listo.
export function engancharBotonImprimir(idBoton) {
    const boton = document.getElementById(idBoton);
    if (boton) {
        boton.addEventListener('click', () => {
            window.__ventanaImpresionReporte = window.open('', '_blank');
        });
    }
}

export function imprimirPdf(base64) {
    const blob = new Blob([aBytes(base64)], { type: 'application/pdf' });
    const url = URL.createObjectURL(blob);

    const ventana = window.__ventanaImpresionReporte;
    window.__ventanaImpresionReporte = null;

    if (!ventana || ventana.closed) {
        return;
    }

    ventana.location = url;

    // No hay evento "listo para imprimir" fiable entre navegadores para un PDF
    // cargado así; un margen corto antes de llamar a print() es lo que
    // recomienda la propia documentación de MDN para este caso.
    setTimeout(() => ventana.print(), 500);
}
