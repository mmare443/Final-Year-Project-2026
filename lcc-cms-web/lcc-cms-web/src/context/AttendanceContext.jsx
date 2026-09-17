import { createContext, useContext, useState, useCallback } from "react";
import {
  API_ORIGIN,
  API_UNREACHABLE,
  apiFetch,
  isNetworkFailure,
  throwIfNotOk,
} from "../api";

/**
 * ATTENDANCE CONTEXT — M5.
 * Uses the shared apiFetch client (JWT + API_ORIGIN) like other modules.
 */

const API_BASE = `${API_ORIGIN}/api/attendance`;

const AttendanceContext = createContext(null);

export const ATTENDANCE_STATUSES = ["Present", "Absent", "Late", "Excused"];
export const ATTENDANCE_THRESHOLD = 75;

function describeAttendanceError(err, fallbackLabel) {
  if (err?.name === "AbortError" || /timeout|timed out/i.test(String(err?.message || ""))) {
    return "The attendance request timed out. Try again.";
  }
  if (isNetworkFailure(err)) {
    return "Attendance API is unavailable. Confirm the backend is running.";
  }
  const message = err?.message || String(err);
  if (/\(404\)/.test(message)) {
    return `Attendance endpoint missing (404). ${message}`;
  }
  if (/\(401\)/.test(message) || /unauthorized|sign in required/i.test(message)) {
    return message.includes("401") ? message : `Unauthorized (401). ${message}`;
  }
  if (/\(403\)/.test(message) || /do not have permission/i.test(message)) {
    return message;
  }
  return message || fallbackLabel || API_UNREACHABLE;
}

export function AttendanceProvider({ children }) {
  const [sessions, setSessions] = useState([]);
  const [sessionDetail, setSessionDetail] = useState(null);
  const [rates, setRates] = useState([]);
  const [alerts, setAlerts] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [apiError, setApiError] = useState(null);

  const handleError = (err) => {
    setApiError(describeAttendanceError(err));
  };

  const fetchSessions = useCallback(async (allocationId) => {
    setIsLoading(true);
    try {
      const url = allocationId
        ? `${API_BASE}/sessions?allocationId=${allocationId}`
        : `${API_BASE}/sessions`;
      const res = await apiFetch(url);
      await throwIfNotOk(res, "GET /api/attendance/sessions");
      const data = await res.json();
      setSessions(data);
      setApiError(null);
      return data;
    } catch (err) {
      handleError(err);
      return [];
    } finally {
      setIsLoading(false);
    }
  }, []);

  const fetchSession = useCallback(async (id) => {
    const res = await apiFetch(`${API_BASE}/sessions/${id}`);
    await throwIfNotOk(res, "GET /api/attendance/sessions");
    const data = await res.json();
    setSessionDetail(data);
    return data;
  }, []);

  const openSession = async (allocationId, sessionDate) => {
    const res = await apiFetch(`${API_BASE}/sessions`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ allocationId, sessionDate }),
    });
    await throwIfNotOk(res, "POST /api/attendance/sessions");
    const created = await res.json();
    setSessions((prev) => [created, ...prev]);
    return created;
  };

  const saveMarks = async (sessionId, marks) => {
    const res = await apiFetch(`${API_BASE}/sessions/${sessionId}/marks`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ marks }),
    });
    await throwIfNotOk(res, "PUT /api/attendance/sessions/marks");
    const detail = await res.json();
    setSessionDetail(detail);
    return detail;
  };

  const fetchRates = useCallback(async ({ studentId, allocationId } = {}) => {
    try {
      const params = new URLSearchParams();
      if (studentId) params.set("studentId", studentId);
      if (allocationId) params.set("allocationId", allocationId);
      const qs = params.toString();
      const res = await apiFetch(`${API_BASE}/rates${qs ? `?${qs}` : ""}`);
      await throwIfNotOk(res, "GET /api/attendance/rates");
      const data = await res.json();
      setRates(data);
      setApiError(null);
      return data;
    } catch (err) {
      handleError(err);
      return [];
    }
  }, []);

  const fetchAlerts = useCallback(async (studentId) => {
    try {
      const url = studentId
        ? `${API_BASE}/alerts?studentId=${encodeURIComponent(studentId)}`
        : `${API_BASE}/alerts`;
      const res = await apiFetch(url);
      await throwIfNotOk(res, "GET /api/attendance/alerts");
      const data = await res.json();
      setAlerts(data);
      setApiError(null);
      return data;
    } catch (err) {
      handleError(err);
      return [];
    }
  }, []);

  const fetchReport = useCallback(async ({ view, allocationId, studentId }) => {
    const params = new URLSearchParams({ view });
    if (allocationId) params.set("allocationId", allocationId);
    if (studentId) params.set("studentId", studentId);
    const res = await apiFetch(`${API_BASE}/reports?${params.toString()}`);
    await throwIfNotOk(res, "GET /api/attendance/reports");
    return res.json();
  }, []);

  const value = {
    sessions,
    sessionDetail,
    rates,
    alerts,
    isLoading,
    apiError,
    fetchSessions,
    fetchSession,
    openSession,
    saveMarks,
    fetchRates,
    fetchAlerts,
    fetchReport,
  };

  return (
    <AttendanceContext.Provider value={value}>
      {children}
    </AttendanceContext.Provider>
  );
}

export function useAttendance() {
  const ctx = useContext(AttendanceContext);
  if (!ctx) {
    throw new Error("useAttendance must be used within an AttendanceProvider");
  }
  return ctx;
}
