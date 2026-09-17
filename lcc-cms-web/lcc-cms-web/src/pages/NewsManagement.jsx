import { useCallback, useState, useEffect } from "react";
import DashboardLayout from "../components/DashboardLayout";
import { API_ORIGIN, API_UNREACHABLE, apiFetch, isNetworkFailure, readApiError } from "../api";
import { MANAGEMENT_NAV } from "./managementNav";
import "./AcademicStructure.css";
import "./StudentRecords.css";
import "./NewsManagement.css";

const emptyForm = {
  isExternal: false,
  title: "",
  summary: "",
  content: "",
  sourceName: "",
  sourceSubtitle: "",
  sourceLogoUrl: "",
  sourceTitle: "",
  thumbnailUrl: "",
  externalUrl: "",
  imageUrl: "",
  isPublished: false,
};

const SOURCE_RULES = [
  { test: /facebook\.com|fb\.com|fb\.watch/i, name: "Facebook", logo: "images/college/news/logos/facebook.svg" },
  { test: /youtube\.com|youtu\.be/i, name: "YouTube", logo: "images/college/news/logos/youtube.svg" },
  { test: /postcourier\.com\.pg/i, name: "Post Courier", logo: "images/college/news/logos/postcourier.svg" },
  { test: /thenational\.com\.pg/i, name: "The National", logo: "images/college/news/logos/thenational.svg" },
  { test: /emtv\.com\.pg/i, name: "EMTV", logo: "images/college/news/logos/emtv.svg" },
  { test: /nbc\.com\.pg|nbcpng/i, name: "NBC", logo: "images/college/news/logos/nbc.svg" },
];

function detectSource(url) {
  const value = String(url || "");
  return SOURCE_RULES.find((rule) => rule.test.test(value)) || null;
}

function toForm(row) {
  return {
    isExternal: Boolean(row.isExternal),
    title: row.title || "",
    summary: row.summary || "",
    content: row.content || "",
    sourceName: row.sourceName || "",
    sourceSubtitle: row.sourceSubtitle || "",
    sourceLogoUrl: row.sourceLogoUrl || "",
    sourceTitle: row.sourceTitle || "",
    thumbnailUrl: row.thumbnailUrl || "",
    externalUrl: row.externalUrl || "",
    imageUrl: row.imageUrl || "",
    isPublished: Boolean(row.isPublished),
  };
}

function toPayload(form) {
  if (form.isExternal) {
    const detected = detectSource(form.externalUrl);
    return {
      isExternal: true,
      title: form.title,
      summary: form.sourceSubtitle || form.summary || "",
      content: "",
      imageUrl: null,
      sourceName: form.sourceName || detected?.name || "",
      sourceSubtitle: form.sourceSubtitle || "",
      sourceLogoUrl: form.sourceLogoUrl || detected?.logo || null,
      sourceTitle: form.sourceTitle || "",
      thumbnailUrl: form.thumbnailUrl || null,
      externalUrl: form.externalUrl,
      isPublished: Boolean(form.isPublished),
    };
  }

  return {
    isExternal: false,
    title: form.title,
    summary: form.summary,
    content: form.content,
    imageUrl: form.imageUrl || null,
    sourceName: null,
    sourceSubtitle: null,
    sourceLogoUrl: null,
    sourceTitle: null,
    thumbnailUrl: null,
    externalUrl: null,
    isPublished: Boolean(form.isPublished),
  };
}

function publishedLabel(value) {
  if (!value) return "—";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "—";
  return date.toLocaleDateString("en-PG", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}

export default function NewsManagement() {
  const [articles, setArticles] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [apiError, setApiError] = useState(null);
  const [formOpen, setFormOpen] = useState(false);
  const [form, setForm] = useState(emptyForm);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState(null);
  const [editingId, setEditingId] = useState(null);

  const loadNews = useCallback(async () => {
    setIsLoading(true);
    try {
      const res = await apiFetch(`${API_ORIGIN}/api/news`);
      if (!res.ok) throw new Error(await readApiError(res));
      setArticles(await res.json());
      setApiError(null);
    } catch (err) {
      setApiError(isNetworkFailure(err) ? API_UNREACHABLE : (err.message || API_UNREACHABLE));
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    loadNews();
  }, [loadNews]);

  const openCreate = () => {
    setEditingId(null);
    setForm(emptyForm);
    setSaveError(null);
    setFormOpen(true);
  };

  const openEdit = (row) => {
    setEditingId(row.newsId ?? row.id);
    setForm(toForm(row));
    setSaveError(null);
    setFormOpen(true);
  };

  const applyExternalUrl = (externalUrl) => {
    const detected = detectSource(externalUrl);
    setForm((prev) => ({
      ...prev,
      externalUrl,
      sourceName: detected?.name || prev.sourceName,
      sourceLogoUrl: detected?.logo || prev.sourceLogoUrl,
    }));
  };

  const loadPreview = async (externalUrl) => {
    if (!externalUrl) return;
    try {
      const res = await apiFetch(
        `${API_ORIGIN}/api/news/preview?url=${encodeURIComponent(externalUrl)}`
      );
      if (!res.ok) return;
      const data = await res.json();
      setForm((prev) => ({
        ...prev,
        sourceName: data.sourceName || prev.sourceName,
        sourceLogoUrl: data.sourceLogoUrl || prev.sourceLogoUrl,
        sourceTitle: data.sourceTitle || prev.sourceTitle,
        sourceSubtitle: data.sourceSubtitle || prev.sourceSubtitle,
        thumbnailUrl: data.thumbnailUrl || prev.thumbnailUrl,
        title: prev.title || data.sourceTitle || "",
      }));
    } catch {
      /* preview is optional; save still fetches Open Graph on the API */
    }
  };

  const saveArticle = async (payload, id = editingId) => {
    const res = await apiFetch(
      id == null ? `${API_ORIGIN}/api/news` : `${API_ORIGIN}/api/news/${id}`,
      {
        method: id == null ? "POST" : "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      }
    );
    if (!res.ok) throw new Error(await readApiError(res));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSaving(true);
    setSaveError(null);
    try {
      await saveArticle(toPayload(form));
      setFormOpen(false);
      setForm(emptyForm);
      setEditingId(null);
      await loadNews();
    } catch (err) {
      setSaveError(err.message || "Couldn't save this article.");
    } finally {
      setSaving(false);
    }
  };

  const togglePublished = async (row) => {
    setSaveError(null);
    try {
      await saveArticle(
        toPayload({ ...toForm(row), isPublished: !row.isPublished }),
        row.newsId ?? row.id
      );
      await loadNews();
    } catch (err) {
      setApiError(err.message || "Couldn't update publish status.");
    }
  };

  const deleteArticle = async (row) => {
    const id = row.newsId ?? row.id;
    if (!window.confirm(`Delete “${row.title}”? This cannot be undone.`)) return;
    try {
      const res = await apiFetch(`${API_ORIGIN}/api/news/${id}`, { method: "DELETE" });
      if (!res.ok) throw new Error(await readApiError(res));
      await loadNews();
    } catch (err) {
      setApiError(err.message || "Couldn't delete this article.");
    }
  };

  return (
    <DashboardLayout title="News Management" navItems={MANAGEMENT_NAV}>
      <p style={{ color: "var(--text-light)", fontSize: 13, marginBottom: 18 }}>
        Internal college articles use a photo and full text. External media stories
        (Facebook, YouTube, Post Courier, The National, EMTV, NBC) use a source
        logo and Visit Source — no college image is required.
      </p>

      {apiError && <div className="records-error">{apiError}</div>}

      <div className="as-toolbar">
        <button type="button" className="as-add-btn" onClick={openCreate}>+ Add News</button>
      </div>

      {formOpen && (
        <form className="as-form" onSubmit={handleSubmit}>
          {saveError && <div className="as-error">{saveError}</div>}
          <h3 className="news-form-title">{editingId == null ? "Add News" : "Edit News"}</h3>
          <fieldset className="news-type">
            <legend>Story type</legend>
            <label>
              <input
                type="radio"
                name="news-type"
                checked={!form.isExternal}
                onChange={() => setForm({ ...form, isExternal: false })}
              />
              Internal News
            </label>
            <label>
              <input
                type="radio"
                name="news-type"
                checked={form.isExternal}
                onChange={() => setForm({ ...form, isExternal: true, imageUrl: "" })}
              />
              External News
            </label>
          </fieldset>
          <div className="as-form-fields news-form-fields">
            <label className="news-span-2">Title
              <input
                value={form.title}
                onChange={(e) => setForm({ ...form, title: e.target.value })}
                required={!form.isExternal}
              />
            </label>
            {form.isExternal ? (
              <>
                <label className="news-span-2">Source URL
                  <input
                    type="url"
                    value={form.externalUrl}
                    onChange={(e) => applyExternalUrl(e.target.value)}
                    onBlur={(e) => loadPreview(e.target.value)}
                    required
                    maxLength={1000}
                    placeholder="https://www.facebook.com/..."
                  />
                </label>
                <label>Source name
                  <input
                    value={form.sourceName}
                    onChange={(e) => setForm({ ...form, sourceName: e.target.value })}
                    maxLength={200}
                    placeholder="Detected from URL"
                  />
                </label>
                <label>Source subtitle
                  <input
                    value={form.sourceSubtitle}
                    onChange={(e) => setForm({ ...form, sourceSubtitle: e.target.value })}
                    maxLength={300}
                    placeholder="og:description"
                  />
                </label>
                <label className="news-span-2">Source title
                  <input
                    value={form.sourceTitle}
                    onChange={(e) => setForm({ ...form, sourceTitle: e.target.value })}
                    maxLength={500}
                    placeholder="Filled from og:title when available"
                  />
                </label>
                <label className="news-span-2">Thumbnail URL
                  <input
                    value={form.thumbnailUrl}
                    onChange={(e) => setForm({ ...form, thumbnailUrl: e.target.value })}
                    maxLength={1000}
                    placeholder="Filled from og:image when available"
                  />
                </label>
                {form.thumbnailUrl ? (
                  <div className="news-span-2 news-thumb-preview">
                    <img src={form.thumbnailUrl} alt="" />
                  </div>
                ) : null}
                <label className="news-span-2">Source logo URL (optional)
                  <input
                    value={form.sourceLogoUrl}
                    onChange={(e) => setForm({ ...form, sourceLogoUrl: e.target.value })}
                    maxLength={500}
                    placeholder="Filled automatically for known outlets"
                  />
                </label>
              </>
            ) : (
              <>
                <label>Image URL
                  <input
                    value={form.imageUrl}
                    onChange={(e) => setForm({ ...form, imageUrl: e.target.value })}
                    placeholder="images/college/lccpr.jpg"
                    maxLength={500}
                  />
                </label>
                <label className="news-span-2">Summary
                  <textarea
                    value={form.summary}
                    onChange={(e) => setForm({ ...form, summary: e.target.value })}
                    required
                    maxLength={500}
                    rows={3}
                  />
                </label>
                <label className="news-span-2">Content
                  <textarea
                    value={form.content}
                    onChange={(e) => setForm({ ...form, content: e.target.value })}
                    required
                    rows={8}
                  />
                </label>
              </>
            )}
            <label className="news-check">
              <input
                type="checkbox"
                checked={form.isPublished}
                onChange={(e) => setForm({ ...form, isPublished: e.target.checked })}
              />
              Publish
            </label>
          </div>
          <div className="as-form-actions">
            <button type="submit" className="as-save-btn" disabled={saving}>
              {saving ? "Saving…" : (editingId == null ? "Save" : "Save changes")}
            </button>
            <button type="button" className="as-cancel-btn" onClick={() => setFormOpen(false)}>Cancel</button>
          </div>
        </form>
      )}

      {isLoading ? (
        <p style={{ color: "var(--text-light)" }}>Loading news…</p>
      ) : (
        <table className="records-table">
          <thead>
            <tr>
              <th>Title</th>
              <th>Type</th>
              <th>Source</th>
              <th>Published</th>
              <th>Status</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {articles.length === 0 && (
              <tr>
                <td colSpan={6} style={{ color: "var(--text-light)" }}>No news articles yet.</td>
              </tr>
            )}
            {articles.map((row) => (
              <tr key={row.newsId ?? row.id}>
                <td>{row.title}</td>
                <td>{row.isExternal ? "External" : "Internal"}</td>
                <td>{row.isExternal ? (row.sourceName || "—") : "LCC"}</td>
                <td>{publishedLabel(row.publishedAt)}</td>
                <td>
                  <span className={row.isPublished ? "as-active-badge" : "news-draft-badge"}>
                    {row.isPublished ? "Published" : "Draft"}
                  </span>
                </td>
                <td>
                  <div className="records-actions">
                    <button type="button" className="records-edit-btn" onClick={() => openEdit(row)}>Edit</button>
                    <button type="button" className="records-edit-btn" onClick={() => togglePublished(row)}>
                      {row.isPublished ? "Unpublish" : "Publish"}
                    </button>
                    <button type="button" className="records-edit-btn" onClick={() => deleteArticle(row)}>Delete</button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </DashboardLayout>
  );
}
