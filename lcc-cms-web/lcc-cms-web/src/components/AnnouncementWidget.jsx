import { useEffect } from "react";
import { useMockAuth } from "../context/MockAuthContext";
import { markAnnouncementsSeen, useAnnouncementFeed } from "../hooks/useAnnouncementFeed";
import "./AnnouncementWidget.css";

function formatDate(value) {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return String(value);
  return date.toLocaleDateString("en-PG", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}

function excerpt(content, max = 180) {
  const text = String(content || "").replace(/\s+/g, " ").trim();
  if (text.length <= max) return text;
  return `${text.slice(0, max).trim()}…`;
}

export default function AnnouncementWidget({ title = "Latest Announcements" }) {
  const { email } = useMockAuth();
  const { items, isLoading } = useAnnouncementFeed();

  useEffect(() => {
    if (!isLoading && items.length) markAnnouncementsSeen(items, email);
  }, [isLoading, items, email]);

  return (
    <section className="announce-widget">
      <h2 className="announce-widget-title">{title}</h2>
      {isLoading && <p className="announce-empty">Loading announcements…</p>}
      {!isLoading && items.length === 0 && (
        <p className="announce-empty">No current announcements.</p>
      )}
      <ul className="announce-list">
        {items.slice(0, 5).map((item) => (
          <li key={item.id || item.announcementId} className="announce-item">
            <div className="announce-item-head">
              <h3>{item.title}</h3>
              <span className={`announce-priority announce-priority-${item.priority || "Normal"}`}>
                {item.priority || "Normal"}
              </span>
            </div>
            <p className="announce-meta">
              {item.startDate ? `From ${formatDate(item.startDate)}` : formatDate(item.createdAt)}
              {item.endDate ? ` · Until ${formatDate(item.endDate)}` : ""}
            </p>
            <p className="announce-body">{excerpt(item.content)}</p>
          </li>
        ))}
      </ul>
    </section>
  );
}
