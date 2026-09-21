const API_ORIGIN = "http://localhost:5000";

function byId(id) {
    return document.getElementById(id);
}

function escapeHtml(value) {
    return String(value ?? "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;");
}

function dateLabel(value) {
    if (!value) return "";
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return "";
    return date.toLocaleDateString("en-PG", {
        day: "numeric",
        month: "short",
        year: "numeric",
    });
}

function excerpt(content, max = 220) {
    const text = String(content || "").replace(/\s+/g, " ").trim();
    if (text.length <= max) return text;
    return `${text.slice(0, max).trim()}…`;
}

function cardHtml(item) {
    const when = item.startDate ? dateLabel(item.startDate) : dateLabel(item.createdAt || item.postedAt);
    const until = item.endDate ? ` · Until ${dateLabel(item.endDate)}` : "";
    const priority = item.priority && item.priority !== "Normal"
        ? `<span class="announce-badge announce-badge-${escapeHtml(item.priority)}">${escapeHtml(item.priority)}</span>`
        : "";
    return [
        '<article class="info-card announce-card">',
        '<div class="announce-card-head">',
        `<h3>${escapeHtml(item.title)}</h3>`,
        priority,
        "</div>",
        when ? `<p class="announce-date">${escapeHtml(when)}${escapeHtml(until)}</p>` : "",
        `<p>${escapeHtml(excerpt(item.content))}</p>`,
        "</article>",
    ].join("");
}

async function loadPublicAnnouncements() {
    const root = byId("home-announce-root");
    const status = byId("home-announce-status");
    if (!root) return;

    try {
        const res = await fetch(`${API_ORIGIN}/api/announcements/public`);
        if (!res.ok) throw new Error(`API ${res.status}`);
        const data = await res.json();
        const items = Array.isArray(data) ? data.slice(0, 6) : [];
        if (status) {
            status.hidden = items.length > 0;
            status.textContent = items.length ? "" : "No current announcements.";
        }
        root.innerHTML = items.length
            ? items.map(cardHtml).join("")
            : "";
    } catch (err) {
        console.error("Announcements render failed", err);
        if (status) {
            status.hidden = false;
            status.textContent = "Couldn't load announcements.";
        }
    }
}

loadPublicAnnouncements();
