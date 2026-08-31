import { createContext, useContext, useState, useCallback } from "react";
import { API_ORIGIN } from "./MockDataContext";

/**
 * LEARNING CONTEXT — M6.
 * Same local-first / mock-auth pattern as the rest of the project.
 * Files go to the API as FormData (materials + submissions).
 */

const API_BASE = `${API_ORIGIN}/api/learning`;

const LearningContext = createContext(null);

export function LearningProvider({ children }) {
  const [materials, setMaterials] = useState([]);
  const [assignments, setAssignments] = useState([]);
  const [submissions, setSubmissions] = useState([]);
  const [summary, setSummary] = useState(null);
  const [apiError, setApiError] = useState(null);

  const handleError = () => {
    setApiError(
      "Couldn't reach the backend API. Make sure `dotnet run` is " +
        "running on http://localhost:5000."
    );
  };

  const fetchMaterials = useCallback(async ({ allocationId, studentId } = {}) => {
    try {
      const params = new URLSearchParams();
      if (allocationId) params.set("allocationId", allocationId);
      if (studentId) params.set("studentId", studentId);
      const qs = params.toString();
      const res = await fetch(`${API_BASE}/materials${qs ? `?${qs}` : ""}`);
      if (!res.ok) throw new Error(`API returned ${res.status}`);
      const data = await res.json();
      setMaterials(data);
      setApiError(null);
      return data;
    } catch {
      handleError();
      return [];
    }
  }, []);

  const fetchAssignments = useCallback(async ({ allocationId, studentId } = {}) => {
    try {
      const params = new URLSearchParams();
      if (allocationId) params.set("allocationId", allocationId);
      if (studentId) params.set("studentId", studentId);
      const qs = params.toString();
      const res = await fetch(`${API_BASE}/assignments${qs ? `?${qs}` : ""}`);
      if (!res.ok) throw new Error(`API returned ${res.status}`);
      const data = await res.json();
      setAssignments(data);
      setApiError(null);
      return data;
    } catch {
      handleError();
      return [];
    }
  }, []);

  const fetchSubmissions = useCallback(async (assignmentId) => {
    const res = await fetch(`${API_BASE}/assignments/${assignmentId}/submissions`);
    if (!res.ok) throw new Error(`API returned ${res.status}`);
    const data = await res.json();
    setSubmissions(data);
    return data;
  }, []);

  const fetchMySubmissions = useCallback(async (studentId) => {
    try {
      const res = await fetch(`${API_BASE}/submissions?studentId=${encodeURIComponent(studentId)}`);
      if (!res.ok) throw new Error(`API returned ${res.status}`);
      const data = await res.json();
      setSubmissions(data);
      setApiError(null);
      return data;
    } catch {
      handleError();
      return [];
    }
  }, []);

  const fetchSummary = useCallback(async (studentId) => {
    try {
      const url = studentId
        ? `${API_BASE}/summary?studentId=${encodeURIComponent(studentId)}`
        : `${API_BASE}/summary`;
      const res = await fetch(url);
      if (!res.ok) throw new Error(`API returned ${res.status}`);
      const data = await res.json();
      setSummary(data);
      return data;
    } catch {
      handleError();
      return null;
    }
  }, []);

  const uploadMaterial = async (allocationId, title, file) => {
    const form = new FormData();
    form.append("allocationId", allocationId);
    form.append("title", title);
    form.append("file", file);
    const res = await fetch(`${API_BASE}/materials`, { method: "POST", body: form });
    if (!res.ok) {
      const message = await res.text().catch(() => null);
      throw new Error(message || `API returned ${res.status}`);
    }
    const created = await res.json();
    setMaterials((prev) => [created, ...prev]);
    return created;
  };

  const deleteMaterial = async (id) => {
    const res = await fetch(`${API_BASE}/materials/${id}`, { method: "DELETE" });
    if (!res.ok) throw new Error(`API returned ${res.status}`);
    setMaterials((prev) => prev.filter((m) => m.id !== id));
  };

  const createAssignment = async (payload) => {
    const res = await fetch(`${API_BASE}/assignments`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
    if (!res.ok) {
      const message = await res.text().catch(() => null);
      throw new Error(message || `API returned ${res.status}`);
    }
    const created = await res.json();
    setAssignments((prev) => [...prev, created]);
    return created;
  };

  const updateAssignment = async (id, payload) => {
    const res = await fetch(`${API_BASE}/assignments/${id}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
    if (!res.ok) {
      const message = await res.text().catch(() => null);
      throw new Error(message || `API returned ${res.status}`);
    }
    const updated = await res.json();
    setAssignments((prev) => prev.map((a) => (a.id === updated.id ? updated : a)));
    return updated;
  };

  const deleteAssignment = async (id) => {
    const res = await fetch(`${API_BASE}/assignments/${id}`, { method: "DELETE" });
    if (!res.ok) throw new Error(`API returned ${res.status}`);
    setAssignments((prev) => prev.filter((a) => a.id !== id));
  };

  const submitWork = async (assignmentId, studentId, file) => {
    const form = new FormData();
    form.append("studentId", studentId);
    form.append("file", file);
    const res = await fetch(`${API_BASE}/assignments/${assignmentId}/submissions`, {
      method: "POST",
      body: form,
    });
    if (!res.ok) {
      const message = await res.text().catch(() => null);
      throw new Error(message || `API returned ${res.status}`);
    }
    const saved = await res.json();
    setSubmissions((prev) => {
      const without = prev.filter((s) => !(s.assignmentId === saved.assignmentId && s.studentId === saved.studentId));
      return [...without, saved];
    });
    return saved;
  };

  const gradeSubmission = async (id, marksAwarded, feedback) => {
    const res = await fetch(`${API_BASE}/submissions/${id}/grade`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ marksAwarded, feedback }),
    });
    if (!res.ok) {
      const message = await res.text().catch(() => null);
      throw new Error(message || `API returned ${res.status}`);
    }
    const updated = await res.json();
    setSubmissions((prev) => prev.map((s) => (s.id === updated.id ? updated : s)));
    return updated;
  };

  const value = {
    materials,
    assignments,
    submissions,
    summary,
    apiError,
    fetchMaterials,
    fetchAssignments,
    fetchSubmissions,
    fetchMySubmissions,
    fetchSummary,
    uploadMaterial,
    deleteMaterial,
    createAssignment,
    updateAssignment,
    deleteAssignment,
    submitWork,
    gradeSubmission,
  };

  return (
    <LearningContext.Provider value={value}>
      {children}
    </LearningContext.Provider>
  );
}

export function useLearning() {
  const ctx = useContext(LearningContext);
  if (!ctx) {
    throw new Error("useLearning must be used within a LearningProvider");
  }
  return ctx;
}
