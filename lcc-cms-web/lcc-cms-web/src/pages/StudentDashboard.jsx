import { useEffect, useMemo } from "react";
import DashboardLayout from "../components/DashboardLayout";
import { useStudents } from "../context/StudentsContext";
import { useAttendance } from "../context/AttendanceContext";
import { useLearning } from "../context/LearningContext";
import { STUDENT_NAV } from "./Attendance";

export default function StudentDashboard() {
  const { myProfile, fetchMyProfile } = useStudents();
  const { rates, fetchRates } = useAttendance();
  const { summary, fetchSummary } = useLearning();

  useEffect(() => {
    (async () => {
      const profile = myProfile || await fetchMyProfile();
      if (profile?.id) {
        fetchRates({ studentId: profile.id });
        fetchSummary(profile.id);
      }
    })();
  }, [myProfile, fetchMyProfile, fetchRates, fetchSummary]);

  const overall = useMemo(() => {
    if (!rates.length) return null;
    const marked = rates.reduce((sum, r) => sum + r.sessionsMarked, 0);
    const attended = rates.reduce((sum, r) => sum + r.sessionsAttended, 0);
    if (!marked) return 0;
    return Math.round((attended * 1000) / marked) / 10;
  }, [rates]);

  return (
    <DashboardLayout title="Student Dashboard" navItems={STUDENT_NAV}>
      <div className="dash-card-grid">
        <div className="dash-card">
          <h3>Enrolled Courses</h3>
          <div className="dash-card-value">{rates.length || "—"}</div>
        </div>
        <div className="dash-card">
          <h3>Attendance Rate</h3>
          <div className="dash-card-value">{overall == null ? "—" : `${overall}%`}</div>
        </div>
        <div className="dash-card">
          <h3>Pending Assignments</h3>
          <div className="dash-card-value">{summary?.pendingCount ?? "—"}</div>
        </div>
        <div className="dash-card">
          <h3>Latest Results</h3>
          <div className="dash-card-value">—</div>
        </div>
      </div>
      <p style={{ marginTop: 24, color: "var(--text-light)", fontSize: 13 }}>
        Attendance (M5) and assignments (M6) are live. Published results follow in M7.
      </p>
    </DashboardLayout>
  );
}
