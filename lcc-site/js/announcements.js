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

function itemId(item) {
    return item.announcementId ?? item.id ?? "";
}

function cardHtml(item) {
    const id = itemId(item);
    const when = item.startDate
        ? dateLabel(item.startDate)
        : dateLabel(item.createdAt || item.postedAt);
    const audience = String(item.audience || "").trim();
    const priority = item.priority && item.priority !== "Normal"
        ? `<span class="announce-badge announce-badge-${escapeHtml(item.priority)}">${escapeHtml(item.priority)}</span>`
        : "";
    const audienceBadge = audience
        ? `<span class="announce-badge announce-badge-${escapeHtml(audience)}">${escapeHtml(audience)}</span>`
        : "";
    return [
        `<article class="announce-item" id="announce-${escapeHtml(id)}">`,
        `<h3>${escapeHtml(item.title)}</h3>`,
        when ? `<p class="announce-date">${escapeHtml(when)}</p>` : "",
        `<div class="announce-meta">${audienceBadge}${priority}</div>`,
        `<button type="button" class="announce-more" aria-expanded="false" data-announce-toggle="${escapeHtml(id)}">Read More</button>`,
        `<div class="announce-body" id="announce-body-${escapeHtml(id)}" hidden>${escapeHtml(item.content)}</div>`,
        "</article>",
    ].join("");
}

function bindToggles(root) {
    root.addEventListener("click", (event) => {
        const button = event.target.closest("[data-announce-toggle]");
        if (!button || !root.contains(button)) return;
        const id = button.getAttribute("data-announce-toggle");
        const body = byId(`announce-body-${id}`);
        if (!body) return;
        const open = body.hasAttribute("hidden");
        if (open) body.removeAttribute("hidden");
        else body.setAttribute("hidden", "");
        button.setAttribute("aria-expanded", open ? "true" : "false");
        button.textContent = open ? "Show less" : "Read More";
    });
}

function openFromHash() {
    const hash = String(window.location.hash || "");
    const match = hash.match(/^#announce-(\d+)$/);
    if (!match) return;
    const button = document.querySelector(`[data-announce-toggle="${match[1]}"]`);
    if (button && button.getAttribute("aria-expanded") !== "true") {
        button.click();
    }
    const item = byId(`announce-${match[1]}`);
    if (item) item.scrollIntoView({ block: "nearest" });
}

async function loadPublicAnnouncements() {
    const root = byId("home-announce-root");
    const status = byId("home-announce-status");
    if (!root) return;
    bindToggles(root);

    try {
        const res = await fetch(window.collegeApiUrl("/api/announcements/public"));
        if (!res.ok) throw new Error(`API ${res.status}`);
        const data = await res.json();
        const items = Array.isArray(data) ? data.slice(0, 8) : [];
        if (status) {
            status.hidden = items.length > 0;
            status.textContent = items.length ? "" : "No current announcements.";
        }
        root.innerHTML = items.length ? items.map(cardHtml).join("") : "";
        openFromHash();
    } catch (err) {
        console.error("Announcements render failed", err);
        if (status) {
            status.hidden = false;
            status.textContent = "Unable to connect to the College API.";
        }
    }
}

loadPublicAnnouncements();
