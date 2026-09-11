import { useEffect, useState } from "react";
import DashboardLayout from "../components/DashboardLayout";
import { API_ORIGIN, apiFetch } from "../api";
import { MANAGEMENT_NAV } from "./managementNav";
import "./StudentRecords.css";

function audienceLabel(targetRole) {
  return targetRole?.trim() ? targetRole : "All roles";
}

function createdDateLabel(postedAt) {
  if (!postedAt) return "—";
  const date = new Date(postedAt);
  if (Number.isNaN(date.getTime())) return "—";
  return date.toLocaleDateString("en-PG", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}

export default function Announcements() {
  const [notices, setNotices] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [apiError, setApiError] = useState(null);

  useEffect(() => {
    (async () => {
      setIsLoading(true);
      try {
        const res = await apiFetch(`${API_ORIGIN}/api/notices`);
        if (!res.ok) throw new Error(`API returned ${res.status}`);
        const data = await res.json();
        setNotices(Array.isArray(data) ? data : []);
        setApiError(null);
      } catch {
        setNotices([]);
        setApiError(
          "Couldn't reach the backend API. Make sure `dotnet run` is running on http://localhost:5000."
        );
      } finally {
        setIsLoading(false);
      }
    })();
  }, []);

  return (
    <DashboardLayout title="Announcements" navItems={MANAGEMENT_NAV}>
      {apiError && <div className="records-error">{apiError}</div>}
      {isLoading && (
        <p style={{ color: "var(--text-light)", marginBottom: 18 }}>
          Loading announcements…
        </p>
      )}

      <div className="dash-card-grid">
        <div className="dash-card">
          <h3>Announcements</h3>
          <div className="dash-card-value">{isLoading ? "—" : notices.length}</div>
        </div>
      </div>

      <p style={{ marginTop: 24, marginBottom: 14, color: "var(--text-light)", fontSize: 13 }}>
        Institutional notices from the existing notices API. Audience is the
        target role; notices with no role apply to everyone. Status is Posted
        once a notice exists in the table.
      </p>

      <table className="records-table">
        <thead>
          <tr>
            <th>Title</th>
            <th>Audience</th>
            <th>Created Date</th>
            <th>Status</th>
          </tr>
        </thead>
        <tbody>
          {notices.length === 0 && !isLoading && (
            <tr>
              <td colSpan={4} style={{ color: "var(--text-light)" }}>
                No announcements published yet.
              </td>
            </tr>
          )}
          {notices.map((notice) => (
            <tr key={notice.id}>
              <td>{notice.title}</td>
              <td>{audienceLabel(notice.targetRole)}</td>
              <td>{createdDateLabel(notice.postedAt)}</td>
              <td>Posted</td>
            </tr>
          ))}
        </tbody>
      </table>
    </DashboardLayout>
  );
}
