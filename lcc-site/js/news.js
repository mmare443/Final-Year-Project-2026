const API_ORIGIN = "http://localhost:5000";

function byId(id) {
    return document.getElementById(id);
}

function escapeHtml(value) {
    return String(value ?? "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;");
}

function audienceLabel(targetRole) {
    const role = String(targetRole ?? "").trim();
    return role ? role : "All roles";
}

function postedLabel(postedAt) {
    if (!postedAt) return "";
    const date = new Date(postedAt);
    if (Number.isNaN(date.getTime())) return "";
    return date.toLocaleDateString("en-PG", {
        day: "numeric",
        month: "short",
        year: "numeric",
    });
}

function renderNotice(notice) {
    const title = escapeHtml(notice.title || "Untitled notice");
    const audience = escapeHtml(audienceLabel(notice.targetRole));
    const when = escapeHtml(postedLabel(notice.postedAt));
    const content = escapeHtml(notice.content || "");
    return `
        <article class="info-card news-card">
            <span class="card-meta">${audience}${when ? ` · ${when}` : ""}</span>
            <h3>${title}</h3>
            <p>${content}</p>
        </article>
    `;
}

document.addEventListener("DOMContentLoaded", async () => {
    const status = byId("news-status");
    const root = byId("news-root");
    if (!status || !root) return;

    try {
        const res = await fetch(`${API_ORIGIN}/api/notices`);
        if (!res.ok) throw new Error(`API returned ${res.status}`);
        const notices = await res.json();
        const list = Array.isArray(notices) ? notices : [];

        if (list.length === 0) {
            status.textContent = "No announcements have been published yet.";
            root.innerHTML = "";
            return;
        }

        root.innerHTML = list.map(renderNotice).join("");
        status.hidden = true;
    } catch (err) {
        status.textContent =
            "Couldn't load news. Make sure the College API is running on http://localhost:5000.";
        console.error(err);
    }
});
