import { useEffect, useState } from "react";
import DashboardLayout from "../components/DashboardLayout";
import { useStudents } from "../context/StudentsContext";
import "./StudentRecords.css";

const NAV = [
  { label: "Overview", path: "/registrar" },
  { label: "Admissions", path: "/registrar" },
  { label: "Student Records", path: "/registrar/students" },
  { label: "Course Registration" },
  { label: "Staff Management" },
  { label: "Accommodation & Welfare" },
  { label: "System Administration" },
  { label: "Profile" },
];

export default function StudentRecords() {
  const { allStudents, isLoading, apiError, fetchAllStudents, correctStudentProfile } = useStudents();
  const [editingId, setEditingId] = useState(null);
  const [editForm, setEditForm] = useState(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState(null);

  useEffect(() => {
    fetchAllStudents();
  }, [fetchAllStudents]);

  const startEdit = (student) => {
    setEditingId(student.id);
    setEditForm({
      phone: student.phone,
      emergencyContactName: student.emergencyContactName,
      emergencyContactPhone: student.emergencyContactPhone,
      postalAddress: student.postalAddress,
    });
    setSaveError(null);
  };

  const cancelEdit = () => {
    setEditingId(null);
    setEditForm(null);
  };

  const handleChange = (e) => {
    setEditForm({ ...editForm, [e.target.name]: e.target.value });
  };

  const handleSave = async (id) => {
    setSaving(true);
    setSaveError(null);
    try {
      await correctStudentProfile(id, editForm);
      setEditingId(null);
      setEditForm(null);
    } catch (err) {
      setSaveError(err.message || "Couldn't save — check the backend API is running.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <DashboardLayout title="Student Records — Registrar" navItems={NAV}>
      <p style={{ color: "var(--text-light)", fontSize: 13, marginBottom: 18 }}>
        M2 — correct a student's contact or emergency details on their
        behalf. Academic fields (programme, ID) are set at admission (M1)
        and not editable here.
      </p>

      {apiError && (
        <div className="records-error">{apiError}</div>
      )}
      {saveError && (
        <div className="records-error">{saveError}</div>
      )}

      {isLoading ? (
        <p style={{ color: "var(--text-light)" }}>Loading student records…</p>
      ) : (
        <table className="records-table">
          <thead>
            <tr>
              <th>Student</th>
              <th>ID</th>
              <th>Programme</th>
              <th>Phone</th>
              <th>Emergency Contact</th>
              <th>Postal Address</th>
              <th>Action</th>
            </tr>
          </thead>
          <tbody>
            {allStudents.map((s) => (
              <tr key={s.id}>
                <td>{s.fullName}</td>
                <td>{s.id}</td>
                <td>{s.programme}</td>
                {editingId === s.id ? (
                  <>
                    <td>
                      <input
                        type="tel" name="phone" value={editForm.phone}
                        onChange={handleChange} className="records-input"
                      />
                    </td>
                    <td className="records-emergency-cell">
                      <input
                        type="text" name="emergencyContactName" value={editForm.emergencyContactName}
                        onChange={handleChange} className="records-input" placeholder="Name"
                      />
                      <input
                        type="tel" name="emergencyContactPhone" value={editForm.emergencyContactPhone}
                        onChange={handleChange} className="records-input" placeholder="Phone"
                      />
                    </td>
                    <td>
                      <input
                        type="text" name="postalAddress" value={editForm.postalAddress}
                        onChange={handleChange} className="records-input"
                      />
                    </td>
                    <td className="records-actions">
                      <button
                        className="btn-approve" disabled={saving}
                        onClick={() => handleSave(s.id)}
                      >
                        {saving ? "…" : "Save"}
                      </button>
                      <button className="btn-reject" onClick={cancelEdit} disabled={saving}>
                        Cancel
                      </button>
                    </td>
                  </>
                ) : (
                  <>
                    <td>{s.phone}</td>
                    <td>{s.emergencyContactName || "—"}{s.emergencyContactPhone ? ` (${s.emergencyContactPhone})` : ""}</td>
                    <td>{s.postalAddress || "—"}</td>
                    <td>
                      <button className="records-edit-btn" onClick={() => startEdit(s)}>
                        Correct
                      </button>
                    </td>
                  </>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </DashboardLayout>
  );
}
