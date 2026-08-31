import { createContext, useContext, useState, useCallback } from "react";
import { API_ORIGIN } from "./MockDataContext";

/**
 * ATTENDANCE CONTEXT — M5.
 * Same local-first / mock-auth pattern as the rest of the project.
 */

const API_BASE = `${API_ORIGIN}/api/attendance`;

const AttendanceContext = createContext(null);

export const ATTENDANCE_STATUSES = ["Present", "Absent", "Late", "Excused"];
export const ATTENDANCE_THRESHOLD = 75;

export function AttendanceProvider({ children }) {
  const [sessions, setSessions] = useState([]);
  const [sessionDetail, setSessionDetail] = useState(null);
  const [rates, setRates] = useState([]);
  const [alerts, setAlerts] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [apiError, setApiError] = useState(null);

  const handleError = (err) => {
    setApiError(
      "Couldn't reach the backend API. Make sure `dotnet run` is " +
        "running on http://localhost:5000."
    );
  };

  const fetchSessions = useCallback(async (allocationId) => {
    setIsLoading(true);
    try {
      const url = allocationId
        ? `${API_BASE}/sessions?allocationId=${allocationId}`
        : `${API_BASE}/sessions`;
      const res = await fetch(url);
      if (!res.ok) throw new Error(`API returned ${res.status}`);
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
    const res = await fetch(`${API_BASE}/sessions/${id}`);
    if (!res.ok) throw new Error(`API returned ${res.status}`);
    const data = await res.json();
    setSessionDetail(data);
    return data;
  }, []);

  const openSession = async (allocationId, sessionDate) => {
    const res = await fetch(`${API_BASE}/sessions`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ allocationId, sessionDate }),
    });
    if (!res.ok) {
      const message = await res.text().catch(() => null);
      throw new Error(message || `API returned ${res.status}`);
    }
    const created = await res.json();
    setSessions((prev) => [created, ...prev]);
    return created;
  };

  const saveMarks = async (sessionId, marks) => {
    const res = await fetch(`${API_BASE}/sessions/${sessionId}/marks`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ marks }),
    });
    if (!res.ok) {
      const message = await res.text().catch(() => null);
      throw new Error(message || `API returned ${res.status}`);
    }
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
      const res = await fetch(`${API_BASE}/rates${qs ? `?${qs}` : ""}`);
      if (!res.ok) throw new Error(`API returned ${res.status}`);
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
      const res = await fetch(url);
      if (!res.ok) throw new Error(`API returned ${res.status}`);
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
    const res = await fetch(`${API_BASE}/reports?${params.toString()}`);
    if (!res.ok) {
      const message = await res.text().catch(() => null);
      throw new Error(message || `API returned ${res.status}`);
    }
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
