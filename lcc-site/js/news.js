const DEFAULT_NEWS_IMAGE = "images/college/news/default-news.jpg";
const SOURCE_LOGO_ROOT = "images/college/news/logos/";

const SOURCE_CATALOG = [
    { name: "Facebook", logo: "facebook.svg", hosts: ["facebook.com", "fb.com", "fb.watch"] },
    { name: "YouTube", logo: "youtube.svg", hosts: ["youtube.com", "youtu.be"] },
    { name: "Post Courier", logo: "postcourier.svg", hosts: ["postcourier.com.pg"] },
    { name: "The National", logo: "thenational.svg", hosts: ["thenational.com.pg"] },
    { name: "EMTV", logo: "emtv.svg", hosts: ["emtv.com.pg"] },
    { name: "NBC", logo: "nbc.svg", hosts: ["nbc.com.pg", "nbcpng"] },
];

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

function pick(article, ...keys) {
    for (const key of keys) {
        const value = article?.[key];
        if (value !== undefined && value !== null && value !== "") {
            return value;
        }
    }
    return undefined;
}

function publishedLabel(publishedAt) {
    if (!publishedAt) return "";
    const date = new Date(publishedAt);
    if (Number.isNaN(date.getTime())) return "";
    return date.toLocaleDateString("en-PG", {
        day: "numeric",
        month: "short",
        year: "numeric",
    });
}

function inHtmlFolder() {
    const path = String(window.location.pathname || "").replace(/\\/g, "/");
    return /\/html\//i.test(path) || /\/html$/i.test(path);
}

function siteImage(path) {
    const clean = String(path || "").replace(/^\//, "");
    return inHtmlFolder() ? `../${clean}` : clean;
}

function newsPageHref(id) {
    const file = inHtmlFolder() ? "news.html" : "html/news.html";
    return id ? `${file}?id=${encodeURIComponent(id)}` : file;
}

function resolveImageUrl(url) {
    const value = String(url || "").trim();
    if (!value) return siteImage(DEFAULT_NEWS_IMAGE);
    if (/^https?:\/\//i.test(value)) return value;
    return siteImage(value.replace(/^\//, ""));
}

function detectSource(url) {
    try {
        const host = new URL(url).hostname.toLowerCase();
        return SOURCE_CATALOG.find((item) => item.hosts.some((part) => host.includes(part))) || null;
    } catch {
        return null;
    }
}

function photoWithFallback(primary, secondary) {
    const first = resolveImageUrl(primary || secondary || "");
    const second = primary && secondary ? resolveImageUrl(secondary) : siteImage(DEFAULT_NEWS_IMAGE);
    const last = siteImage(DEFAULT_NEWS_IMAGE);
    const contain = !/^https?:\/\//i.test(String(primary || "").trim());
    const cls = contain ? "news-thumb news-thumb-logo" : "news-thumb";
    return `<img class="${cls}" src="${escapeHtml(first)}" alt="" data-fallback="${escapeHtml(second)}" data-default="${escapeHtml(last)}" onerror="window.__lccNewsImgFallback(this)">`;
}

window.__lccNewsImgFallback = function (img) {
    if (!img || img.dataset.failed === "last") return;
    const fallback = img.getAttribute("data-fallback") || "";
    const last = img.getAttribute("data-default") || "";
    if (img.dataset.failed !== "fallback" && fallback && !img.src.includes(fallback) && fallback !== last) {
        img.dataset.failed = "fallback";
        img.classList.add("news-thumb-logo");
        img.src = fallback;
        return;
    }
    if (last && !img.src.includes(last)) {
        img.dataset.failed = "last";
        img.classList.remove("news-thumb-logo");
        img.src = last;
    }
};

function isPublishedArticle(article) {
    const flag = pick(article, "isPublished", "IsPublished");
    return flag === undefined || flag === true || flag === "true" || flag === 1;
}

function normalizeArticle(raw) {
    const article = raw && typeof raw === "object" ? raw : {};
    const id = pick(article, "newsId", "id", "NewsId", "Id");
    const externalUrl = pick(article, "externalUrl", "ExternalUrl");
    const isExternal = Boolean(
        pick(article, "isExternal", "IsExternal") && externalUrl
    );
    const detected = isExternal ? detectSource(externalUrl) : null;
    return {
        id,
        title: pick(article, "title", "Title") || "Untitled",
        summary: pick(article, "summary", "Summary") || "",
        content: pick(article, "content", "Content") || "",
        imageUrl: pick(article, "imageUrl", "ImageUrl"),
        externalUrl,
        sourceName: pick(article, "sourceName", "SourceName") || detected?.name || "",
        sourceSubtitle: pick(article, "sourceSubtitle", "SourceSubtitle") || "",
        sourceLogoUrl: pick(article, "sourceLogoUrl", "SourceLogoUrl")
            || (detected ? SOURCE_LOGO_ROOT + detected.logo : undefined),
        sourceTitle: pick(article, "sourceTitle", "SourceTitle") || "",
        thumbnailUrl: pick(article, "thumbnailUrl", "ThumbnailUrl"),
        isExternal,
        publishedAt: pick(article, "publishedAt", "PublishedAt"),
        isPublished: isPublishedArticle(article),
    };
}

function renderCard(article, compact) {
    const when = escapeHtml(publishedLabel(article.publishedAt));
    if (article.isExternal) {
        const displayTitle = article.sourceTitle || article.title;
        const subtitle = escapeHtml(article.sourceSubtitle || article.summary || "");
        const sourceLabel = escapeHtml(article.sourceName || "External");
        const logo = article.sourceLogoUrl
            ? `<img class="news-source-logo" src="${escapeHtml(resolveImageUrl(article.sourceLogoUrl))}" alt="">`
            : "";
        return [
            '<article class="info-card news-card news-card-external">',
            '<div class="news-media">',
            photoWithFallback(article.thumbnailUrl, article.sourceLogoUrl),
            `<div class="news-source-brand">${logo}<span>${sourceLabel}</span></div>`,
            "</div>",
            `<time class="card-meta" datetime="${escapeHtml(article.publishedAt || "")}">${when || "News"}</time>`,
            `<h3>${escapeHtml(displayTitle)}</h3>`,
            subtitle ? `<p class="news-source-subtitle">${subtitle}</p>` : "",
            `<a class="news-visit" href="${escapeHtml(article.externalUrl)}" target="_blank" rel="noopener noreferrer">Visit Source</a>`,
            "</article>",
        ].join("");
    }

    const image = resolveImageUrl(article.imageUrl);
    const href = newsPageHref(article.id);
    const linkText = compact ? "Read news" : "Read More";
    return [
        '<article class="info-card news-card">',
        `<img class="info-card-photo" src="${escapeHtml(image)}" alt="">`,
        `<span class="card-meta">${when || "News"}</span>`,
        `<h3>${escapeHtml(article.title)}</h3>`,
        `<p>${escapeHtml(article.summary)}</p>`,
        `<a href="${escapeHtml(href)}">${linkText}</a>`,
        "</article>",
    ].join("");
}

function renderArticle(article) {
    const image = resolveImageUrl(article.imageUrl);
    const when = escapeHtml(publishedLabel(article.publishedAt));
    const content = escapeHtml(article.content || article.summary || "").replace(/\n/g, "<br>");
    return [
        '<article class="news-article">',
        `<p><a href="${escapeHtml(newsPageHref())}">← All news</a></p>`,
        `<img class="news-article-photo" src="${escapeHtml(image)}" alt="">`,
        `<span class="card-meta">${when}</span>`,
        `<h2>${escapeHtml(article.title)}</h2>`,
        `<div class="news-article-body">${content}</div>`,
        "</article>",
    ].join("");
}

function parseNewsPayload(data) {
    if (Array.isArray(data)) return data;
    if (Array.isArray(data?.value)) return data.value;
    if (Array.isArray(data?.items)) return data.items;
    return [];
}

async function fetchNewsList() {
    const res = await fetch(`${API_ORIGIN}/api/news`);
    if (!res.ok) throw new Error(`API returned ${res.status}`);
    return parseNewsPayload(await res.json())
        .map(normalizeArticle)
        .filter((article) => article.isPublished);
}

async function fetchNewsArticle(id) {
    const res = await fetch(`${API_ORIGIN}/api/news/${encodeURIComponent(id)}`);
    if (!res.ok) throw new Error(`API returned ${res.status}`);
    return normalizeArticle(await res.json());
}

async function bootNews() {
    const status = byId("news-status");
    const root = byId("news-root");
    const homeRoot = byId("home-news-root");
    const params = new URLSearchParams(window.location.search);
    const articleId = params.get("id");

    try {
        if (root && articleId) {
            const article = await fetchNewsArticle(articleId);
            if (article.isExternal && article.externalUrl) {
                window.location.replace(article.externalUrl);
                return;
            }
            root.className = "";
            root.innerHTML = renderArticle(article);
            if (status) status.hidden = true;
            return;
        }

        const list = await fetchNewsList();

        if (root) {
            if (list.length === 0) {
                if (status) {
                    status.hidden = false;
                    status.textContent = "No news has been published yet.";
                }
                root.innerHTML = "";
            } else {
                root.innerHTML = list.map((item) => renderCard(item, false)).join("");
                if (status) status.hidden = true;
            }
        }

        if (homeRoot) {
            const preview = list.slice(0, 3);
            if (preview.length === 0) {
                homeRoot.innerHTML = [
                    '<article class="info-card">',
                    '<span class="card-meta">News</span>',
                    "<h3>College News and Updates</h3>",
                    "<p>Published news will appear here.</p>",
                    '<a href="html/news.html">Read news</a>',
                    "</article>",
                ].join("");
            } else {
                homeRoot.innerHTML = preview.map((item) => renderCard(item, true)).join("");
            }
        }
    } catch (err) {
        const message =
            "Couldn't load news. Make sure the College API is running on http://localhost:5000.";
        if (status) {
            status.hidden = false;
            status.textContent = message;
        }
        if (homeRoot) {
            homeRoot.innerHTML = `<p class="programmes-status">${message}</p>`;
        }
        console.error("News render failed", err);
    }
}

if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", bootNews);
} else {
    bootNews();
}
