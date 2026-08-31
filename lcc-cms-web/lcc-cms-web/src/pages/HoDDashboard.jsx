import { useEffect } from "react";
import DashboardLayout from "../components/DashboardLayout";
import { useAttendance } from "../context/AttendanceContext";
import { HOD_NAV } from "./Attendance";

export default function HoDDashboard() {
  const { alerts, fetchAlerts } = useAttendance();

  useEffect(() => {
    fetchAlerts();
  }, [fetchAlerts]);

  return (
    <DashboardLayout title="Head of Department Dashboard" navItems={HOD_NAV}>
      <div className="dash-card-grid">
        <div className="dash-card">
          <h3>Department Staff</h3>
          <div className="dash-card-value">—</div>
        </div>
        <div className="dash-card">
          <h3>Active Programmes</h3>
          <div className="dash-card-value">—</div>
        </div>
        <div className="dash-card">
          <h3>Low-attendance alerts</h3>
          <div className="dash-card-value">{alerts.length}</div>
        </div>
        <div className="dash-card">
          <h3>Pending Approvals</h3>
          <div className="dash-card-value">—</div>
        </div>
      </div>
      <p style={{ marginTop: 24, color: "var(--text-light)", fontSize: 13 }}>
        Attendance monitoring (M5) is live — open Attendance for unit and student reports.
        Staff and programme counts follow with M9.
      </p>
    </DashboardLayout>
  );
}
