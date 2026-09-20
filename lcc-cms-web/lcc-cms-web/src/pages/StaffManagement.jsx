import { useCallback, useEffect, useState } from "react";
import DashboardLayout from "../components/DashboardLayout";
import { useAcademicStructure } from "../context/AcademicStructureContext";
import { useMockAuth } from "../context/MockAuthContext";
import { API_ORIGIN, API_UNREACHABLE, apiFetch, isNetworkFailure, readApiError } from "../api";
import { REGISTRAR_NAV } from "./registrarNav";
import {
  buildRecordPrintHtml,
  fetchUserPhotoDataUrl,
  printedAtLabel,
  printHtmlDocument,
} from "../print/printRecord";
import "./AcademicStructure.css";
import "./StudentRecords.css";
import "./StaffManagement.css";

const emptyForm = {
  fullName: "",
  email: "",
  role: "Lecturer",
  departmentId: "",
  jobTitle: "",
  employmentDetails: "",
  status: "Active",
};

function toForm(row) {
  return {
    fullName: row.fullName || "",
    email: row.email || "",
    role: row.role || "Lecturer",
    departmentId: String(row.departmentId || ""),
    jobTitle: row.jobTitle || "",
    employmentDetails: row.employmentDetails || "",
    status: row.status || "Active",
  };
}

function staffPayload(form) {
  return {
    fullName: form.fullName,
    email: form.email,
    role: form.role,
    departmentId: Number(form.departmentId),
    jobTitle: form.jobTitle,
    employmentDetails: form.employmentDetails || null,
    status: form.status,
  };
}

async function printStaffProfile(row, printedBy) {
  const photoDataUrl = await fetchUserPhotoDataUrl(row.staffId ?? row.userId);
  const html = buildRecordPrintHtml({
    documentTitle: "Staff Profile Report",
    photoDataUrl,
    printedBy,
    printedAt: printedAtLabel(),
    fields: [
      ["Staff ID", row.staffNumber],
      ["Full Name", row.fullName],
      ["Email", row.email],
      ["Role", row.role],
      ["Job Title", row.jobTitle],
      ["Department", row.departmentName],
      ["Status", row.status],
    ],
    extraTitle: "Additional Staff Information",
    extraText: row.employmentDetails,
  });
  printHtmlDocument(html);
}

export default function StaffManagement() {
  const { displayName } = useMockAuth();
  const { departments, fetchAll } = useAcademicStructure();
  const [staff, setStaff] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [apiError, setApiError] = useState(null);
  const [formOpen, setFormOpen] = useState(false);
  const [form, setForm] = useState(emptyForm);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState(null);
  const [editingId, setEditingId] = useState(null);
  const [viewRow, setViewRow] = useState(null);

  const loadStaff = useCallback(async () => {
    setIsLoading(true);
    try {
      const res = await apiFetch(`${API_ORIGIN}/api/staff`);
      if (!res.ok) throw new Error(await readApiError(res));
      setStaff(await res.json());
      setApiError(null);
    } catch (err) {
      setApiError(isNetworkFailure(err) ? API_UNREACHABLE : (err.message || API_UNREACHABLE));
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchAll();
    loadStaff();
  }, [fetchAll, loadStaff]);

  const openCreate = () => {
    setEditingId(null);
    setForm(emptyForm);
    setSaveError(null);
    setFormOpen(true);
  };

  const openEdit = (row) => {
    setEditingId(row.staffId);
    setForm(toForm(row));
    setSaveError(null);
    setFormOpen(true);
    setViewRow(null);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSaving(true);
    setSaveError(null);
    try {
      const res = await apiFetch(
        editingId == null ? `${API_ORIGIN}/api/staff` : `${API_ORIGIN}/api/staff/${editingId}`,
        {
          method: editingId == null ? "POST" : "PUT",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(staffPayload(form)),
        }
      );
      if (!res.ok) throw new Error(await readApiError(res));
      setFormOpen(false);
      setForm(emptyForm);
      setEditingId(null);
      await loadStaff();
    } catch (err) {
      setSaveError(err.message || "Couldn't save this staff member.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <DashboardLayout title="Staff Management" navItems={REGISTRAR_NAV}>
      <p style={{ color: "var(--text-light)", fontSize: 13, marginBottom: 18 }}>
        Create faculty and staff users. Staff IDs are generated as STF-YYYY-NNN.
        Internal user id remains staff_id = user_id.
      </p>

      {apiError && <div className="records-error">{apiError}</div>}

      <div className="as-toolbar">
        <button className="as-add-btn" onClick={openCreate}>+ Add staff</button>
      </div>

      {formOpen && (
        <form className="as-form" onSubmit={handleSubmit}>
          {saveError && <div className="as-error">{saveError}</div>}
          <h3 className="staff-form-title">{editingId == null ? "Add Staff" : "Edit Staff"}</h3>
          <div className="as-form-fields">
            <label>Full Name
              <input
                value={form.fullName}
                onChange={(e) => setForm({ ...form, fullName: e.target.value })}
                required
                maxLength={150}
              />
            </label>
            <label>Email
              <input value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} required />
            </label>
            <label>Role
              <select value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value })}>
                <option value="Lecturer">Lecturer</option>
                <option value="HoD">HoD</option>
                <option value="RegistrarAdmin">Registrar/Admin</option>
                <option value="ManagementPrincipal">Management/Principal</option>
              </select>
            </label>
            <label>Job Title
              <input value={form.jobTitle} onChange={(e) => setForm({ ...form, jobTitle: e.target.value })} required />
            </label>
            <label>Department
              <select
                value={form.departmentId}
                onChange={(e) => setForm({ ...form, departmentId: e.target.value })}
                required
              >
                <option value="">Select…</option>
                {departments.map((d) => (
                  <option key={d.departmentId ?? d.id} value={d.departmentId ?? d.id}>
                    {d.departmentName ?? d.name}
                  </option>
                ))}
              </select>
            </label>
            {editingId != null && (
              <label>Status
                <select value={form.status} onChange={(e) => setForm({ ...form, status: e.target.value })}>
                  <option value="Active">Active</option>
                  <option value="Inactive">Inactive</option>
                </select>
              </label>
            )}
            <label>Additional Staff Information
              <input
                value={form.employmentDetails}
                onChange={(e) => setForm({ ...form, employmentDetails: e.target.value })}
                maxLength={500}
              />
            </label>
          </div>
          <div className="as-form-actions">
            <button type="submit" className="as-save-btn" disabled={saving}>
              {saving ? "Saving…" : (editingId == null ? "Save" : "Save changes")}
            </button>
            <button type="button" className="as-cancel-btn" onClick={() => setFormOpen(false)}>Cancel</button>
          </div>
        </form>
      )}

      {isLoading ? (
        <p style={{ color: "var(--text-light)" }}>Loading staff…</p>
      ) : (
        <table className="records-table">
          <thead>
            <tr>
              <th>Staff ID</th>
              <th>Full Name</th>
              <th>Email</th>
              <th>Role</th>
              <th>Job Title</th>
              <th>Department</th>
              <th>Status</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {staff.length === 0 && (
              <tr>
                <td colSpan={8} style={{ color: "var(--text-light)" }}>No staff records.</td>
              </tr>
            )}
            {staff.map((row) => (
              <tr key={row.staffId}>
                <td>{row.staffNumber || "—"}</td>
                <td>{row.fullName || "—"}</td>
                <td>{row.email}</td>
                <td>{row.role}</td>
                <td>{row.jobTitle}</td>
                <td>{row.departmentName}</td>
                <td>{row.status}</td>
                <td>
                  <div className="records-actions">
                    <button type="button" className="records-edit-btn" onClick={() => setViewRow(row)}>View</button>
                    <button type="button" className="records-edit-btn" onClick={() => openEdit(row)}>Edit</button>
                    <button type="button" className="records-edit-btn" onClick={() => printStaffProfile(row, displayName)}>Print</button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {viewRow && (
        <div className="staff-modal-backdrop" onClick={() => setViewRow(null)}>
          <div className="staff-modal" onClick={(e) => e.stopPropagation()} role="dialog" aria-labelledby="staff-detail-title">
            <h3 id="staff-detail-title">Staff details</h3>
            <dl className="staff-detail-list">
              <dt>Staff ID</dt><dd>{viewRow.staffNumber || "—"}</dd>
              <dt>Full Name</dt><dd>{viewRow.fullName || "—"}</dd>
              <dt>Email</dt><dd>{viewRow.email}</dd>
              <dt>Role</dt><dd>{viewRow.role}</dd>
              <dt>Job Title</dt><dd>{viewRow.jobTitle}</dd>
              <dt>Department</dt><dd>{viewRow.departmentName}</dd>
              <dt>Faculty</dt><dd>{viewRow.facultyName || "—"}</dd>
              <dt>Status</dt><dd>{viewRow.status}</dd>
              <dt>Additional Staff Information</dt>
              <dd>{viewRow.employmentDetails || "—"}</dd>
            </dl>
            <div className="as-form-actions">
              <button type="button" className="as-save-btn" onClick={() => openEdit(viewRow)}>Edit / Save</button>
              <button type="button" className="as-cancel-btn" onClick={() => printStaffProfile(viewRow, displayName)}>Print</button>
              <button type="button" className="as-cancel-btn" onClick={() => setViewRow(null)}>Close</button>
            </div>
          </div>
        </div>
      )}
    </DashboardLayout>
  );
}
