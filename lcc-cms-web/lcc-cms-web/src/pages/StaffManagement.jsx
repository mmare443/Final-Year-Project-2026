import { useCallback, useEffect, useState } from "react";
import DashboardLayout from "../components/DashboardLayout";
import { useAcademicStructure } from "../context/AcademicStructureContext";
import { API_ORIGIN, apiFetch } from "../api";
import { REGISTRAR_NAV } from "./registrarNav";
import "./AcademicStructure.css";
import "./StudentRecords.css";

const emptyForm = {
  email: "",
  role: "Lecturer",
  departmentId: "",
  jobTitle: "",
  employmentDetails: "",
};

export default function StaffManagement() {
  const { departments, fetchAll } = useAcademicStructure();
  const [staff, setStaff] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [apiError, setApiError] = useState(null);
  const [formOpen, setFormOpen] = useState(false);
  const [form, setForm] = useState(emptyForm);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState(null);

  const loadStaff = useCallback(async () => {
    setIsLoading(true);
    try {
      const res = await apiFetch(`${API_ORIGIN}/api/staff`);
      if (!res.ok) throw new Error(`API returned ${res.status}`);
      setStaff(await res.json());
      setApiError(null);
    } catch {
      setApiError(
        "Couldn't reach the backend API. Make sure `dotnet run` is running on http://localhost:5000."
      );
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchAll();
    loadStaff();
  }, [fetchAll, loadStaff]);

  const handleCreate = async (e) => {
    e.preventDefault();
    setSaving(true);
    setSaveError(null);
    try {
      const res = await apiFetch(`${API_ORIGIN}/api/staff`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          email: form.email,
          role: form.role,
          departmentId: Number(form.departmentId),
          jobTitle: form.jobTitle,
          employmentDetails: form.employmentDetails || null,
        }),
      });
      if (!res.ok) {
        const message = await res.text().catch(() => null);
        throw new Error(message || `API returned ${res.status}`);
      }
      setFormOpen(false);
      setForm(emptyForm);
      await loadStaff();
    } catch (err) {
      setSaveError(err.message || "Couldn't save this staff member.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <DashboardLayout title="Staff Management — M9" navItems={REGISTRAR_NAV}>
      <p style={{ color: "var(--text-light)", fontSize: 13, marginBottom: 18 }}>
        Create faculty and staff users (staff_id = user_id). Role must be a
        staff SQL role, not Student.
      </p>

      {apiError && <div className="records-error">{apiError}</div>}

      <div className="as-toolbar">
        <button className="as-add-btn" onClick={() => { setFormOpen(true); setSaveError(null); }}>
          + Add staff
        </button>
      </div>

      {formOpen && (
        <form className="as-form" onSubmit={handleCreate}>
          {saveError && <div className="as-error">{saveError}</div>}
          <div className="as-form-fields">
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
            <label>Job title
              <input value={form.jobTitle} onChange={(e) => setForm({ ...form, jobTitle: e.target.value })} required />
            </label>
            <label>Employment details
              <input value={form.employmentDetails} onChange={(e) => setForm({ ...form, employmentDetails: e.target.value })} />
            </label>
          </div>
          <div className="as-form-actions">
            <button type="submit" className="as-save-btn" disabled={saving}>{saving ? "Saving…" : "Save"}</button>
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
              <th>Email</th>
              <th>Role</th>
              <th>Job title</th>
              <th>Department</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            {staff.length === 0 && (
              <tr>
                <td colSpan={5} style={{ color: "var(--text-light)" }}>No staff records.</td>
              </tr>
            )}
            {staff.map((row) => (
              <tr key={row.staffId}>
                <td>{row.email}</td>
                <td>{row.role}</td>
                <td>{row.jobTitle}</td>
                <td>{row.departmentName}</td>
                <td>{row.status}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </DashboardLayout>
  );
}
