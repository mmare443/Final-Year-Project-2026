import { useEffect, useMemo, useState } from "react";
import DashboardLayout from "../components/DashboardLayout";
import { useAcademicStructure } from "../context/AcademicStructureContext";
import { API_ORIGIN, apiFetch } from "../api";
import { LECTURER_NAV } from "./Attendance";
import "./StudentRecords.css";

function rowId(row, camel, pascal) {
  return row?.[camel] ?? row?.[pascal] ?? row?.id ?? null;
}

export default function LecturerClasses() {
  const {
    courseAllocations,
    courses,
    programmes,
    semesters,
    isLoading,
    apiError,
    fetchAll,
  } = useAcademicStructure();
  const [staffId, setStaffId] = useState(null);
  const [meError, setMeError] = useState(null);
  const [registrations, setRegistrations] = useState([]);

  useEffect(() => {
    fetchAll();
  }, [fetchAll]);

  useEffect(() => {
    (async () => {
      try {
        const meRes = await apiFetch(`${API_ORIGIN}/api/me`);
        if (!meRes.ok) throw new Error(`API returned ${meRes.status}`);
        const me = await meRes.json();
        setStaffId(me.staffId ?? null);
        setMeError(null);
      } catch {
        setStaffId(null);
        setMeError(
          "Couldn't reach the backend API. Make sure `dotnet run` is running on http://localhost:5000."
        );
      }

      try {
        const regRes = await apiFetch(`${API_ORIGIN}/api/registrations`);
        if (regRes.ok) {
          const rows = await regRes.json();
          setRegistrations(Array.isArray(rows) ? rows : []);
        }
      } catch {
        setRegistrations([]);
      }
    })();
  }, []);

  const classes = useMemo(() => {
    const mine = courseAllocations.filter(
      (a) => staffId != null && Number(rowId(a, "staffId", "StaffId")) === Number(staffId)
    );

    return mine.map((allocation) => {
      const courseId = rowId(allocation, "courseId", "CourseId");
      const semesterId = rowId(allocation, "semesterId", "SemesterId");
      const course = courses.find(
        (c) => Number(rowId(c, "courseId", "CourseId")) === Number(courseId)
      );
      const programmeId = rowId(course, "programmeId", "ProgrammeId");
      const programme = programmes.find(
        (p) => Number(rowId(p, "programmeId", "ProgrammeId")) === Number(programmeId)
      );
      const semester = semesters.find(
        (s) => Number(rowId(s, "semesterId", "SemesterId")) === Number(semesterId)
      );
      const studentCount = registrations.filter(
        (r) =>
          String(r.status).toLowerCase() === "approved"
          && Number(r.courseId) === Number(courseId)
          && Number(r.semesterId) === Number(semesterId)
      ).length;

      return {
        allocationId: rowId(allocation, "allocationId", "AllocationId"),
        courseCode: course?.courseCode ?? course?.code ?? "—",
        courseName: course?.courseName ?? course?.name ?? "—",
        programmeName: programme?.programmeName ?? programme?.name ?? "—",
        semesterName: semester?.semesterName ?? semester?.name ?? "—",
        studentCount,
      };
    });
  }, [courseAllocations, courses, programmes, semesters, registrations, staffId]);

  const error = meError || apiError;

  return (
    <DashboardLayout title="My Classes" navItems={LECTURER_NAV}>
      {error && <div className="records-error">{error}</div>}

      <div className="dash-card-grid" style={{ marginBottom: 24 }}>
        <div className="dash-card">
          <h3>Classes teaching</h3>
          <div className="dash-card-value">
            {isLoading && staffId == null ? "—" : classes.length}
          </div>
        </div>
      </div>

      {isLoading && (
        <p style={{ color: "var(--text-light)", marginBottom: 18 }}>
          Loading your classes…
        </p>
      )}

      <table className="records-table">
        <thead>
          <tr>
            <th>Course Code</th>
            <th>Course Name</th>
            <th>Programme</th>
            <th>Semester</th>
            <th>Student Count</th>
          </tr>
        </thead>
        <tbody>
          {classes.length === 0 && !isLoading && (
            <tr>
              <td colSpan={5} style={{ color: "var(--text-light)" }}>
                No course allocations are assigned to you yet.
              </td>
            </tr>
          )}
          {classes.map((row) => (
            <tr key={row.allocationId}>
              <td>{row.courseCode}</td>
              <td>{row.courseName}</td>
              <td>{row.programmeName}</td>
              <td>{row.semesterName}</td>
              <td>{row.studentCount}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </DashboardLayout>
  );
}
