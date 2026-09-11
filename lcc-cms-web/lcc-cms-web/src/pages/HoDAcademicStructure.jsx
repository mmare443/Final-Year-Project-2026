import { useEffect, useMemo, useState } from "react";
import DashboardLayout from "../components/DashboardLayout";
import { useAcademicStructure } from "../context/AcademicStructureContext";
import { HOD_NAV } from "./hodNav";
import { loadHodDepartment, rowId } from "./hodScope";
import "./AcademicStructure.css";
import "./StudentRecords.css";

const TABS = [
  { key: "faculty", label: "Faculty" },
  { key: "department", label: "Department" },
  { key: "programmes", label: "Programmes" },
  { key: "courses", label: "Courses" },
];

export default function HoDAcademicStructure() {
  const {
    faculties,
    departments,
    programmes,
    courses,
    isLoading,
    apiError,
    fetchAll,
  } = useAcademicStructure();
  const [departmentId, setDepartmentId] = useState(null);
  const [departmentName, setDepartmentName] = useState(null);
  const [facultyName, setFacultyName] = useState(null);
  const [scopeError, setScopeError] = useState(null);
  const [activeTab, setActiveTab] = useState("faculty");

  useEffect(() => {
    fetchAll();
  }, [fetchAll]);

  useEffect(() => {
    (async () => {
      try {
        const scope = await loadHodDepartment();
        setDepartmentId(scope.departmentId);
        setDepartmentName(scope.departmentName);
        setFacultyName(scope.facultyName);
        setScopeError(null);
      } catch {
        setScopeError(
          "Couldn't reach the backend API. Make sure `dotnet run` is running on http://localhost:5000."
        );
      }
    })();
  }, []);

  const department = useMemo(
    () =>
      departments.find(
        (d) => Number(rowId(d, "departmentId", "DepartmentId")) === Number(departmentId)
      ),
    [departments, departmentId]
  );

  const faculty = useMemo(() => {
    const facultyId = rowId(department, "facultyId", "FacultyId");
    return faculties.find(
      (f) => Number(rowId(f, "facultyId", "FacultyId")) === Number(facultyId)
    );
  }, [faculties, department]);

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

  const courseRows = useMemo(
    () =>
      courses.filter((c) =>
        programmeIds.has(Number(rowId(c, "programmeId", "ProgrammeId")))
      ),
    [courses, programmeIds]
  );

  const error = scopeError || apiError;

  const renderTable = () => {
    if (activeTab === "faculty") {
      return (
        <table className="as-table">
          <thead>
            <tr>
              <th>Faculty</th>
            </tr>
          </thead>
          <tbody>
            {!faculty && !isLoading && (
              <tr><td className="as-empty">No faculty linked to your department.</td></tr>
            )}
            {faculty && (
              <tr key={rowId(faculty, "facultyId", "FacultyId")}>
                <td>{faculty.facultyName ?? faculty.name ?? facultyName ?? "—"}</td>
              </tr>
            )}
          </tbody>
        </table>
      );
    }

    if (activeTab === "department") {
      return (
        <table className="as-table">
          <thead>
            <tr>
              <th>Department</th>
              <th>Faculty</th>
            </tr>
          </thead>
          <tbody>
            {!department && !isLoading && (
              <tr><td colSpan={2} className="as-empty">No department is assigned to your account.</td></tr>
            )}
            {department && (
              <tr key={rowId(department, "departmentId", "DepartmentId")}>
                <td>{department.departmentName ?? department.name ?? departmentName ?? "—"}</td>
                <td>{faculty?.facultyName ?? faculty?.name ?? facultyName ?? "—"}</td>
              </tr>
            )}
          </tbody>
        </table>
      );
    }

    if (activeTab === "programmes") {
      return (
        <table className="as-table">
          <thead>
            <tr>
              <th>Programme</th>
              <th>Duration</th>
            </tr>
          </thead>
          <tbody>
            {programmeRows.length === 0 && !isLoading && (
              <tr><td colSpan={2} className="as-empty">No programmes in your department.</td></tr>
            )}
            {programmeRows.map((p) => (
              <tr key={rowId(p, "programmeId", "ProgrammeId")}>
                <td>{p.programmeName ?? p.name}</td>
                <td>{p.durationYears ?? "—"} yrs</td>
              </tr>
            ))}
          </tbody>
        </table>
      );
    }

    return (
      <table className="as-table">
        <thead>
          <tr>
            <th>Code</th>
            <th>Name</th>
            <th>Programme</th>
            <th>Credits</th>
          </tr>
        </thead>
        <tbody>
          {courseRows.length === 0 && !isLoading && (
            <tr><td colSpan={4} className="as-empty">No courses in your department.</td></tr>
          )}
          {courseRows.map((c) => {
            const programme = programmes.find(
              (p) =>
                Number(rowId(p, "programmeId", "ProgrammeId"))
                === Number(rowId(c, "programmeId", "ProgrammeId"))
            );
            return (
              <tr key={rowId(c, "courseId", "CourseId")}>
                <td>{c.courseCode ?? c.code}</td>
                <td>{c.courseName ?? c.name}</td>
                <td>{programme?.programmeName ?? programme?.name ?? "—"}</td>
                <td>{c.creditValue ?? "—"}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
    );
  };

  return (
    <DashboardLayout title="Academic Structure" navItems={HOD_NAV}>
      {error && <div className="records-error">{error}</div>}
      <p style={{ color: "var(--text-light)", fontSize: 13, marginBottom: 18 }}>
        Read-only view of {departmentName || "your department"} academic structure.
      </p>

      <div className="as-tabs">
        {TABS.map((t) => (
          <button
            key={t.key}
            type="button"
            className={`as-tab${activeTab === t.key ? " as-tab-active" : ""}`}
            onClick={() => setActiveTab(t.key)}
          >
            {t.label}
          </button>
        ))}
      </div>

      {isLoading && (
        <p style={{ color: "var(--text-light)", marginBottom: 18 }}>Loading…</p>
      )}

      {renderTable()}
    </DashboardLayout>
  );
}
