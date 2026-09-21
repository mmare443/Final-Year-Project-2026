import { useCallback, useEffect, useState } from "react";
import DashboardLayout from "../components/DashboardLayout";
import { API_ORIGIN, API_UNREACHABLE, apiFetch, isNetworkFailure, throwIfNotOk } from "../api";
import { MANAGEMENT_NAV } from "./managementNav";
import "./AcademicStructure.css";
import "./StudentRecords.css";

const AUDIENCES = ["PUBLIC", "STUDENT", "STAFF", "EVERYONE"];
const PRIORITIES = ["Normal", "High", "Urgent"];

const emptyForm = {
  title: "",
  content: "",
  audience: "EVERYONE",
  startDate: "",
  endDate: "",
  priority: "Normal",
};

function toForm(row) {
  return {
    title: row.title || "",
    content: row.content || "",
    audience: row.audience || "EVERYONE",
    startDate: row.startDate ? String(row.startDate).slice(0, 10) : "",
    endDate: row.endDate ? String(row.endDate).slice(0, 10) : "",
    priority: row.priority || "Normal",
  };
}

function toPayload(form) {
  return {
    title: form.title,
    content: form.content,
    audience: form.audience,
    startDate: form.startDate || null,
    endDate: form.endDate || null,
    priority: form.priority,
  };
}

function dateLabel(value) {
  if (!value) return "—";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return String(value);
  return date.toLocaleDateString("en-PG", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}

function statusLabel(row) {
  if (row.isArchived) return "Archived";
  if (row.isPublished) return "Published";
  return "Draft";
}

export default function Announcements() {
  const [rows, setRows] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [apiError, setApiError] = useState(null);
  const [formOpen, setFormOpen] = useState(false);
  const [form, setForm] = useState(emptyForm);
  const [editingId, setEditingId] = useState(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState(null);
  const [filter, setFilter] = useState("active");

  const loadRows = useCallback(async () => {
    setIsLoading(true);
    try {
      const res = await apiFetch(`${API_ORIGIN}/api/announcements`);
      await throwIfNotOk(res);
      const data = await res.json();
      setRows(Array.isArray(data) ? data : []);
      setApiError(null);
    } catch (err) {
      setRows([]);
      setApiError(isNetworkFailure(err) ? API_UNREACHABLE : (err.message || API_UNREACHABLE));
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    loadRows();
  }, [loadRows]);

  const openCreate = () => {
    setEditingId(null);
    setForm(emptyForm);
    setSaveError(null);
    setFormOpen(true);
  };

  const openEdit = (row) => {
    setEditingId(row.id ?? row.announcementId);
    setForm(toForm(row));
    setSaveError(null);
    setFormOpen(true);
  };

  const saveRow = async (event) => {
    event.preventDefault();
    setSaving(true);
    setSaveError(null);
    try {
      const res = await apiFetch(
        editingId == null ? `${API_ORIGIN}/api/announcements` : `${API_ORIGIN}/api/announcements/${editingId}`,
        {
          method: editingId == null ? "POST" : "PUT",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(toPayload(form)),
        }
      );
      await throwIfNotOk(res);
      setFormOpen(false);
      await loadRows();
    } catch (err) {
      setSaveError(err.message || "Couldn't save the announcement.");
    } finally {
      setSaving(false);
    }
  };

  const action = async (row, path) => {
    try {
      const id = row.id ?? row.announcementId;
      const res = await apiFetch(`${API_ORIGIN}/api/announcements/${id}/${path}`, { method: "PUT" });
      await throwIfNotOk(res);
      await loadRows();
    } catch (err) {
      setApiError(err.message || "Couldn't update the announcement.");
    }
  };

  const deleteRow = async (row) => {
    if (!window.confirm(`Delete “${row.title}”? This cannot be undone.`)) return;
    try {
      const id = row.id ?? row.announcementId;
      const res = await apiFetch(`${API_ORIGIN}/api/announcements/${id}`, { method: "DELETE" });
      await throwIfNotOk(res);
      await loadRows();
    } catch (err) {
      setApiError(err.message || "Couldn't delete the announcement.");
    }
  };

  const visible = rows.filter((row) => {
    if (filter === "archived") return row.isArchived;
    if (filter === "draft") return !row.isPublished && !row.isArchived;
    if (filter === "active") return row.isPublished && !row.isArchived;
    return true;
  });

  return (
    <DashboardLayout title="Announcement Management" navItems={MANAGEMENT_NAV}>
      {apiError && <div className="records-error">{apiError}</div>}

      <div className="as-toolbar">
        <button type="button" className="as-add-btn" onClick={openCreate}>
          Add Announcement
        </button>
      </div>

      <div className="as-tabs">
        {[
          { id: "active", label: "Published" },
          { id: "draft", label: "Drafts" },
          { id: "archived", label: "Archived" },
          { id: "all", label: "All" },
        ].map((tab) => (
          <button
            key={tab.id}
            type="button"
            className={`as-tab${filter === tab.id ? " as-tab-active" : ""}`}
            onClick={() => setFilter(tab.id)}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {formOpen && (
        <form className="as-form" onSubmit={saveRow}>
          <div className="as-form-fields">
            <label className="as-form-span">
              Title
              <input
                value={form.title}
                onChange={(e) => setForm({ ...form, title: e.target.value })}
                maxLength={150}
                required
              />
            </label>
            <label>
              Audience
              <select
                value={form.audience}
                onChange={(e) => setForm({ ...form, audience: e.target.value })}
              >
                {AUDIENCES.map((item) => (
                  <option key={item} value={item}>{item}</option>
                ))}
              </select>
            </label>
            <label>
              Priority
              <select
                value={form.priority}
                onChange={(e) => setForm({ ...form, priority: e.target.value })}
              >
                {PRIORITIES.map((item) => (
                  <option key={item} value={item}>{item}</option>
                ))}
              </select>
            </label>
            <label>
              Start Date
              <input
                type="date"
                value={form.startDate}
                onChange={(e) => setForm({ ...form, startDate: e.target.value })}
              />
            </label>
            <label>
              End Date
              <input
                type="date"
                value={form.endDate}
                onChange={(e) => setForm({ ...form, endDate: e.target.value })}
              />
            </label>
            <label className="as-form-span">
              Content
              <textarea
                value={form.content}
                onChange={(e) => setForm({ ...form, content: e.target.value })}
                rows={5}
                required
              />
            </label>
          </div>
          {saveError && <div className="as-error">{saveError}</div>}
          <div className="as-form-actions">
            <button type="submit" className="as-save-btn" disabled={saving}>
              {saving ? "Saving…" : "Save"}
            </button>
            <button type="button" className="as-cancel-btn" onClick={() => setFormOpen(false)}>
              Cancel
            </button>
          </div>
        </form>
      )}

      {isLoading && <p style={{ color: "var(--text-light)" }}>Loading announcements…</p>}

      <table className="records-table">
        <thead>
          <tr>
            <th>Title</th>
            <th>Audience</th>
            <th>Priority</th>
            <th>Start</th>
            <th>End</th>
            <th>Status</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {visible.length === 0 && !isLoading && (
            <tr>
              <td colSpan={7} style={{ color: "var(--text-light)" }}>
                No announcements in this view.
              </td>
            </tr>
          )}
          {visible.map((row) => (
            <tr key={row.id ?? row.announcementId}>
              <td>{row.title}</td>
              <td>{row.audience}</td>
              <td>{row.priority}</td>
              <td>{dateLabel(row.startDate)}</td>
              <td>{dateLabel(row.endDate)}</td>
              <td>{statusLabel(row)}</td>
              <td>
                <button type="button" className="records-edit-btn" onClick={() => openEdit(row)}>Edit</button>
                {" "}
                {!row.isPublished && !row.isArchived && (
                  <button type="button" className="records-edit-btn" onClick={() => action(row, "publish")}>Publish</button>
                )}
                {" "}
                {!row.isArchived && (
                  <button type="button" className="records-edit-btn" onClick={() => action(row, "archive")}>Archive</button>
                )}
                {row.isArchived && (
                  <button type="button" className="records-edit-btn" onClick={() => action(row, "restore")}>Restore</button>
                )}
                {" "}
                <button type="button" className="records-edit-btn" onClick={() => deleteRow(row)}>Delete</button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </DashboardLayout>
  );
}
