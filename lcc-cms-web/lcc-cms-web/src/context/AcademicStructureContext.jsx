import { createContext, useContext, useState, useCallback } from "react";
import { API_ORIGIN } from "./MockDataContext";

/**
 * ACADEMIC STRUCTURE CONTEXT — M3.
 *
 * Covers all 7 entity types the spec defines: faculties, departments,
 * programmes, courses, academic years, semesters, course allocations.
 * Same local-first / mock-auth context as everywhere else — see
 * MockDataContext.jsx's header comment for the general pattern.
 */

const API_BASE = `${API_ORIGIN}/api/academic-structure`;

const AcademicStructureContext = createContext(null);

const ENTITY_ENDPOINTS = {
  faculties: "faculties",
  departments: "departments",
  programmes: "programmes",
  courses: "courses",
  academicYears: "academic-years",
  semesters: "semesters",
  courseAllocations: "course-allocations",
};

export function AcademicStructureProvider({ children }) {
  const [data, setData] = useState({
    faculties: [], departments: [], programmes: [], courses: [],
    academicYears: [], semesters: [], courseAllocations: [],
  });
  const [isLoading, setIsLoading] = useState(false);
  const [apiError, setApiError] = useState(null);

  const fetchAll = useCallback(async () => {
    setIsLoading(true);
    try {
      const entries = Object.entries(ENTITY_ENDPOINTS);
      const results = await Promise.all(
        entries.map(([, endpoint]) => fetch(`${API_BASE}/${endpoint}`).then((r) => {
          if (!r.ok) throw new Error(`API returned ${r.status}`);
          return r.json();
        }))
      );
      const next = {};
      entries.forEach(([key], i) => { next[key] = results[i]; });
      setData(next);
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

  const create = async (entityKey, payload) => {
    const endpoint = ENTITY_ENDPOINTS[entityKey];
    const res = await fetch(`${API_BASE}/${endpoint}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
    if (!res.ok) {
      const message = await res.text().catch(() => null);
      throw new Error(message || `API returned ${res.status}`);
    }
    const created = await res.json();
    setData((prev) => ({ ...prev, [entityKey]: [...prev[entityKey], created] }));
    return created;
  };

  const activateSemester = async (id) => {
    const res = await fetch(`${API_BASE}/semesters/${id}/activate`, { method: "PUT" });
    if (!res.ok) throw new Error(`API returned ${res.status}`);
    await fetchAll(); // re-fetch since activating one deactivates all others
  };

  const value = { ...data, isLoading, apiError, fetchAll, create, activateSemester };

  return (
    <AcademicStructureContext.Provider value={value}>
      {children}
    </AcademicStructureContext.Provider>
  );
}

export function useAcademicStructure() {
  const ctx = useContext(AcademicStructureContext);
  if (!ctx) {
    throw new Error("useAcademicStructure must be used within an AcademicStructureProvider");
  }
  return ctx;
}
