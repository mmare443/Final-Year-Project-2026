/**
 * PUBLIC_SITE_URL — where the static public website (index.html,
 * html/login.html, etc.) actually lives.
 *
 * This app (the React SPA) and the public site are two separate projects
 * per Blueprint Rev 5 §2 — the public site stays static HTML, this SPA is
 * what its login.html button links into. That means "back to the LCC
 * website" links in this app have to point to a real external URL, not an
 * internal route.
 *
 * TODO: update this to the real deployed public site URL once it's hosted
 * (e.g. https://lccb.ac.pg). For now it points at a local static server —
 * run one from your public site folder with, e.g.:
 *   python -m http.server 8899
 * and adjust the port below if you used a different one.
 */
export const PUBLIC_SITE_URL = "http://localhost:8899";
