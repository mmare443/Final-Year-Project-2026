import { useCallback, useEffect, useState } from "react";
import { API_ORIGIN, apiFetch } from "../api";

const SEEN_PREFIX = "lcc_announce_seen:";

function seenKey(email) {
  return `${SEEN_PREFIX}${email || "anon"}`;
}

function latestStamp(items) {
  return (items || []).reduce((max, item) => {
    const raw = item.updatedAt || item.createdAt || item.publishedAt || "";
    const t = Date.parse(raw);
    return Number.isFinite(t) && t > max ? t : max;
  }, 0);
}

export function unseenAnnouncementCount(items, email) {
  const seen = Number(localStorage.getItem(seenKey(email)) || "0");
  return (items || []).filter((item) => {
    const t = Date.parse(item.updatedAt || item.createdAt || "");
    return Number.isFinite(t) && t > seen;
  }).length;
}

export function markAnnouncementsSeen(items, email) {
  const latest = latestStamp(items);
  if (latest) localStorage.setItem(seenKey(email), String(latest));
}

export function useAnnouncementFeed() {
  const [items, setItems] = useState([]);
  const [isLoading, setIsLoading] = useState(true);

  const reload = useCallback(async () => {
    try {
      const res = await apiFetch(`${API_ORIGIN}/api/announcements/portal`);
      if (!res.ok) throw new Error("feed failed");
      const data = await res.json();
      setItems(Array.isArray(data) ? data : []);
    } catch {
      setItems([]);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    reload();
  }, [reload]);

  return { items, isLoading, reload };
}
