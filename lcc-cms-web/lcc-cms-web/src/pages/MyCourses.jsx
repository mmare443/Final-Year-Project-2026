import { useEffect } from "react";
import DashboardLayout from "../components/DashboardLayout";
import { useStudents } from "../context/StudentsContext";
import { useRegistrations } from "../context/RegistrationsContext";
import { STUDENT_NAV } from "./Attendance";
import "./StudentRecords.css";

export default function MyCourses() {
  const { myProfile, fetchMyProfile } = useStudents();
  const { registrations, isLoading, apiError, fetchAll } = useRegistrations();

  useEffect(() => {
    (async () => {
      const profile = myProfile || await fetchMyProfile();
      if (profile?.id) {
        await fetchAll(profile.id);
      }
    })();
  }, [myProfile, fetchMyProfile, fetchAll]);

  return (
    <DashboardLayout title="My Courses" navItems={STUDENT_NAV}>
      <p style={{ color: "var(--text-light)", fontSize: 13, marginBottom: 18 }}>
        Courses you have registered for, including pending approval.
      </p>

      {apiError && <div className="records-error">{apiError}</div>}

      {isLoading ? (
        <p style={{ color: "var(--text-light)" }}>Loading your courses…</p>
      ) : (
        <table className="records-table">
          <thead>
            <tr>
              <th>Course code</th>
              <th>Course name</th>
              <th>Credits</th>
              <th>Registration status</th>
            </tr>
          </thead>
          <tbody>
            {registrations.length === 0 && (
              <tr>
                <td colSpan={4} style={{ color: "var(--text-light)" }}>
                  You have no course registrations yet.
                </td>
              </tr>
            )}
            {registrations.map((row) => (
              <tr key={row.id}>
                <td>{row.courseCode}</td>
                <td>{row.courseName}</td>
                <td>{row.creditValue}</td>
                <td>{row.status}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </DashboardLayout>
  );
}
