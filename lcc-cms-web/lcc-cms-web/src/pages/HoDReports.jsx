import { useEffect, useMemo, useState } from "react";
import DashboardLayout from "../components/DashboardLayout";
import { useAcademicStructure } from "../context/AcademicStructureContext";
import { useAttendance } from "../context/AttendanceContext";
import { API_ORIGIN, apiFetch } from "../api";
import { HOD_NAV } from "./hodNav";
import { loadHodDepartment, rowId } from "./hodScope";
import "./StudentRecords.css";

export default function HoDReports() {
  const { programmes, courses, fetchAll, apiError: structureError } = useAcademicStructure();
  const { alerts, fetchAlerts } = useAttendance();
  const [departmentId, setDepartmentId] = useState(null);
  const [staffCount, setStaffCount] = useState(null);
  const [registrations, setRegistrations] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [apiError, setApiError] = useState(null);

  useEffect(() => {
    fetchAll();
    fetchAlerts();
  }, [fetchAll, fetchAlerts]);

  useEffect(() => {
    (async () => {
      setIsLoading(true);
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

        const regs = regRes.ok ? await regRes.json() : [];
        setRegistrations(Array.isArray(regs) ? regs : []);
        setApiError(null);
      } catch {
        setApiError(
          "Couldn't reach the backend API. Make sure `dotnet run` is running on http://localhost:5000."
        );
      } finally {
        setIsLoading(false);
      }
    })();
  }, []);

  const programmeRows = useMemo(
    () =>
      programmes.filter(
        (p) =>
          departmentId != null
          && Number(rowId(p, "departmentId", "DepartmentId")) === Number(departmentId)
      ),
    [programmes, departmentId]
  );

  const programmeIds = useMemo(
    () => new Set(programmeRows.map((p) => Number(rowId(p, "programmeId", "ProgrammeId")))),
    [programmeRows]
  );

  const courseCount = useMemo(
    () =>
      courses.filter((c) =>
        programmeIds.has(Number(rowId(c, "programmeId", "ProgrammeId")))
      ).length,
    [courses, programmeIds]
  );

  const courseIds = useMemo(
    () =>
      new Set(
        courses
          .filter((c) => programmeIds.has(Number(rowId(c, "programmeId", "ProgrammeId"))))
          .map((c) => Number(rowId(c, "courseId", "CourseId")))
      ),
    [courses, programmeIds]
  );

  const studentCount = useMemo(() => {
    if (courseIds.size === 0) return 0;
    const unique = new Set();
    registrations.forEach((r) => {
      if (String(r.status).toLowerCase() !== "approved") return;
      if (!courseIds.has(Number(r.courseId))) return;
      const key = r.studentId ?? r.studentNumber;
      if (key) unique.add(String(key));
    });
    return unique.size;
  }, [registrations, courseIds]);

  const error = apiError || structureError;
  const dash = (value) => (isLoading || value == null ? "—" : value);

  return (
    <DashboardLayout title="Reports" navItems={HOD_NAV}>
      {error && <div className="records-error">{error}</div>}
      {isLoading && (
        <p style={{ color: "var(--text-light)", marginBottom: 18 }}>
          Loading department reports…
        </p>
      )}

      <div className="dash-card-grid">
        <div className="dash-card">
          <h3>Department Staff Count</h3>
          <div className="dash-card-value">{dash(staffCount)}</div>
        </div>
        <div className="dash-card">
          <h3>Programme Count</h3>
          <div className="dash-card-value">{dash(departmentId == null ? null : programmeRows.length)}</div>
        </div>
        <div className="dash-card">
          <h3>Course Count</h3>
          <div className="dash-card-value">{dash(departmentId == null ? null : courseCount)}</div>
        </div>
        <div className="dash-card">
          <h3>Attendance Alerts</h3>
          <div className="dash-card-value">{alerts.length}</div>
        </div>
        <div className="dash-card">
          <h3>Student Count</h3>
          <div className="dash-card-value">{dash(studentCount)}</div>
        </div>
      </div>
    </DashboardLayout>
  );
}
