# Prompt maestro: diseño responsivo de SIGAC (móvil y tableta)

> Copie todo lo que está dentro del bloque de abajo y péguelo en una conversación nueva.
> Al final, en **"Sección a ajustar"**, indique el módulo o la pantalla (o pegue el código).
> Conviene trabajar **un módulo a la vez** (por ejemplo: primero el layout, luego Beneficiarios, luego Reportes).

---

```text
Actúa como un Desarrollador Frontend Senior y Experto en Diseño UI/UX. Tu objetivo es
optimizar el diseño visual de SIGAC (Sistema de Gestión de Alimentando Corazones) para
que se vea y se use perfectamente en teléfonos y tabletas, sin empeorar la vista de
escritorio.

## Contexto del proyecto (respétalo)

- Tecnología: Blazor (.NET 10, componentes .razor) + MudBlazor 9.8. NO uses Tailwind ni
  reemplaces MudBlazor: trabaja con sus componentes y propiedades (MudGrid/MudItem con
  xs/sm/md, MudHidden, Breakpoint, Dense, FullScreen en diálogos, etc.).
- Estilos globales en `wwwroot/app.css`, con tokens de color en `:root` (prefijo `--ac-`)
  y estilos aislados por componente en archivos `.razor.css` (con `::deep` cuando toca
  elementos internos de MudBlazor).
- La app tiene tres modos de apariencia: claro, oscuro (`html[data-theme="dark"]`) y alto
  contraste (`html[data-alto-contraste="true"]`). Todo cambio debe funcionar en los tres:
  usa los tokens `--ac-*` existentes, nunca colores fijos nuevos.
- Ya existe un `@media (pointer: coarse)` en app.css con objetivos táctiles de 44px:
  súbelo a 48px en lugar de duplicar la regla.
- Accesibilidad que ya está y no debe romperse: enlace "Saltar al contenido principal",
  foco visible, `aria-label` en botones de icono, jerarquía de encabezados (un solo h1 por
  pantalla), `prefers-reduced-motion`.
- Idioma de la interfaz y de los comentarios: español. Mantén la densidad y el estilo de
  los comentarios del código existente (explican el porqué, no el qué).

## Piezas clave de la interfaz

- Layout: `MainLayout.razor` (MudAppBar con logo, SesionToolbar y AparienciaToolbar;
  MudDrawer con `Variant="Responsive"` y NavMenu; MigasPan sobre el contenido).
- Encabezado de cada pantalla: `EncabezadoPagina` (título, descripción y botón de acción).
- Tarjetas de resumen (KPIs): `.ac-kpis` / `TarjetaResumen`.
- Filtros: MudTextField/MudSelect apilados dentro de un MudCard sobre la grilla.
- Listados: MudDataGrid con `ServerData` y paginación del servidor (unas 20 pantallas).
- Formularios: pantallas `Registrar*` / `Editar*` y componentes `Campos*`.
- Diálogos: `*Dialog.razor` (anular, cancelar, cambiar rol, restablecer contraseña...).
- Reportes: pantallas `Panorama*` y `Reporte*` con gráficas y `AccionesReporte`.
- Otros: `CalendarioAlquileres`, `PhoneInput`, `Login`, `Bienvenida`.

## Pautas técnicas (obligatorias)

1. Mobile-first: estilos base para pantalla pequeña y media queries progresivas con
   `min-width` para tableta y escritorio.
2. Breakpoints: móvil < 768px; tableta 768px–1024px (vertical y horizontal); escritorio
   > 1024px. Ten en cuenta que los breakpoints de MudBlazor son distintos
   (xs < 600, sm 600–959, md 960–1279, lg ≥ 1280) y que el MudDrawer responsivo cambia de
   modo en md. Cuando uses propiedades de MudBlazor, elige el punto más cercano y dime
   cuál usaste; no mezcles criterios sin avisar.
3. Tipografía: texto de cuerpo mínimo 16px (también en inputs, para que iOS no haga zoom
   al enfocar). Escala h1/h2/h3 con `clamp()` para que los títulos largos no rompan el
   diseño ni desborden.
4. Zonas de toque: todo elemento interactivo (botones, enlaces, iconos, chips clicables,
   filas con acción, paginador) mide mínimo 48x48px y tiene al menos 8px de separación
   con el vecino.
5. Navegación:
   - Móvil: el menú lateral se abre con el botón hamburguesa y se cierra solo al elegir
     una opción o tocar fuera; debe arrancar cerrado en móvil (hoy `_drawerOpen = true`).
   - La barra superior no puede desbordarse: en móvil oculta el lema, reduce el bloque del
     usuario a solo el avatar y agrupa las opciones de apariencia en un menú.
   - Tableta: decide (y justifica) entre menú colapsable o menú fijo estrecho.
6. Rejillas y columnas: una columna en móvil; dos o tres en tableta según el espacio.
   Aplica esto a KPIs, filtros (en tableta, filtros en 2 columnas), formularios y
   tarjetas de reportes.
7. Listados (lo más importante: que la información se lea bien):
   - Móvil: no muestres la tabla de 7+ columnas. Convierte cada fila en una tarjeta con el
     dato principal destacado (p. ej. nombre), los datos secundarios como pares
     etiqueta: valor, el estado como chip y las acciones al pie de la tarjeta con botones
     de 48px. Verifica si MudDataGrid 9.8 trae soporte para apilar filas; si no, usa
     CSS sobre la grilla o una vista alterna con MudHidden, pero sin duplicar la carga de
     datos del servidor.
   - Tableta: tabla con las columnas esenciales; las secundarias se ocultan o pasan a un
     detalle expandible.
   - El paginador debe caber en una línea en móvil (simplifícalo si hace falta).
   - Fechas, montos y cantidades siguen alineados con `tabular-nums`.
8. Formularios: un campo por fila en móvil; botones principales a todo el ancho y en el
   orden lógico (acción principal primero en el flujo de lectura). Usa el `inputmode` y
   `type` correctos (numérico, teléfono, correo, fecha).
9. Diálogos: pantalla completa en móvil (`FullScreen` o `MaxWidth` adecuado según el
   ancho), con los botones siempre visibles sin tener que hacer scroll largo.
10. Medios y gráficas: imágenes, gráficas, videos y contenedores con `max-width: 100%`,
    manteniendo proporción con `aspect-ratio` u `object-fit`. Las gráficas deben
    redimensionarse con el contenedor y sus leyendas no deben tapar los datos.
11. Desbordamiento: cero scroll horizontal de la página. Corrige la causa (anchos fijos,
    `min-width`, textos largos sin `overflow-wrap: anywhere`, márgenes negativos). Usa
    `overflow-x: hidden` solo como red de seguridad y nunca en un contenedor que tenga
    elementos `position: sticky`. Si una tabla de verdad no puede apilarse, el scroll
    horizontal va dentro de su propio contenedor, nunca en la página.
12. Espaciado: margen lateral de 16px en móvil y 24px en tableta; reduce los paddings
    grandes de MudCard (`pa-4`) en móvil.

## Cómo quiero que trabajes

- Antes de escribir código, si algo es ambiguo (qué columnas son esenciales, qué acción
  es la principal, si un cambio afecta a otras pantallas), hazme las preguntas necesarias
  en una lista corta. No inventes reglas de negocio.
- Prioriza cambios globales y reutilizables (app.css, componentes compartidos como
  EncabezadoPagina o TarjetaResumen) antes que parches pantalla por pantalla.
- No cambies lógica, servicios, permisos ni consultas: solo presentación.
- Revisa el resultado en estos anchos: 360px, 390px, 768px, 820px, 1024px y 1280px; en
  claro, oscuro y alto contraste.

## Qué debes entregarme

1. El código corregido (archivos .razor, .razor.css y/o app.css), indicando la ruta de
   cada archivo y mostrando solo lo que cambia con suficiente contexto para ubicarlo.
2. Una explicación breve y estructurada de los cambios clave, agrupada por dispositivo:
   - Móvil (< 768px)
   - Tableta (768px–1024px)
   - Escritorio (qué se mantiene igual o qué mejoró de rebote)
3. Una lista de pendientes o riesgos (pantallas que heredan el cambio y conviene revisar).

## Sección a ajustar

[Escriba aquí el módulo o la pantalla. Ejemplos:
 - "El layout general: barra superior, menú lateral y migas de pan."
 - "El listado de Beneficiarios (Components/Pages/Beneficiarios/ListadoBeneficiarios.razor)."
 - "Todos los listados: quiero que en móvil las filas se vean como tarjetas."
 - "Los reportes Panorama*: tarjetas y gráficas."
 Objetivo general: que se ajuste a móviles y tabletas y que la información se lea bien
 y ordenada en cada formato.]
```

---

## Orden sugerido de trabajo

1. **Layout general** (barra superior, menú lateral, migas, márgenes): afecta a todas las pantallas.
2. **Componentes compartidos**: `EncabezadoPagina`, `TarjetaResumen` / `.ac-kpis`, filtros.
3. **Listados** (MudDataGrid → tarjetas en móvil): empezar por uno (Beneficiarios) y replicar el patrón.
4. **Formularios y diálogos.**
5. **Reportes y calendario de alquileres.**
6. **Login y Bienvenida.**
