import { useState } from "react";
import { useMockData } from "../context/MockDataContext";
import { PUBLIC_SITE_URL } from "../config";
import "./Apply.css";

// Matches Section 1 of LCCB's actual paper Application Form for 2027
// Enrolment — replaces the earlier placeholder Bachelor's-degree list,
// which didn't match what LCCB actually offers (Diploma/Certificate
// programmes in three fields, not degrees).
const PROGRAMMES = [
  "Diploma in Applied Ministry",
  "Diploma in Tropical Agriculture",
  "Diploma in Business Administration and Management",
  "Certificate in Applied Ministry",
  "Certificate in Tropical Agriculture",
  "Certificate in Business Administration and Management",
];

const EMPTY_FORM = {
  fullName: "",
  email: "",
  phone: "",
  programme: PROGRAMMES[0],
};

const MAX_FILE_SIZE = 5 * 1024 * 1024; // 5 MB — must match the backend's limit
const DOC_ALLOWED_TYPES = ["application/pdf", "image/jpeg", "image/png"];
const PHOTO_ALLOWED_TYPES = ["image/jpeg", "image/png"];

// Matches Section 8 (submission checklist) of the paper form. `key` must
// match the corresponding IFormFile property name on the backend's
// AdmissionApplicationRequest exactly (case-insensitive), since that's
// how ASP.NET Core's model binder connects a FormData field to it.
const DOCUMENT_FIELDS = [
  { key: "letterOfInterest", label: "Letter of Interest", required: true, accept: DOC_ALLOWED_TYPES },
  { key: "passportPhoto", label: "Passport-size Photo", required: true, accept: PHOTO_ALLOWED_TYPES },
  { key: "feeDepositSlip", label: "Application Fee Deposit Slip (K30)", required: true, accept: DOC_ALLOWED_TYPES },
  { key: "grade10Certificate", label: "Grade 10 Certificate", required: true, accept: DOC_ALLOWED_TYPES },
  { key: "grade12Certificate", label: "Grade 12 Certificate", required: true, accept: DOC_ALLOWED_TYPES },
  { key: "referenceLetter1", label: "Reference Letter — Church Pastor", required: true, accept: DOC_ALLOWED_TYPES },
  { key: "referenceLetter2", label: "Reference Letter — Community Leader", required: true, accept: DOC_ALLOWED_TYPES },
  { key: "workReference", label: "Work Reference (if applicable)", required: false, accept: DOC_ALLOWED_TYPES },
];

export default function Apply() {
  const { addApplication } = useMockData();
  const [form, setForm] = useState(EMPTY_FORM);
  const [documents, setDocuments] = useState({});
  const [fileErrors, setFileErrors] = useState({});
  const [submitted, setSubmitted] = useState(null);
  const [error, setError] = useState(null);
  const [submitting, setSubmitting] = useState(false);

  const handleChange = (e) => {
    setForm({ ...form, [e.target.name]: e.target.value });
  };

  const handleFileChange = (fieldKey, accept) => (e) => {
    const file = e.target.files[0];
    setFileErrors((prev) => ({ ...prev, [fieldKey]: null }));
    setDocuments((prev) => ({ ...prev, [fieldKey]: null }));

    if (!file) return;

    // Client-side validation is a UX nicety, not a security boundary — the
    // backend re-checks type, size, AND which documents are required
    // itself, and is the real gate (see AdmissionsController.cs).
    if (!accept.includes(file.type)) {
      setFileErrors((prev) => ({ ...prev, [fieldKey]: "Wrong file type." }));
      e.target.value = "";
      return;
    }
    if (file.size > MAX_FILE_SIZE) {
      setFileErrors((prev) => ({ ...prev, [fieldKey]: "Too large — 5 MB max." }));
      e.target.value = "";
      return;
    }

    setDocuments((prev) => ({ ...prev, [fieldKey]: file }));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      const record = await addApplication(form, documents);
      setSubmitted(record);
      setForm(EMPTY_FORM);
      setDocuments({});
    } catch (err) {
      setError(err.message || "Couldn't submit — check that the backend API is running.");
    } finally {
      setSubmitting(false);
    }
  };

  if (submitted) {
    return (
      <div className="apply-page">
        <div className="apply-card">
          <h1>Application Submitted</h1>
          <p className="apply-intro">
            Thank you, {submitted.fullName.split(" ")[0]}. Your application
            for <strong>{submitted.programme}</strong> has been received and
            is now <strong>{submitted.status}</strong>, pending Registrar
            review.
          </p>
          {submitted.documents && submitted.documents.length > 0 && (
            <div className="apply-note">
              <p>Documents received:</p>
              <ul className="apply-doc-list">
                {submitted.documents.map((d) => (
                  <li key={d.path}>{d.type} — {d.fileName}</li>
                ))}
              </ul>
            </div>
          )}
          <p className="apply-note">
            You'll receive a welcome email with login credentials at{" "}
            {submitted.email} if your application is approved.
          </p>
          <a href={PUBLIC_SITE_URL} className="back-link">← Return to LCC website</a>
        </div>
      </div>
    );
  }

  return (
    <div className="apply-page">
      <div className="apply-card">
        <div className="apply-eyebrow">LCC-CMS · Admissions</div>
        <h1>Apply to Lutheran Church College, Banz</h1>
        <p className="apply-intro">
          Complete the form and attach all required documents. Per LCCB's
          admissions policy, applications without required documents will
          not be accepted.
        </p>

        {error && <div className="apply-error">{error}</div>}

        <form onSubmit={handleSubmit} className="apply-form">
          <label>
            Full Name
            <input
              type="text" name="fullName" required
              value={form.fullName} onChange={handleChange}
            />
          </label>

          <label>
            Email Address
            <input
              type="email" name="email" required
              value={form.email} onChange={handleChange}
            />
          </label>

          <label>
            Phone Number
            <input
              type="tel" name="phone" required
              value={form.phone} onChange={handleChange}
            />
          </label>

          <label>
            Programme of Study
            <select name="programme" value={form.programme} onChange={handleChange}>
              {PROGRAMMES.map((p) => (
                <option key={p} value={p}>{p}</option>
              ))}
            </select>
          </label>

          <div className="apply-doc-section">
            <div className="apply-doc-heading">Required Documents</div>
            {DOCUMENT_FIELDS.map(({ key, label, required, accept }) => (
              <label key={key}>
                {label}{required && <span className="required-star"> *</span>}
                <input
                  type="file"
                  accept={accept.includes("image/png") && !accept.includes("application/pdf")
                    ? ".jpg,.jpeg,.png" : ".pdf,.jpg,.jpeg,.png"}
                  onChange={handleFileChange(key, accept)}
                />
                {fileErrors[key] ? (
                  <span className="field-note field-error">{fileErrors[key]}</span>
                ) : documents[key] ? (
                  <span className="field-note field-ok">
                    {documents[key].name} ({(documents[key].size / 1024).toFixed(0)} KB)
                  </span>
                ) : (
                  <span className="field-note">
                    {required ? "Required" : "Optional"} —{" "}
                    {accept.includes("application/pdf") ? "PDF, JPG, or PNG" : "JPG or PNG"}, 5 MB max.
                  </span>
                )}
              </label>
            ))}
          </div>

          <button type="submit" className="apply-submit" disabled={submitting}>
            {submitting ? "Submitting…" : "Submit Application"}
          </button>
        </form>

        <a href={PUBLIC_SITE_URL} className="back-link">← Return to LCC website</a>
      </div>
    </div>
  );
}
