import { API_ORIGIN, apiFetch } from "./api";

/**
 * users.profile_photo_url is a storage key such as
 * uploads/profiles/user-5.jpg. The file is kept under App_Data, not the
 * public site, so it must be read with the signed-in token from
 * GET /api/profile/photo. Do not prefix it with the portal origin.
 */
export function readProfilePhotoUrl(record) {
  if (!record || typeof record !== "object") return null;
  const value = record.profilePhotoUrl
    ?? record.ProfilePhotoUrl
    ?? record.profile_photo_url
    ?? null;
  const text = value == null ? "" : String(value).trim();
  return text || null;
}

export function ownProfilePhotoRequestUrl() {
  return `${API_ORIGIN}/api/profile/photo`;
}

export function userProfilePhotoRequestUrl(userId) {
  return `${API_ORIGIN}/api/profile/photo/${encodeURIComponent(userId)}`;
}

export async function blobToDataUrl(blob) {
  if (!blob || blob.size === 0) return null;
  return await new Promise((resolve) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result || "") || null);
    reader.onerror = () => resolve(null);
    reader.readAsDataURL(blob);
  });
}

export async function fetchAuthorizedPhotoBlob(url) {
  const res = await apiFetch(url);
  if (!res.ok) return null;
  const blob = await res.blob();
  if (!blob || blob.size === 0) return null;
  const type = (blob.type || "").toLowerCase();
  if (type && !type.startsWith("image/") && type !== "application/octet-stream") {
    return null;
  }
  return blob;
}

export async function fetchAuthorizedPhotoDataUrl(url) {
  try {
    const blob = await fetchAuthorizedPhotoBlob(url);
    return blob ? await blobToDataUrl(blob) : null;
  } catch {
    return null;
  }
}

export async function displayUrlToDataUrl(url) {
  if (!url) return null;
  if (String(url).startsWith("data:")) return url;
  try {
    const res = String(url).startsWith("blob:") ? await fetch(url) : await apiFetch(url);
    if (!res.ok) return null;
    return await blobToDataUrl(await res.blob());
  } catch {
    return null;
  }
}
