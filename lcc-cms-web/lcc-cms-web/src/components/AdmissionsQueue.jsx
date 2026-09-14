import { useState } from "react";
import { apiFetch } from "../api";
import { useMockData, API_ORIGIN } from "../context/MockDataContext";
import "./AdmissionsQueue.css";

function DocumentsCell({ documents }) {
  const [expanded, setExpanded] = useState(false);

  if (!documents || documents.length === 0) {
    return <span className="admissions-decided">None</span>;
  }

  return (
    <div className="documents-cell">
      <button
        className="documents-toggle"
        onClick={() => setExpanded((v) => !v)}
      >
        {documents.length} document{documents.length !== 1 ? "s" : ""} {expanded ? "▲" : "▼"}
      </button>
      {expanded && (
        <ul className="documents-list">
          {documents.map((doc) => (
            <li key={doc.path}>
              <a
                href={`${API_ORIGIN}${doc.path}`}
                target="_blank"
                rel="noopener noreferrer"
                className="document-link"
              >
                {doc.type}
              </a>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

export default function AdmissionsQueue() {
  const { applications, decideApplication, STATUS, isLoading, apiError, refresh } = useMockData();
  const [decidingId, setDecidingId] = useState(null);
  const [preview, setPreview] = useState(null);
  const [previewError, setPreviewError] = useState(null);
  const [previewingId, setPreviewingId] = useState(null);

  const loadPreview = async (id) => {
    setPreviewingId(id);
    setPreviewError(null);
    try {
      const res = await apiFetch(`${API_ORIGIN}/api/admissions/${id}/activation-preview`);
      const text = await res.text();
      if (!res.ok) {
        throw new Error(text || `Preview failed (${res.status}).`);
      }
      setPreview(JSON.parse(text));
    } catch (err) {
      setPreview(null);
      setPreviewError(err.message || "Could not load onboarding preview.");
    } finally {
      setPreviewingId(null);
    }
  };

  const handleDecision = async (id, decision) => {
    setDecidingId(id);
    try {
      await decideApplication(id, decision);
    } catch (err) {
      alert("Couldn't reach the backend API — check that dotnet run is still running.");
    } finally {
      setDecidingId(null);
    }
  };

  if (apiError) {
    return (
      <div className="admissions-error">
        {apiError}
        <button className="admissions-retry" onClick={refresh}>Retry</button>
      </div>
    );
  }

  if (isLoading) {
    return <div className="admissions-empty">Loading applications…</div>;
  }

  if (applications.length === 0) {
    return (
      <div className="admissions-empty">
        No applications yet. Submit one from the public{" "}
        <a href="/apply">Apply page</a> to see it appear here.
      </div>
    );
  }

  const localActivateHref = preview?.activationToken
    ? `/activate?token=${encodeURIComponent(preview.activationToken)}`
    : null;

  return (
    <>
    <table className="admissions-table">
      <thead>
        <tr>
          <th>Applicant</th>
          <th>Programme</th>
          <th>Contact</th>
          <th>Documents</th>
          <th>Status</th>
          <th>Onboarding</th>
          <th>Student ID</th>
          <th>Action</th>
        </tr>
      </thead>
      <tbody>
        {applications.map((app) => (
          <tr key={app.id}>
            <td>{app.fullName}</td>
            <td>{app.programme}</td>
            <td>
              <div>{app.email}</div>
              <div className="admissions-phone">{app.phone}</div>
            </td>
            <td>
              <DocumentsCell documents={app.documents} />
            </td>
            <td>
              <span className={`status-badge status-${app.status.toLowerCase()}`}>
                {app.status}
              </span>
            </td>
            <td>{app.onboardingStatus || "—"}</td>
            <td>{app.studentId || "—"}</td>
            <td>
              {app.status === STATUS.APPLIED ? (
                <div className="admissions-actions">
                  <button
                    className="btn-approve"
                    disabled={decidingId === app.id}
                    onClick={() => handleDecision(app.id, "approve")}
                  >
                    {decidingId === app.id ? "…" : "Approve"}
                  </button>
                  <button
                    className="btn-reject"
                    disabled={decidingId === app.id}
                    onClick={() => handleDecision(app.id, "reject")}
                  >
                    {decidingId === app.id ? "…" : "Reject"}
                  </button>
                </div>
              ) : app.status === STATUS.APPROVED ? (
                <button
                  className="btn-preview"
                  disabled={previewingId === app.id}
                  onClick={() => loadPreview(app.id)}
                >
                  {previewingId === app.id ? "…" : "Onboarding"}
                </button>
              ) : (
                <span className="admissions-decided">Decided</span>
              )}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
    {previewError && <p className="admissions-preview-error">{previewError}</p>}
    {preview && (
      <div className="activation-preview">
        <h3>Onboarding preview</h3>
        <dl>
          <dt>Student name</dt>
          <dd>{preview.studentName}</dd>
          <dt>Programme</dt>
          <dd>{preview.programme}</dd>
          <dt>Student number</dt>
          <dd>{preview.studentNumber}</dd>
          <dt>Allocated room</dt>
          <dd>{preview.allocatedRoom || (preview.hostel && preview.room ? `${preview.hostel} / ${preview.room}` : "Not allocated")}</dd>
          <dt>Onboarding status</dt>
          <dd>{preview.onboardingStatus}</dd>
          <dt>Activation token</dt>
          <dd className="activation-token">{preview.activationToken || "—"}</dd>
          <dt>Activation link</dt>
          <dd>
            {preview.activationLink ? (
              <a href={localActivateHref || preview.activationLink}>
                {preview.activationLink}
              </a>
            ) : (
              "—"
            )}
          </dd>
        </dl>
        {localActivateHref && (
          <p>
            Local demo:{" "}
            <a href={localActivateHref}>Open activate page</a>
          </p>
        )}
      </div>
    )}
    </>
  );
}
