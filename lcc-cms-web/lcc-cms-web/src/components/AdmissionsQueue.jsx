import { useState } from "react";
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

  return (
    <table className="admissions-table">
      <thead>
        <tr>
          <th>Applicant</th>
          <th>Programme</th>
          <th>Contact</th>
          <th>Documents</th>
          <th>Status</th>
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
              ) : (
                <span className="admissions-decided">Decided</span>
              )}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
