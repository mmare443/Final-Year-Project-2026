import { useEffect, useMemo, useState } from "react";
import DashboardLayout from "../components/DashboardLayout";
import { useAcademicStructure } from "../context/AcademicStructureContext";
import { useAttendance } from "../context/AttendanceContext";
import { API_ORIGIN, apiFetch } from "../api";
import { HOD_NAV } from "./hodNav";
import { loadHodDepartment, rowId } from "./hodScope";
import "./StudentRecords.css";

export default function HoDDashboard() {
  const { programmes, fetchAll } = useAcademicStructure();
  const { alerts, fetchAlerts } = useAttendance();
  const [staffCount, setStaffCount] = useState(null);
  const [pendingCount, setPendingCount] = useState(null);
  const [departmentId, setDepartmentId] = useState(null);
  const [apiError, setApiError] = useState(null);

  useEffect(() => {
    fetchAll();
    fetchAlerts();
  }, [fetchAll, fetchAlerts]);

  useEffect(() => {
    (async () => {
      try {
        const scope = await loadHodDepartment();
        setDepartmentId(scope.departmentId);

        const [staffRes, regRes] = await Promise.all([
          apiFetch(`${API_ORIGIN}/api/staff`),
          apiFetch(`${API_ORIGIN}/api/registrations`),
        ]);
        if (!staffRes.ok) throw new Error(`API returned ${staffRes.status}`);

        const allStaff = await staffRes.json();
        setStaffCount(
          (Array.isArray(allStaff) ? allStaff : []).filter(
            (row) =>
              scope.departmentId != null
              && Number(row.departmentId) === Number(scope.departmentId)
          ).length
        );

        if (regRes.ok) {
          const regs = await regRes.json();
          setPendingCount(
            (Array.isArray(regs) ? regs : []).filter(
              (r) => String(r.status).toLowerCase() === "pending"
            ).length
          );
        } else {
          setPendingCount(0);
        }

        setApiError(null);
      } catch {
        setApiError(
          "Couldn't reach the backend API. Make sure `dotnet run` is running on http://localhost:5000."
        );
      }
    })();
  }, []);

  const programmeCount = useMemo(
    () =>
      programmes.filter(
        (p) =>
          departmentId != null
          && Number(rowId(p, "departmentId", "DepartmentId")) === Number(departmentId)
      ).length,
    [programmes, departmentId]
  );

  return (
    <DashboardLayout title="Dashboard" navItems={HOD_NAV}>
      {apiError && <div className="records-error">{apiError}</div>}
      <div className="dash-card-grid">
        <div className="dash-card">
          <h3>Department Staff</h3>
          <div className="dash-card-value">{staffCount == null ? "—" : staffCount}</div>
        </div>
        <div className="dash-card">
          <h3>Active Programmes</h3>
          <div className="dash-card-value">{departmentId == null ? "—" : programmeCount}</div>
        </div>
        <div className="dash-card">
          <h3>Low-attendance alerts</h3>
          <div className="dash-card-value">{alerts.length}</div>
        </div>
        <div className="dash-card">
          <h3>Pending Approvals</h3>
          <div className="dash-card-value">{pendingCount == null ? "—" : pendingCount}</div>
        </div>
      </div>
    </DashboardLayout>
  );
}
