import { useEffect, useMemo, useState } from "react";
import DashboardLayout from "../components/DashboardLayout";
import { useAcademicStructure } from "../context/AcademicStructureContext";
import { HOD_NAV } from "./hodNav";
import { loadHodDepartment, rowId } from "./hodScope";
import "./StudentRecords.css";

export default function DepartmentProgrammes() {
  const { programmes, departments, faculties, isLoading, apiError, fetchAll } =
    useAcademicStructure();
  const [departmentId, setDepartmentId] = useState(null);
  const [scopeError, setScopeError] = useState(null);

  useEffect(() => {
    fetchAll();
  }, [fetchAll]);

  useEffect(() => {
    (async () => {
      try {
        const scope = await loadHodDepartment();
        setDepartmentId(scope.departmentId);
        setScopeError(null);
      } catch {
        setScopeError(
          "Couldn't reach the backend API. Make sure `dotnet run` is running on http://localhost:5000."
        );
      }
    })();
  }, []);

  const rows = useMemo(() => {
    return programmes
      .filter(
        (p) =>
          departmentId != null
          && Number(rowId(p, "departmentId", "DepartmentId")) === Number(departmentId)
      )
      .map((programme) => {
        const dept = departments.find(
          (d) =>
            Number(rowId(d, "departmentId", "DepartmentId"))
            === Number(rowId(programme, "departmentId", "DepartmentId"))
        );
        const faculty = faculties.find(
          (f) =>
            Number(rowId(f, "facultyId", "FacultyId"))
            === Number(rowId(dept, "facultyId", "FacultyId"))
        );

        return {
          programmeId: rowId(programme, "programmeId", "ProgrammeId"),
          name: programme.programmeName ?? programme.name ?? "—",
          durationYears: programme.durationYears ?? programme.DurationYears ?? "—",
          facultyName: faculty?.facultyName ?? faculty?.name ?? "—",
        };
      });
  }, [programmes, departments, faculties, departmentId]);

  const error = scopeError || apiError;

  return (
    <DashboardLayout title="Programmes" navItems={HOD_NAV}>
      {error && <div className="records-error">{error}</div>}
      {isLoading && (
        <p style={{ color: "var(--text-light)", marginBottom: 18 }}>
          Loading programmes…
        </p>
      )}

      <table className="records-table">
        <thead>
          <tr>
            <th>Programme Name</th>
            <th>Duration</th>
            <th>Faculty</th>
          </tr>
        </thead>
        <tbody>
          {rows.length === 0 && !isLoading && (
            <tr>
              <td colSpan={3} style={{ color: "var(--text-light)" }}>
                No programmes are published for your department yet.
              </td>
            </tr>
          )}
          {rows.map((row) => (
            <tr key={row.programmeId}>
              <td>{row.name}</td>
              <td>{row.durationYears === "—" ? "—" : `${row.durationYears} yrs`}</td>
              <td>{row.facultyName}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </DashboardLayout>
  );
}
