const TOKEN_KEY = "lcc_cms_jwt";

export const API_ORIGIN = "http://localhost:5000";

export function getAccessToken() {
  return sessionStorage.getItem(TOKEN_KEY);
}

export function setAccessToken(token) {
  if (!token) {
    sessionStorage.removeItem(TOKEN_KEY);
    return;
  }
  sessionStorage.setItem(TOKEN_KEY, token);
}

export function clearAccessToken() {
  sessionStorage.removeItem(TOKEN_KEY);
}

export function apiFetch(url, options = {}) {
  const headers = new Headers(options.headers || {});
  const token = getAccessToken();
  if (token && !headers.has("Authorization")) {
    headers.set("Authorization", `Bearer ${token}`);
  }
  return fetch(url, { ...options, headers });
}

export function isNetworkFailure(err) {
  return (
    err instanceof TypeError
    || err?.name === "TypeError"
    || /failed to fetch|networkerror|load failed/i.test(String(err?.message || ""))
  );
}

export const API_UNREACHABLE =
  "Couldn't reach the backend API. Make sure it is running on http://localhost:5000.";

export async function readApiError(res) {
  const text = (await res.text().catch(() => "")).trim();
  if (res.status === 401) {
    return "Sign in required (401). Sign in again and retry.";
  }
  if (res.status === 403) {
    return "You do not have permission for this request (403).";
  }
  if (res.status === 404) {
    return "The requested API endpoint or record was not found (404).";
  }
  if (res.status >= 500) {
    return text || `The server failed this request (${res.status}).`;
  }
  return text || `Request failed (${res.status}).`;
}

export async function throwIfNotOk(res, label) {
  if (res.ok) return res;
  const detail = await readApiError(res);
  throw new Error(label ? `${label}: ${detail}` : detail);
}
