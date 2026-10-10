import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { apiFetch } from "../api";
import { useMockData, API_ORIGIN } from "../context/MockDataContext";
import { CONTACT } from "../config/contactConfig";
import "./AdmissionsQueue.css";

function shortRemark(value) {
  const text = (value || "").trim();
  if (!text) return "—";
  return text.length > 90 ? `${text.slice(0, 87)}…` : text;
}

function reviewLine(app) {
  const total = app.documentCount || 0;
  if (total === 0) return "No documents";
  const parts = [
    `${total} document${total === 1 ? "" : "s"}`,
    `${app.reviewedCount || 0} reviewed`,
    `${app.acceptableCount || 0} acceptable`,
    `${app.provisionalCount || 0} provisionally acceptable`,
    `${app.belowStandardCount || 0} does not meet standard`,
  ];
  if (app.missingCount) parts.push(`${app.missingCount} missing`);
  return parts.join(" · ");
}

export default function AdmissionsQueue() {
  const { applications, STATUS, isLoading, apiError, refresh } = useMockData();
  const [preview, setPreview] = useState(null);
  const [previewError, setPreviewError] = useState(null);

  useEffect(() => {
    refresh();
  }, [refresh]);

  const loadPreview = async (id) => {
    setPreviewError(null);
    try {
      const res = await apiFetch(`${API_ORIGIN}/api/admissions/${id}/activation-preview`);
      const text = await res.text();
      if (!res.ok) throw new Error(text || `Preview failed (${res.status}).`);
      setPreview(JSON.parse(text));
    } catch (err) {
      setPreview(null);
      setPreviewError(err.message || "Could not load onboarding preview.");
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
        No applications yet.
      </div>
    );
  }

  const localActivateHref = preview?.activationToken
    ? `/activate?token=${encodeURIComponent(preview.activationToken)}`
    : null;

  return (
    <>
      <p className="admissions-review-note">
        Open each application and review every submitted document before recording the Selection Committee decision.
        The college does not ask the applicant for a replacement copy during this assessment.
        WhatsApp delivery is not part of this version.
      </p>
      <table className="admissions-table">
        <thead>
          <tr>
            <th>Applicant</th>
            <th>Programme</th>
            <th>Contact</th>
            <th>Documents</th>
            <th>Application status</th>
            <th>Decision remark</th>
            <th>Email</th>
            <th>Student ID</th>
            <th></th>
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
              <td>{reviewLine(app)}</td>
              <td>
                <div>{app.status}</div>
                {app.selectionDecisionLabel && <div>{app.selectionDecisionLabel}</div>}
                {app.consistencyWarnings?.map((warning) => (
                  <div key={warning}>{warning}</div>
                ))}
                {app.status === STATUS.APPROVED && (
                  <button type="button" className="btn-preview" onClick={() => loadPreview(app.id)}>
                    Onboarding
                  </button>
                )}
              </td>
              <td>{shortRemark(app.decisionRemark)}</td>
              <td>
                {app.emailStatus || "Not sent"}
                {app.emailStatus === "Failed" ? " · Retry available" : ""}
              </td>
              <td>{app.studentId || "—"}</td>
              <td>
                <Link className="btn-approve" to={`/registrar/admissions/${app.id}`}>
                  Review Application
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      {previewError && <p className="admissions-preview-error">{previewError}</p>}
      {preview && (
        <div className="activation-preview">
          <h3>Existing onboarding</h3>
          <p>This application is already approved. Opening onboarding does not create another student.</p>
          <dl>
            <dt>Student number</dt>
            <dd>{preview.studentNumber}</dd>
            <dt>Onboarding status</dt>
            <dd>{preview.onboardingStatus}</dd>
            <dt>Activation link</dt>
            <dd>
              {preview.activationLink ? (
                <a href={localActivateHref || preview.activationLink}>{preview.activationLink}</a>
              ) : "—"}
            </dd>
            <dt>College contact</dt>
            <dd>
              {(preview.contact?.institution || CONTACT.institution)} · {preview.contact?.phone || CONTACT.phone}
            </dd>
          </dl>
        </div>
      )}
    </>
  );
}
