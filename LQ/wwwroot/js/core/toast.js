// @ts-check
/**
 * @file Single global success/error toast that fades in at top-center.
 *
 * Originally lived in workspace.js. Moved here so it's reachable from any module
 * without dragging workspace.js's other state along.
 */

const SUCCESS_CLASSES = 'bg-green-500/10 border-green-500/20 text-green-400';
const ERROR_CLASSES = 'bg-red-500/10 border-red-500/20 text-red-400';

const ICON_SUCCESS =
  '<svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">' +
  '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"></path>' +
  '</svg>';

const ICON_ERROR =
  '<svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">' +
  '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" ' +
  'd="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z">' +
  '</path></svg>';

/**
 * Show a toast at the top-center of the viewport. Auto-dismisses after 3s.
 * @param {string} message
 * @param {boolean} [isError]
 */
export function showToast(message, isError = false) {
  const toast = document.createElement('div');
  toast.className =
    'fixed top-6 left-1/2 -translate-x-1/2 z-[100] px-6 py-3 rounded-full shadow-2xl ' +
    'backdrop-blur-xl border text-sm font-medium transition-all duration-300 transform ' +
    'scale-95 opacity-0 flex items-center gap-2 ' +
    (isError ? ERROR_CLASSES : SUCCESS_CLASSES);

  const icon = isError ? ICON_ERROR : ICON_SUCCESS;
  // message is plain text, so use textContent on a span we build alongside the icon
  const span = document.createElement('span');
  span.textContent = message;
  toast.innerHTML = icon;
  toast.appendChild(span);

  document.body.appendChild(toast);

  requestAnimationFrame(() => {
    toast.classList.remove('scale-95', 'opacity-0');
    toast.classList.add('scale-100', 'opacity-100');
  });

  setTimeout(() => {
    toast.classList.remove('scale-100', 'opacity-100');
    toast.classList.add('scale-95', 'opacity-0');
    setTimeout(() => toast.remove(), 300);
  }, 3000);
}
