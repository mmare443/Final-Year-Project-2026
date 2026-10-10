import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import DashboardLayout from "../components/DashboardLayout";
import { API_ORIGIN, apiFetch } from "../api";
import { REGISTRAR_NAV } from "./registrarNav";
import "../components/AdmissionsQueue.css";

const OUTCOMES = [
  ["Acceptable", "Acceptable for application assessment"],
  ["UnclearButProvisionallyAcceptable", "Unclear, provisionally acceptable"],
  ["DoesNotMeetStandard", "Does not meet standard"],
  ["Missing", "Missing"],
];

const DECISIONS = [
  ["ApplicationSuccessful", "Application Successful"],
  ["ApplicationSuccessfulPendingOriginalVerification", "Application Successful, Subject to Original Document Verification"],
  ["ApplicationUnsuccessful", "Application Unsuccessful"],
  ["PendingCommitteeReview", "Pending Committee Review"],
];

function formatWhen(value) {
  if (!value) return "—";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

export default function AdmissionReview() {
  const { admissionId } = useParams();
  const [detail, setDetail] = useState(null);
  const [error, setError] = useState(null);
  const [decision, setDecision] = useState("");
  const [remark, setRemark] = useState("");
  const [overrideReason, setOverrideReason] = useState("");
  const [preview, setPreview] = useState(null);
  const [viewer, setViewer] = useState(null);
  const [zoom, setZoom] = useState(1);
  const [saving, setSaving] = useState(false);
  const [onboarding, setOnboarding] = useState(null);

  const load = async () => {
    const res = await apiFetch(`${API_ORIGIN}/api/admissions/${admissionId}/review`);
    const text = await res.text();
    if (!res.ok) throw new Error(text || `Could not open the application (${res.status}).`);
    const body = JSON.parse(text);
    setDetail(body);
    setDecision(body.selectionDecision || "");
    setRemark(body.decisionRemark || "");
    setOverrideReason(body.decisionOverrideReason || "");
    return body;
  };

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const body = await load();
        if (!cancelled) setError(null);
        if (!cancelled && body.selectionDecision) await loadPreview(body.selectionDecision, body.decisionRemark || "");
      } catch (err) {
        if (!cancelled) setError(err.message || "Could not open the application.");
      }
    })();
    return () => { cancelled = true; };
  }, [admissionId]);

  useEffect(() => () => {
    if (viewer?.url) URL.revokeObjectURL(viewer.url);
  }, [viewer]);

    const loadPreview = async (nextDecision, nextRemark) => {
    if (!nextDecision || nextDecision === "PendingCommitteeReview") {
      setPreview(null);
      return null;
    }
    const params = new URLSearchParams({ decision: nextDecision, remark: nextRemark || "" });
    const res = await apiFetch(`${API_ORIGIN}/api/admissions/${admissionId}/email-preview?${params}`);
    const text = await res.text();
    if (!res.ok) throw new Error(text || "Could not preview the email.");
    const body = JSON.parse(text);
    setPreview(body);
    return body;
  };

  const openDocument = async (doc, download) => {
    const res = await apiFetch(
      `${API_ORIGIN}/api/admissions/${admissionId}/documents/${doc.documentId}/content${download ? "?download=true" : ""}`
    );
    if (!res.ok) {
      const text = await res.text();
      throw new Error(text || `Could not open the document (${res.status}).`);
    }
    const blob = await res.blob();
    const url = URL.createObjectURL(blob);
    if (download) {
      const link = document.createElement("a");
      link.href = url;
      link.download = doc.fileName || "document";
      link.click();
      URL.revokeObjectURL(url);
    } else {
      if (viewer?.url) URL.revokeObjectURL(viewer.url);
      setZoom(1);
      setViewer({ url, name: doc.fileName, type: doc.contentType || blob.type });
    }
    await load();
  };

  const saveReview = async (doc, outcome, reviewRemark) => {
    setSaving(true);
    setError(null);
    try {
      const res = await apiFetch(
        `${API_ORIGIN}/api/admissions/${admissionId}/documents/${doc.documentId}/review`,
        {
          method: "PUT",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ outcome, remark: reviewRemark }),
        }
      );
      const text = await res.text();
      if (!res.ok) throw new Error(text || "Could not save the document review.");
      setDetail(JSON.parse(text));
    } catch (err) {
      setError(err.message || "Could not save the document review.");
    } finally {
      setSaving(false);
    }
  };

  const loadOnboarding = async () => {
    const res = await apiFetch(`${API_ORIGIN}/api/admissions/${admissionId}/activation-preview`);
    const text = await res.text();
    if (!res.ok) throw new Error(text || "Could not open the existing onboarding record.");
    setOnboarding(JSON.parse(text));
  };

  const beginOnboarding = async () => {
    const confirmed = window.confirm(
      "This will approve the applicant and proceed to the existing student onboarding process. The applicant must present all original documents during School Registration Week. Continue?"
    );
    if (!confirmed) return;
    setSaving(true);
    setError(null);
    try {
      const res = await apiFetch(`${API_ORIGIN}/api/admissions/${admissionId}/decision`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ decision: "approve" }),
      });
      const text = await res.text();
      if (!res.ok) throw new Error(text || "Could not begin onboarding.");
      await load();
      await loadOnboarding();
    } catch (err) {
      setError(err.message || "Could not begin onboarding.");
    } finally {
      setSaving(false);
    }
  };

  const saveDecision = async (sendEmail) => {
    if (!decision) {
      setError("Select the final application decision.");
      return;
    }
    if (sendEmail && !window.confirm("Send this email to the applicant now?")) return;
    setSaving(true);
    setError(null);
    try {
      const res = await apiFetch(`${API_ORIGIN}/api/admissions/${admissionId}/selection-decision`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          decision,
          remark,
          overrideReason: detail?.allDocumentsAddressed ? "" : overrideReason,
          sendEmail,
        }),
      });
      const text = await res.text();
      if (!res.ok) throw new Error(text || "Could not save the decision.");
      const body = JSON.parse(text);
      setDetail(body);
      if (body.emailStatus === "Failed") {
        setError(body.emailFailure || "The decision was saved. The email was not sent.");
      }
    } catch (err) {
      setError(err.message || "Could not save the decision.");
    } finally {
      setSaving(false);
    }
  };

  const retryEmail = async () => {
    if (!detail?.emailLogId) return;
    if (!window.confirm("Send the admission email again?")) return;
    setSaving(true);
    setError(null);
    try {
      const res = await apiFetch(
        `${API_ORIGIN}/api/admissions/${admissionId}/emails/${detail.emailLogId}/retry`,
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ confirm: true }),
        }
      );
      const text = await res.text();
      if (!res.ok) throw new Error(text || "Could not send the email again.");
      const body = JSON.parse(text);
      setDetail(body);
      if (body.emailStatus === "Failed") setError(body.emailFailure || "The email was not sent. The decision is unchanged.");
    } catch (err) {
      setError(err.message || "Could not send the email again.");
    } finally {
      setSaving(false);
    }
  };

  const image = viewer && String(viewer.type).startsWith("image/");
  const pdf = viewer && viewer.type === "application/pdf";

  return (
    <DashboardLayout title="Review Application" navItems={REGISTRAR_NAV}>
      <p><Link to="/registrar/admissions">Back to admissions</Link></p>
      {error && <div className="admissions-error">{error}</div>}
      {!detail ? (
        <p>Loading application…</p>
      ) : (
        <>
          <section className="dash-card">
            <h2>{detail.applicantName}</h2>
            <p>{detail.programme}</p>
            <p>{detail.email} · {detail.phone || "No phone"}</p>
            <p>Application reference {detail.applicationReference}</p>
            <p>Enrolment status: {detail.status}. Student ID: {detail.studentNumber || "—"}</p>
            {detail.selectionDecisionLabel && <p>Selection decision: {detail.selectionDecisionLabel}</p>}
            <p>Email: {detail.emailStatus}{detail.canRetryEmail ? " · Retry available" : ""}</p>
            {detail.originalVerificationOutstanding && (
              <p>
                Original document verification remains outstanding. The applicant must present all original documents during School Registration Week.
                The uploaded copies have not been proved genuine.
              </p>
            )}
            {detail.consistencyWarnings?.length > 0 && (
              <ul>
                {detail.consistencyWarnings.map((warning) => <li key={warning}>{warning}</li>)}
              </ul>
            )}
            {detail.warnings?.length > 0 && (
              <ul>
                {detail.warnings.map((warning) => <li key={warning}>{warning}</li>)}
              </ul>
            )}
            <p>
              Uploaded copies are assessed for clarity and completeness. Acceptable means acceptable for application assessment.
              It does not mean the document has been proved genuine.
            </p>
          </section>

          <table className="admissions-table">
            <thead>
              <tr>
                <th>Document</th>
                <th>File</th>
                <th>Uploaded</th>
                <th>Outcome</th>
                <th>Remark</th>
                <th>Reviewed</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {detail.documents.map((doc) => (
                <DocumentRow
                  key={doc.documentId}
                  doc={doc}
                  disabled={saving}
                  onSave={saveReview}
                  onOpen={openDocument}
                />
              ))}
            </tbody>
          </table>

          <section className="dash-card">
            <h3>Final application decision</h3>
            <label>
              Decision
              <select
                value={decision}
                onChange={(event) => {
                  const next = event.target.value;
                  setDecision(next);
                  loadPreview(next, remark)
                    .then((body) => {
                      if (!remark.trim() && body?.remark) setRemark(body.remark);
                    })
                    .catch((err) => setError(err.message));
                }}
              >
                <option value="">Select a decision</option>
                {DECISIONS.map(([value, label]) => (
                  <option key={value} value={value}>{label}</option>
                ))}
              </select>
            </label>
            <label>
              Application decision remark
              <textarea
                value={remark}
                maxLength={2000}
                rows={5}
                onChange={(event) => setRemark(event.target.value)}
                onBlur={() => loadPreview(decision, remark).catch((err) => setError(err.message))}
              />
            </label>
            {!detail.allDocumentsAddressed && (
              <label>
                Override reason
                <textarea
                  value={overrideReason}
                  maxLength={1000}
                  rows={3}
                  onChange={(event) => setOverrideReason(event.target.value)}
                  placeholder="Required only when a submitted document has not been opened or reviewed."
                />
              </label>
            )}
            <div className="admissions-actions">
              <button type="button" disabled={saving} onClick={() => saveDecision(false)}>Save Decision</button>
              <button type="button" disabled={saving || decision === "PendingCommitteeReview"} onClick={() => saveDecision(true)}>
                Save and Send Email
              </button>
              {detail.canRetryEmail && (
                <button type="button" disabled={saving} onClick={retryEmail}>Retry email</button>
              )}
              {detail.canProceedToApproval && (
                <button type="button" disabled={saving} onClick={beginOnboarding}>
                  Approve and Begin Onboarding
                </button>
              )}
              {detail.status === "Approved" && (
                <button type="button" disabled={saving} onClick={() => loadOnboarding().catch((err) => setError(err.message))}>
                  Onboarding
                </button>
              )}
            </div>
            {detail.canProceedToApproval && (
              <p>The selection decision is saved. The application stays Applied until you confirm approval and onboarding.</p>
            )}
            {onboarding && (
              <article className="activation-preview">
                <h3>Existing onboarding</h3>
                <p>This application is already approved. Opening this record does not create another student.</p>
                <p>Student number: {onboarding.studentNumber}</p>
                <p>Onboarding status: {onboarding.onboardingStatus}</p>
              </article>
            )}
            {preview && (
              <article className="email-preview">
                <p><strong>To:</strong> {preview.to}</p>
                <p><strong>Subject:</strong> {preview.subject}</p>
                <pre>{preview.body}</pre>
              </article>
            )}
            <p>WhatsApp delivery is not available in this version.</p>
          </section>
        </>
      )}

      {viewer && (
        <div className="doc-viewer" role="dialog" aria-modal="true">
          <div className="doc-viewer-bar">
            <strong>{viewer.name}</strong>
            <button type="button" onClick={() => setZoom((value) => Math.min(2.5, value + 0.25))}>Zoom in</button>
            <button type="button" onClick={() => setZoom((value) => Math.max(0.5, value - 0.25))}>Zoom out</button>
            <button type="button" onClick={() => { URL.revokeObjectURL(viewer.url); setViewer(null); }}>Close</button>
          </div>
          <div className="doc-viewer-stage">
            {image && (
              <img src={viewer.url} alt="" style={{ width: `${zoom * 100}%`, maxWidth: "none" }} />
            )}
            {pdf && (
              <iframe title={viewer.name} src={viewer.url} className="doc-viewer-pdf" />
            )}
            {!image && !pdf && <p>This file type cannot be previewed. Use Download.</p>}
          </div>
        </div>
      )}
    </DashboardLayout>
  );
}

function DocumentRow({ doc, disabled, onSave, onOpen }) {
  const [outcome, setOutcome] = useState(doc.reviewOutcome || "");
  const [remark, setRemark] = useState(doc.reviewRemark || "");

  useEffect(() => {
    setOutcome(doc.reviewOutcome || "");
    setRemark(doc.reviewRemark || "");
  }, [doc.reviewOutcome, doc.reviewRemark, doc.reviewedAt]);

  return (
    <tr>
      <td>{doc.documentType}</td>
      <td>{doc.fileName}</td>
      <td>{formatWhen(doc.uploadedAt)}</td>
      <td>
        <select value={outcome} onChange={(event) => setOutcome(event.target.value)}>
          <option value="">Not reviewed</option>
          {OUTCOMES.map(([value, label]) => <option key={value} value={value}>{label}</option>)}
        </select>
        <div>{doc.reviewOutcomeLabel}</div>
      </td>
      <td>
        <textarea value={remark} maxLength={1000} rows={3} onChange={(event) => setRemark(event.target.value)} />
      </td>
      <td>
        <div>{doc.reviewedBy || "—"}</div>
        <div>{formatWhen(doc.reviewedAt)}</div>
      </td>
      <td>
        <button type="button" onClick={() => onOpen(doc, false).catch((err) => alert(err.message))}>View</button>
        <button type="button" onClick={() => onOpen(doc, true).catch((err) => alert(err.message))}>Download</button>
        <button type="button" disabled={disabled || !outcome} onClick={() => onSave(doc, outcome, remark)}>
          Save review
        </button>
      </td>
    </tr>
  );
}
