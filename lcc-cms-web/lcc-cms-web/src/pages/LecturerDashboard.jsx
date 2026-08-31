import { useEffect } from "react";
import DashboardLayout from "../components/DashboardLayout";
import { useAcademicStructure } from "../context/AcademicStructureContext";
import { useAttendance } from "../context/AttendanceContext";
import { useLearning } from "../context/LearningContext";
import { LECTURER_NAV } from "./Attendance";

export default function LecturerDashboard() {
  const { courseAllocations, fetchAll } = useAcademicStructure();
  const { sessions, fetchSessions } = useAttendance();
  const { summary, fetchSummary } = useLearning();

  useEffect(() => {
    fetchAll();
    fetchSessions();
    fetchSummary();
  }, [fetchAll, fetchSessions, fetchSummary]);

  const today = new Date().toISOString().slice(0, 10);
  const todayCount = sessions.filter((s) => s.sessionDate === today).length;

  return (
    <DashboardLayout title="Lecturer Dashboard" navItems={LECTURER_NAV}>
      <div className="dash-card-grid">
        <div className="dash-card">
          <h3>Classes Teaching</h3>
          <div className="dash-card-value">{courseAllocations.length}</div>
        </div>
        <div className="dash-card">
          <h3>Sessions recorded</h3>
          <div className="dash-card-value">{sessions.length}</div>
        </div>
        <div className="dash-card">
          <h3>Pending Grading</h3>
          <div className="dash-card-value">{summary?.pendingGradingCount ?? "—"}</div>
        </div>
        <div className="dash-card">
          <h3>Today's Sessions</h3>
          <div className="dash-card-value">{todayCount}</div>
        </div>
      </div>
      <p style={{ marginTop: 24, color: "var(--text-light)", fontSize: 13 }}>
        Attendance (M5) and assignments (M6) are live. Assessment/results publication is M7.
      </p>
    </DashboardLayout>
  );
}
