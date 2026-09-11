import { useEffect, useState } from "react";
import DashboardLayout from "../components/DashboardLayout";
import { API_ORIGIN, apiFetch } from "../api";
import { MANAGEMENT_NAV } from "./managementNav";
import "./StudentRecords.css";

const KPI = [
  { key: "totalStaff", label: "Total Staff" },
  { key: "lecturers", label: "Lecturers" },
  { key: "hoDs", label: "HoDs" },
  { key: "registrarAdmins", label: "Registrar/Admin" },
  { key: "managementPrincipals", label: "Management" },
];

export default function StaffOverview() {
  const [report, setReport] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [apiError, setApiError] = useState(null);

  useEffect(() => {
    (async () => {
      setIsLoading(true);
      try {
        const res = await apiFetch(`${API_ORIGIN}/api/reports/staff`);
        if (!res.ok) throw new Error(`API returned ${res.status}`);
        setReport(await res.json());
        setApiError(null);
      } catch {
        setReport(null);
        setApiError(
          "Couldn't reach the backend API. Make sure `dotnet run` is running on http://localhost:5000."
        );
      } finally {
        setIsLoading(false);
      }
    })();
  }, []);

  const rows = report?.departments ?? [];

  return (
    <DashboardLayout title="Staff Overview" navItems={MANAGEMENT_NAV}>
      {apiError && <div className="records-error">{apiError}</div>}
      {isLoading && (
        <p style={{ color: "var(--text-light)", marginBottom: 18 }}>
          Loading staff overview…
        </p>
      )}

      <div className="dash-card-grid">
        {KPI.map((item) => (
          <div className="dash-card" key={item.key}>
            <h3>{item.label}</h3>
            <div className="dash-card-value">
              {report == null ? "—" : report[item.key]}
            </div>
          </div>
        ))}
      </div>

      <p style={{ marginTop: 24, marginBottom: 14, color: "var(--text-light)", fontSize: 13 }}>
        Staff headcount by role from staff and user records; allocated courses
        are course allocation rows for staff in each department.
      </p>

      <table className="records-table">
        <thead>
          <tr>
            <th>Faculty</th>
            <th>Department</th>
            <th>Staff Count</th>
            <th>Allocated Courses</th>
          </tr>
        </thead>
        <tbody>
          {rows.length === 0 && !isLoading && (
            <tr>
              <td colSpan={4} style={{ color: "var(--text-light)" }}>
                No departments published yet.
              </td>
            </tr>
          )}
          {rows.map((row) => (
            <tr key={`${row.facultyName}-${row.departmentName}`}>
              <td>{row.facultyName}</td>
              <td>{row.departmentName}</td>
              <td>{row.staffCount}</td>
              <td>{row.allocatedCourses}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </DashboardLayout>
  );
}
