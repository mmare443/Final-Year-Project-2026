import { createContext, useContext, useState, useCallback } from "react";
import { API_ORIGIN } from "./MockDataContext";

/**
 * REGISTRATIONS CONTEXT — M4.
 * Same local-first / mock-auth pattern as the rest of the project.
 */

const API_BASE = `${API_ORIGIN}/api/registrations`;

const RegistrationsContext = createContext(null);

export function RegistrationsProvider({ children }) {
  const [registrations, setRegistrations] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [apiError, setApiError] = useState(null);

  const fetchAll = useCallback(async (studentId) => {
    setIsLoading(true);
    try {
      const url = studentId ? `${API_BASE}?studentId=${encodeURIComponent(studentId)}` : API_BASE;
      const res = await fetch(url);
      if (!res.ok) throw new Error(`API returned ${res.status}`);
      const data = await res.json();
      setRegistrations(data);
      setApiError(null);
    } catch (err) {
      setApiError(
        "Couldn't reach the backend API. Make sure `dotnet run` is " +
        "running on http://localhost:5000."
      );
    } finally {
      setIsLoading(false);
    }
  }, []);

  const register = async (request) => {
    const res = await fetch(API_BASE, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request),
    });
    if (!res.ok) {
      const message = await res.text().catch(() => null);
      throw new Error(message || `API returned ${res.status}`);
    }
    const created = await res.json();
    setRegistrations((prev) => [created, ...prev]);
    return created;
  };

  const dropRegistration = async (id) => {
    const res = await fetch(`${API_BASE}/${id}`, { method: "DELETE" });
    if (!res.ok) {
      const message = await res.text().catch(() => null);
      throw new Error(message || `API returned ${res.status}`);
    }
    const updated = await res.json();
    setRegistrations((prev) => prev.map((r) => (r.id === updated.id ? updated : r)));
    return updated;
  };

  const decide = async (id, decision, reason) => {
    const res = await fetch(`${API_BASE}/${id}/decision`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ decision, reason }),
    });
    if (!res.ok) {
      const message = await res.text().catch(() => null);
      throw new Error(message || `API returned ${res.status}`);
    }
    const updated = await res.json();
    setRegistrations((prev) => prev.map((r) => (r.id === updated.id ? updated : r)));
    return updated;
  };

  const value = { registrations, isLoading, apiError, fetchAll, register, dropRegistration, decide };

  return (
    <RegistrationsContext.Provider value={value}>
      {children}
    </RegistrationsContext.Provider>
  );
}

export function useRegistrations() {
  const ctx = useContext(RegistrationsContext);
  if (!ctx) {
    throw new Error("useRegistrations must be used within a RegistrationsProvider");
  }
  return ctx;
}
