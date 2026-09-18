/** Strip trailing slashes so `/registrar` and `/registrar/` are the same item. */
export function normalizeNavPath(path) {
  if (!path) return "/";
  const trimmed = String(path).split("?")[0].split("#")[0].replace(/\/+$/, "");
  return trimmed === "" ? "/" : trimmed;
}

/**
 * Exactly one sidebar item is active: the longest nav path that matches
 * the current location. `/registrar` therefore does not stay active on
 * `/registrar/admissions` or `/registrar/students`.
 */
export function resolveActiveNavPath(navItems, pathname) {
  const current = normalizeNavPath(pathname);
  let best = null;

  for (const item of navItems) {
    const path = typeof item === "string" ? null : item?.path;
    if (!path) continue;
    const target = normalizeNavPath(path);
    const matches = current === target || current.startsWith(`${target}/`);
    if (!matches) continue;
    if (!best || target.length > best.length) {
      best = target;
    }
  }

  return best;
}

export function isNavItemActive(itemPath, activePath) {
  if (!itemPath || !activePath) return false;
  return normalizeNavPath(itemPath) === activePath;
}
