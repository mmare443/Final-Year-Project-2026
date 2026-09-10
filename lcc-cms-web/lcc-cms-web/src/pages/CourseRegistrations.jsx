import { useEffect, useState } from "react";
import DashboardLayout from "../components/DashboardLayout";
import { useRegistrations } from "../context/RegistrationsContext";
import { REGISTRAR_NAV } from "./registrarNav";
import "./StudentRecords.css";

export default function CourseRegistrations() {
  const { registrations, isLoading, apiError, fetchAll, decide } = useRegistrations();
  const [actingId, setActingId] = useState(null);
  const [actionError, setActionError] = useState(null);

  useEffect(() => {
    fetchAll();
  }, [fetchAll]);

  const handleDecide = async (id, decision) => {
    const reason = decision === "reject"
      ? window.prompt("Rejection reason (required):")
      : null;
    if (decision === "reject" && !reason?.trim()) return;

    setActingId(id);
    setActionError(null);
    try {
      await decide(id, decision, reason?.trim());
    } catch (err) {
      setActionError(err.message || "Couldn't update this registration.");
    } finally {
      setActingId(null);
    }
  };

  return (
    <DashboardLayout title="Course Registration — M4" navItems={REGISTRAR_NAV}>
      <p style={{ color: "var(--text-light)", fontSize: 13, marginBottom: 18 }}>
        Approve or reject pending course registrations. Students submit from
        their own account; this queue is Registrar/Admin.
      </p>

      {apiError && <div className="records-error">{apiError}</div>}
      {actionError && <div className="records-error">{actionError}</div>}

      {isLoading ? (
        <p style={{ color: "var(--text-light)" }}>Loading registrations…</p>
      ) : (
        <table className="records-table">
          <thead>
            <tr>
              <th>Student</th>
              <th>Course</th>
              <th>Credits</th>
              <th>Status</th>
              <th>Registered</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {registrations.length === 0 && (
              <tr>
                <td colSpan={6} style={{ color: "var(--text-light)" }}>No registrations.</td>
              </tr>
            )}
            {registrations.map((row) => (
              <tr key={row.id}>
                <td>{row.studentName || row.studentId}</td>
                <td>{row.courseCode} — {row.courseName}</td>
                <td>{row.creditValue}</td>
                <td>{row.status}</td>
                <td>{row.registeredAt ? new Date(row.registeredAt).toLocaleDateString() : "—"}</td>
                <td>
                  {row.status === "Pending" && (
                    <div className="records-actions">
                      <button
                        className="btn-approve"
                        disabled={actingId === row.id}
                        onClick={() => handleDecide(row.id, "approve")}
                      >
                        Approve
                      </button>
                      <button
                        className="btn-reject"
                        disabled={actingId === row.id}
                        onClick={() => handleDecide(row.id, "reject")}
                      >
                        Reject
                      </button>
                    </div>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </DashboardLayout>
  );
}
