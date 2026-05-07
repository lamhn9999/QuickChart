// @ts-check
/**
 * @file Thin fetch wrappers that throw on non-2xx and parse JSON.
 *
 * All helpers accept `{ signal }` so callers can cancel via AbortController
 * (workspace uses one shared controller stored in core/state.js — see Phase 3).
 */

export class HttpError extends Error {
  /**
   * @param {string} message
   * @param {number} status
   * @param {string} [body]
   */
  constructor(message, status, body) {
    super(message);
    this.name = 'HttpError';
    this.status = status;
    this.body = body;
  }
}

/**
 * @param {Response} res
 * @returns {Promise<any>}
 */
async function _parseOrThrow(res) {
  if (!res.ok) {
    let body;
    try {
      body = await res.text();
    } catch {
      /* swallow */
    }
    throw new HttpError(`${res.status} ${res.statusText}`, res.status, body);
  }
  return res.json();
}

/**
 * GET `url`, return parsed JSON.
 * @param {string} url
 * @param {{ signal?: AbortSignal }} [opts]
 */
export async function fetchJson(url, { signal } = {}) {
  const res = await fetch(url, { signal });
  return _parseOrThrow(res);
}

/**
 * POST `body` JSON-serialised to `url`, return parsed JSON.
 * @param {string} url
 * @param {unknown} body
 * @param {{ signal?: AbortSignal }} [opts]
 */
export async function postJson(url, body, { signal } = {}) {
  const res = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
    signal,
  });
  return _parseOrThrow(res);
}

/**
 * POST a FormData payload to `url`, return parsed JSON.
 * @param {string} url
 * @param {FormData} form
 * @param {{ signal?: AbortSignal }} [opts]
 */
export async function postForm(url, form, { signal } = {}) {
  const res = await fetch(url, { method: 'POST', body: form, signal });
  return _parseOrThrow(res);
}
