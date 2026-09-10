import { useEffect, useState } from "react";
import DashboardLayout from "../components/DashboardLayout";
import { API_ORIGIN, apiFetch } from "../api";
import { MANAGEMENT_NAV } from "./managementNav";
import "./StudentRecords.css";

const KPI = [
  { key: "applicationsReceived", label: "Applications received" },
  { key: "approvedAdmissions", label: "Approved admissions" },
  { key: "rejectedAdmissions", label: "Rejected admissions" },
  { key: "enrolledStudents", label: "Enrolled students" },
];

export default function EnrolmentAnalytics() {
  const [report, setReport] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [apiError, setApiError] = useState(null);

  useEffect(() => {
    (async () => {
      setIsLoading(true);
      try {
        const res = await apiFetch(`${API_ORIGIN}/api/reports/enrolment`);
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

  const rows = report?.programmes ?? [];

  return (
    <DashboardLayout title="Enrolment Analytics" navItems={MANAGEMENT_NAV}>
      {apiError && <div className="records-error">{apiError}</div>}
      {isLoading && (
        <p style={{ color: "var(--text-light)", marginBottom: 18 }}>
          Loading enrolment analytics…
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
        Applications and decisions from admissions; enrolled students from
        current student records, by programme.
      </p>

      <table className="records-table">
        <thead>
          <tr>
            <th>Faculty</th>
            <th>Department</th>
            <th>Programme</th>
            <th>Applications</th>
            <th>Approved</th>
            <th>Rejected</th>
            <th>Enrolled</th>
          </tr>
        </thead>
        <tbody>
          {rows.length === 0 && !isLoading && (
            <tr>
              <td colSpan={7} style={{ color: "var(--text-light)" }}>
                No programmes published yet.
              </td>
            </tr>
          )}
          {rows.map((row) => (
            <tr key={row.programmeId}>
              <td>{row.facultyName}</td>
              <td>{row.departmentName}</td>
              <td>{row.programmeName}</td>
              <td>{row.applicationsReceived}</td>
              <td>{row.approvedAdmissions}</td>
              <td>{row.rejectedAdmissions}</td>
              <td>{row.enrolledStudents}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </DashboardLayout>
  );
}
