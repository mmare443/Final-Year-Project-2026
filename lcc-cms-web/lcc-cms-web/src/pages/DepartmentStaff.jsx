import { useEffect, useMemo, useState } from "react";
import DashboardLayout from "../components/DashboardLayout";
import { API_ORIGIN, apiFetch } from "../api";
import { HOD_NAV } from "./hodNav";
import { loadHodDepartment } from "./hodScope";
import "./StudentRecords.css";

function staffName(member) {
  if (member.fullName && String(member.fullName).trim()) return member.fullName.trim();
  const local = String(member.email || "").split("@")[0].replace(/[._-]+/g, " ").trim();
  if (!local) return "—";
  return local.replace(/\b\w/g, (ch) => ch.toUpperCase());
}

export default function DepartmentStaff() {
  const [staff, setStaff] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [apiError, setApiError] = useState(null);

  useEffect(() => {
    (async () => {
      setIsLoading(true);
      try {
        const scope = await loadHodDepartment();
        const res = await apiFetch(`${API_ORIGIN}/api/staff`);
        if (!res.ok) throw new Error(`API returned ${res.status}`);
        const allStaff = await res.json();
        const mine = (Array.isArray(allStaff) ? allStaff : []).filter(
          (row) =>
            scope.departmentId != null
            && Number(row.departmentId) === Number(scope.departmentId)
        );
        setStaff(mine);
        setApiError(null);
      } catch {
        setStaff([]);
        setApiError(
          "Couldn't reach the backend API. Make sure `dotnet run` is running on http://localhost:5000."
        );
      } finally {
        setIsLoading(false);
      }
    })();
  }, []);

  const rows = useMemo(
    () =>
      staff.map((member) => ({
        staffId: member.staffId,
        name: staffName(member),
        jobTitle: member.jobTitle || "—",
        email: member.email || "—",
        departmentName: member.departmentName || "—",
      })),
    [staff]
  );

  return (
    <DashboardLayout title="Department Staff" navItems={HOD_NAV}>
      {apiError && <div className="records-error">{apiError}</div>}
      {isLoading && (
        <p style={{ color: "var(--text-light)", marginBottom: 18 }}>
          Loading department staff…
        </p>
      )}

      <table className="records-table">
        <thead>
          <tr>
            <th>Staff Name</th>
            <th>Job Title</th>
            <th>Email</th>
            <th>Department</th>
          </tr>
        </thead>
        <tbody>
          {rows.length === 0 && !isLoading && (
            <tr>
              <td colSpan={4} style={{ color: "var(--text-light)" }}>
                No staff are assigned to your department yet.
              </td>
            </tr>
          )}
          {rows.map((row) => (
            <tr key={row.staffId}>
              <td>{row.name}</td>
              <td>{row.jobTitle}</td>
              <td>{row.email}</td>
              <td>{row.departmentName}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </DashboardLayout>
  );
}
