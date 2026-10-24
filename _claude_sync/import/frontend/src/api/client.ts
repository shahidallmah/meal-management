const BASE = (import.meta.env.VITE_API_URL as string | undefined)?.replace(/\/$/, '') ?? '';
/** Absolute API address (empty when the web app is served by the API itself). */
export const API_BASE = BASE;
const TOKEN_KEY = 'mm.token';

export class ApiError extends Error {
  constructor(message: string, public status: number) {
    super(message);
  }
}

function safeGet(key: string): string | null {
  try { return localStorage.getItem(key); } catch { return null; }
}
function safeSet(key: string, value: string | null) {
  try {
    if (value === null) localStorage.removeItem(key);
    else localStorage.setItem(key, value);
  } catch { /* storage unavailable */ }
}

let token: string | null = safeGet(TOKEN_KEY);
let onUnauthorized: (() => void) | null = null;

export const auth = {
  get token() { return token; },
  set(t: string | null) { token = t; safeSet(TOKEN_KEY, t); },
  onUnauthorized(cb: () => void) { onUnauthorized = cb; },
};

type Query = Record<string, string | number | boolean | null | undefined>;

export function qs(params?: Query): string {
  if (!params) return '';
  const p = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) if (v !== undefined && v !== null && v !== '') p.set(k, String(v));
  const s = p.toString();
  return s ? `?${s}` : '';
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' };
  const isForm = body instanceof FormData;
  if (body !== undefined && !isForm) headers['Content-Type'] = 'application/json';
  if (token) headers.Authorization = `Bearer ${token}`;

  let res: Response;
  try {
    res = await fetch(BASE + path, { method, headers, body: body === undefined ? undefined : isForm ? body : JSON.stringify(body) });
  } catch {
    throw new ApiError('Cannot reach the server. Check your connection and try again.', 0);
  }

  if (res.status === 401 && token && !path.startsWith('/api/auth/login')) {
    onUnauthorized?.();
  }
  if (!res.ok) {
    let message = `Request failed (${res.status})`;
    try {
      const data = await res.json();
      if (data?.message) message = data.message;
      else if (data?.title) message = data.title;
    } catch { /* not json */ }
    throw new ApiError(message, res.status);
  }
  if (res.status === 204) return undefined as T;
  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

export const api = {
  get: <T>(path: string, params?: Query) => request<T>('GET', path + qs(params)),
  post: <T>(path: string, body?: unknown) => request<T>('POST', path, body ?? {}),
  put: <T>(path: string, body?: unknown) => request<T>('PUT', path, body ?? {}),
  del: <T>(path: string) => request<T>('DELETE', path),
  /** Uploads a file as multipart/form-data (field name "file"). */
  upload: <T>(path: string, file: File, method: 'POST' | 'PUT' = 'PUT') => {
    const fd = new FormData();
    fd.append('file', file);
    return request<T>(method, path, fd);
  },

  /** Downloads a file (report export) using the bearer token. */
  download(path: string, params: Query, fallbackName: string) {
    return saveResponse(fetch(BASE + path + qs(params), { headers: token ? { Authorization: `Bearer ${token}` } : {} }), fallbackName);
  },

  /** POSTs JSON and saves the file the server returns (e.g. activation codes as Excel). */
  downloadPost(path: string, body: unknown, fallbackName: string) {
    const headers: Record<string, string> = { 'Content-Type': 'application/json' };
    if (token) headers.Authorization = `Bearer ${token}`;
    return saveResponse(fetch(BASE + path, { method: 'POST', headers, body: JSON.stringify(body) }), fallbackName);
  },
};

async function saveResponse(pending: Promise<Response>, fallbackName: string): Promise<Response> {
  let res: Response;
  try { res = await pending; } catch { throw new ApiError('Cannot reach the server. Check your connection and try again.', 0); }
  if (!res.ok) {
    let message = `Download failed (${res.status})`;
    try { const d = await res.json(); if (d?.message) message = d.message; } catch { /* ignore */ }
    throw new ApiError(message, res.status);
  }
  const blob = await res.blob();
  const cd = res.headers.get('Content-Disposition') ?? '';
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(cd);
  const name = match ? decodeURIComponent(match[1]) : fallbackName;
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = name;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 2000);
  return res;
}
