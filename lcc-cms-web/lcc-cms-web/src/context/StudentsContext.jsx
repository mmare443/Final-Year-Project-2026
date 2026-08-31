import { createContext, useContext, useState, useCallback } from "react";
import { API_ORIGIN } from "./MockDataContext";

/**
 * STUDENTS CONTEXT — M2, wired to the real ASP.NET Core Web API.
 *
 * Mirrors MockDataContext's pattern (same API_ORIGIN, same FormData
 * approach for the photo upload, same error-surfacing). See that file's
 * header comment for the general local-first / mock-auth context this
 * fits into — not repeated here.
 *
 * "/me" always resolves to the single seeded demo profile right now,
 * since there's no real per-user Entra ID session yet — see the RBAC
 * NOTE in StudentsController.cs for the exact swap-over plan.
 */

const API_BASE = `${API_ORIGIN}/api`;

const StudentsContext = createContext(null);

export function StudentsProvider({ children }) {
  const [myProfile, setMyProfile] = useState(null);
  const [allStudents, setAllStudents] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [apiError, setApiError] = useState(null);

  const fetchMyProfile = useCallback(async () => {
    setIsLoading(true);
    try {
      const res = await fetch(`${API_BASE}/students/me`);
      if (!res.ok) throw new Error(`API returned ${res.status}`);
      const data = await res.json();
      setMyProfile(data);
      setApiError(null);
      return data;
    } catch (err) {
      setApiError(
        "Couldn't reach the backend API. Make sure `dotnet run` is " +
        "running on http://localhost:5000."
      );
      return null;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const updateMyProfile = async (edits) => {
    const res = await fetch(`${API_BASE}/students/me`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(edits),
    });
    if (!res.ok) {
      const message = await res.text().catch(() => null);
      throw new Error(message || `API returned ${res.status}`);
    }
    const updated = await res.json();
    setMyProfile(updated);
    return updated;
  };

  const uploadMyPhoto = async (file) => {
    const formData = new FormData();
    formData.append("photo", file);
    const res = await fetch(`${API_BASE}/students/me/photo`, {
      method: "POST",
      body: formData,
    });
    if (!res.ok) {
      const message = await res.text().catch(() => null);
      throw new Error(message || `API returned ${res.status}`);
    }
    const updated = await res.json();
    setMyProfile(updated);
    return updated;
  };

  const fetchAllStudents = useCallback(async () => {
    setIsLoading(true);
    try {
      const res = await fetch(`${API_BASE}/students`);
      if (!res.ok) throw new Error(`API returned ${res.status}`);
      const data = await res.json();
      setAllStudents(data);
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

  const correctStudentProfile = async (id, edits) => {
    const res = await fetch(`${API_BASE}/students/${id}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(edits),
    });
    if (!res.ok) {
      const message = await res.text().catch(() => null);
      throw new Error(message || `API returned ${res.status}`);
    }
    const updated = await res.json();
    setAllStudents((prev) => prev.map((s) => (s.id === updated.id ? updated : s)));
    return updated;
  };

  const value = {
    myProfile,
    allStudents,
    isLoading,
    apiError,
    fetchMyProfile,
    updateMyProfile,
    uploadMyPhoto,
    fetchAllStudents,
    correctStudentProfile,
  };

  return (
    <StudentsContext.Provider value={value}>
      {children}
    </StudentsContext.Provider>
  );
}

export function useStudents() {
  const ctx = useContext(StudentsContext);
  if (!ctx) {
    throw new Error("useStudents must be used within a StudentsProvider");
  }
  return ctx;
}
