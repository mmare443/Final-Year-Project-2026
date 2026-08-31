import { createContext, useContext, useState, useEffect, useCallback } from "react";

/**
 * ADMISSIONS CONTEXT — M1, wired to the real ASP.NET Core Web API.
 *
 * The API stores applications in memory (see AdmissionsController.cs —
 * resets whenever `dotnet run` restarts), so nothing is durably persisted
 * yet, but this is a genuine frontend <-> backend HTTP round trip.
 *
 * DOCUMENT SET: matches Section 8 of LCCB's actual paper Application Form
 * for 2027 Enrolment — 7 required documents plus 1 conditional one. The
 * `key`s below must match Apply.jsx's DOCUMENT_FIELDS and the backend's
 * AdmissionApplicationRequest property names exactly (case-insensitive).
 *
 * PLACEHOLDER PROGRAMMES: still a placeholder list standing in for M3
 * (Academic Structure), which doesn't exist yet — unchanged from before.
 * (Apply.jsx defines its own PROGRAMMES list matching the real paper form;
 * this one is kept only for reference/consistency, not currently used.)
 *
 * NEXT SWAP (once EF Core + a real database exist): nothing here needs to
 * change. AdmissionsController.cs swaps its in-memory List<> for real EF
 * Core queries; this file keeps talking to the same endpoints.
 */

// Change this if your backend runs on a different port than the one
// `dotnet run` printed for you.
const API_ORIGIN = "http://localhost:5000";
const API_BASE = `${API_ORIGIN}/api`;

// Exported so components can build full URLs to uploaded files — e.g.
// `${API_ORIGIN}${doc.path}` — since each document's path from the API is
// root-relative (/uploads/admissions/<guid>.pdf), not a full URL.
export { API_ORIGIN };

export const MOCK_PROGRAMMES = [
  "Diploma in Applied Ministry",
  "Diploma in Tropical Agriculture",
  "Diploma in Business Administration and Management",
  "Certificate in Applied Ministry",
  "Certificate in Tropical Agriculture",
  "Certificate in Business Administration and Management",
];

const STATUS = {
  APPLIED: "Applied",
  APPROVED: "Approved",
  REJECTED: "Rejected",
};

const AdmissionsContext = createContext(null);

export function MockDataProvider({ children }) {
  const [applications, setApplications] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [apiError, setApiError] = useState(null);

  const refresh = useCallback(async () => {
    setIsLoading(true);
    try {
      const res = await fetch(`${API_BASE}/admissions`);
      if (!res.ok) throw new Error(`API returned ${res.status}`);
      const data = await res.json();
      setApplications(data);
      setApiError(null);
    } catch (err) {
      setApiError(
        "Couldn't reach the backend API. Make sure `dotnet run` is " +
        "running (see LCC_CMS_Api's README) on http://localhost:5000."
      );
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    refresh();
  }, [refresh]);

  // `documents` is an object like { letterOfInterest: File, passportPhoto:
  // File, ... } — keys matching Apply.jsx's DOCUMENT_FIELDS. Any key can
  // be missing/null (workReference is genuinely optional; the others are
  // required by the backend, which returns a 400 listing what's missing
  // rather than silently accepting an incomplete application — matching
  // LCCB's own paper-form rule).
  //
  // IMPORTANT: this uses FormData, not JSON.stringify. Files can't be
  // serialized into a JSON string, so any request that includes files
  // must be sent as multipart/form-data instead — the browser builds the
  // correct Content-Type (including the multipart boundary) automatically
  // when you pass a FormData object as the body, which is why there's no
  // explicit "Content-Type" header here, unlike decideApplication below.
  const addApplication = async (details, documents = {}) => {
    const formData = new FormData();
    formData.append("fullName", details.fullName);
    formData.append("email", details.email);
    formData.append("phone", details.phone);
    formData.append("programme", details.programme);

    for (const [key, file] of Object.entries(documents)) {
      if (file) formData.append(key, file);
    }

    const res = await fetch(`${API_BASE}/admissions`, {
      method: "POST",
      body: formData,
    });
    if (!res.ok) {
      // The backend returns a plain-text explanation for 400s (e.g. which
      // required documents are missing) — surface it instead of a generic
      // "something went wrong".
      const message = await res.text().catch(() => null);
      throw new Error(message || `API returned ${res.status}`);
    }
    const record = await res.json();
    setApplications((prev) => [record, ...prev]);
    return record;
  };

  const decideApplication = async (id, decision) => {
    const res = await fetch(`${API_BASE}/admissions/${id}/decision`, {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ decision }),
    });
    if (!res.ok) throw new Error(`API returned ${res.status}`);
    const updated = await res.json();
    setApplications((prev) =>
      prev.map((app) => (app.id === updated.id ? updated : app))
    );
  };

  const value = {
    applications,
    addApplication,
    decideApplication,
    STATUS,
    isLoading,
    apiError,
    refresh,
  };

  return (
    <AdmissionsContext.Provider value={value}>
      {children}
    </AdmissionsContext.Provider>
  );
}

export function useMockData() {
  const ctx = useContext(AdmissionsContext);
  if (!ctx) {
    throw new Error("useMockData must be used within a MockDataProvider");
  }
  return ctx;
}
