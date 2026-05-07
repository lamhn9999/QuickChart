// @ts-check
/**
 * @file Placeholder card rendered when a foreign-file chart's source CSVFile is no longer
 * accessible (deleted, or access revoked).
 *
 * The block stays in the canvas JSON so the user can decide whether to delete it explicitly
 * — we don't auto-prune, so a transient 403 (e.g. session expiry) doesn't lose the chart.
 *
 * Registered in the group registry like a normal card (with empty `instances` array) so
 * removeChartGroup still works on it.
 */

import { getNextCardPosition } from '../canvas-engine/canvas.js';
import { nextZOrder } from '../canvas-engine/draggable.js';
import { setGroup } from './state.js';

/**
 * @param {number} groupId
 * @param {number[] | null} fieldIndexes
 * @param {string | null} csvFileToken
 * @param {{ x?: number, y?: number, width?: number, height?: number } | null} dropPos
 */
export function buildUnavailableCard(groupId, fieldIndexes, csvFileToken, dropPos) {
  const card = document.createElement('div');
  card.classList.add('canvas-card');
  card.dataset.groupId = String(groupId);

  const pos = dropPos ?? getNextCardPosition();
  const initW = dropPos?.width ?? 480;
  const initH = dropPos?.height;
  card.style.cssText =
    `position:absolute;left:${Math.max(0, pos.x ?? 0)}px;top:${Math.max(0, pos.y ?? 0)}px;` +
    `width:${initW}px;${initH ? `height:${initH}px;` : ''}z-index:${nextZOrder()}`;

  const panelsRow = document.createElement('div');
  panelsRow.classList.add('canvas-card-panels', 'group');
  card.appendChild(panelsRow);

  const panel = document.createElement('div');
  panel.className =
    'flex flex-col items-center justify-center p-8 text-center text-zinc-500 bg-zinc-50 rounded-2xl';
  panel.style.minHeight = '180px';
  panel.dataset.groupId = String(groupId);
  panel.dataset.fieldIndexes = JSON.stringify(fieldIndexes ? [...fieldIndexes] : []);
  if (csvFileToken) panel.dataset.csvFileToken = csvFileToken;

  const icon = document.createElement('div');
  icon.className = 'text-3xl mb-2 opacity-60';
  icon.textContent = '⚠️';
  const title = document.createElement('div');
  title.className = 'font-medium text-sm text-zinc-700';
  title.textContent = 'Source file unavailable';
  const sub = document.createElement('div');
  sub.className = 'text-xs mt-1 text-zinc-500';
  sub.textContent =
    'The CSVFile this chart was sourced from has been removed or is no longer accessible.';

  panel.appendChild(icon);
  panel.appendChild(title);
  panel.appendChild(sub);
  panelsRow.appendChild(panel);

  setGroup(groupId, { card, instances: [], ro: null });
  return card;
}
