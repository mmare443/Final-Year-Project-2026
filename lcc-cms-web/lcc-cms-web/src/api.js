const TOKEN_KEY = "lcc_cms_jwt";

const localPage = typeof window !== "undefined"
  && (window.location.hostname === "localhost" || window.location.hostname === "127.0.0.1");

export const API_ORIGIN = localPage
  ? "http://localhost:5080"
  : "https://api.lccbportal.org";

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
  "Couldn't reach the College API at https://api.lccbportal.org.";

export async function readApiError(res) {
  const text = (await res.text().catch(() => "")).trim();
  const fromJson = parseErrorMessage(text);
  if (res.status === 401) {
    return "Sign in required (401). Sign in again and retry.";
  }
  if (res.status === 403) {
    return fromJson && fromJson !== "An error occurred."
      ? fromJson
      : "You do not have permission for this request (403).";
  }
  if (res.status === 404) {
    return "The requested API endpoint or record was not found (404).";
  }
  if (res.status >= 500) {
    return fromJson && fromJson !== "An error occurred."
      ? fromJson
      : "Couldn't save. Please try again.";
  }
  return fromJson || (text.startsWith("{") ? "Couldn't complete that request." : text) || `Request failed (${res.status}).`;
}

function parseErrorMessage(text) {
  if (!text) return "";
  try {
    const obj = JSON.parse(text);
    if (obj && typeof obj === "object") {
      if (typeof obj.error === "string" && obj.error.trim()) return obj.error.trim();
      if (typeof obj.message === "string" && obj.message.trim()) return obj.message.trim();
      if (typeof obj.title === "string" && obj.title.trim()) return obj.title.trim();
      return "";
    }
  } catch {
    /* not JSON */
  }
  return text.startsWith("{") ? "" : text;
}

export async function throwIfNotOk(res, label) {
  if (res.ok) return res;
  const detail = await readApiError(res);
  throw new Error(label ? `${label}: ${detail}` : detail);
}
