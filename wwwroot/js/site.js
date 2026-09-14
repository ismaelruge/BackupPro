// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Protección anti-doble-clic global: al enviar cualquier formulario, deshabilita sus botones de
// envío para que un segundo clic (impaciencia, doble clic, tecla repetida) no dispare una segunda
// petición mientras la primera sigue en curso. Aplica a toda la app sin tocar cada vista porque
// site.js se carga una sola vez desde el layout compartido.
//
// Se respeta event.defaultPrevented: si la validación de jQuery Unobtrusive (u otro script) ya
// bloqueó el envío por ser inválido, el evento "submit" del navegador igual llega acá al burbujear,
// pero no se debe deshabilitar el botón de un formulario que en realidad no se envió (el usuario
// necesita poder corregir el campo y reintentar sin que el botón quede pegado). Los formularios que
// manejan su propio envío por fetch() y ya controlan el estado de su botón (como el login) no se
// ven afectados: si el botón ya está deshabilitado, no se vuelve a tocar.
document.addEventListener('submit', function (event) {
    var form = event.target;
    if (!(form instanceof HTMLFormElement) || event.defaultPrevented) {
        return;
    }

    var submitControls = form.querySelectorAll('button[type="submit"], input[type="submit"]');
    submitControls.forEach(function (control) {
        if (control.disabled) {
            return;
        }

        control.disabled = true;

        if (control.tagName === 'BUTTON') {
            control.dataset.originalHtml = control.innerHTML;
            control.innerHTML = '<span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>' + control.innerHTML;
        }
    });
});
