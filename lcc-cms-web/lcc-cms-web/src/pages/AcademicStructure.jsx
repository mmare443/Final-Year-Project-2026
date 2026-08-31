import { useEffect, useState } from "react";
import DashboardLayout from "../components/DashboardLayout";
import { useAcademicStructure } from "../context/AcademicStructureContext";
import "./AcademicStructure.css";

const NAV = [
  { label: "Overview", path: "/registrar" },
  { label: "Admissions", path: "/registrar" },
  { label: "Student Records", path: "/registrar/students" },
  { label: "Academic Structure", path: "/registrar/academic-structure" },
  { label: "Course Registration", path: "/registrar/registrations" },
  "Staff Management", "Accommodation & Welfare", "System Administration", "Profile",
];

const TABS = [
  { key: "faculties", label: "Faculties" },
  { key: "departments", label: "Departments" },
  { key: "programmes", label: "Programmes" },
  { key: "courses", label: "Courses" },
  { key: "academicYears", label: "Academic Years" },
  { key: "semesters", label: "Semesters" },
  { key: "courseAllocations", label: "Course Allocations" },
];

export default function AcademicStructure() {
  const {
    faculties, departments, programmes, courses, academicYears, semesters, courseAllocations,
    isLoading, apiError, fetchAll, create, activateSemester,
  } = useAcademicStructure();
  const [activeTab, setActiveTab] = useState("faculties");
  const [formOpen, setFormOpen] = useState(false);
  const [form, setForm] = useState({});
  const [saveError, setSaveError] = useState(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    fetchAll();
  }, [fetchAll]);

  const dataByTab = { faculties, departments, programmes, courses, academicYears, semesters, courseAllocations };
  const rows = dataByTab[activeTab] || [];

  const openForm = () => {
    setForm({});
    setSaveError(null);
    setFormOpen(true);
  };

  const handleFieldChange = (field, value) => {
    setForm((prev) => ({ ...prev, [field]: value }));
  };

  const handleCreate = async (e) => {
    e.preventDefault();
    setSaving(true);
    setSaveError(null);
    try {
      await create(activeTab, form);
      setFormOpen(false);
      setForm({});
    } catch (err) {
      setSaveError(err.message || "Couldn't save — check the backend API is running.");
    } finally {
      setSaving(false);
    }
  };

  const handleActivate = async (id) => {
    try {
      await activateSemester(id);
    } catch (err) {
      alert("Couldn't activate — check the backend API is running.");
    }
  };

  const renderFormFields = () => {
    switch (activeTab) {
      case "faculties":
        return (
          <label>Name <input value={form.name || ""} onChange={(e) => handleFieldChange("name", e.target.value)} required /></label>
        );
      case "departments":
        return (
          <>
            <label>Name <input value={form.name || ""} onChange={(e) => handleFieldChange("name", e.target.value)} required /></label>
            <label>Faculty
              <select value={form.facultyId || ""} onChange={(e) => handleFieldChange("facultyId", Number(e.target.value))} required>
                <option value="">Select…</option>
                {faculties.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}
              </select>
            </label>
          </>
        );
      case "programmes":
        return (
          <>
            <label>Name <input value={form.name || ""} onChange={(e) => handleFieldChange("name", e.target.value)} required /></label>
            <label>Department
              <select value={form.departmentId || ""} onChange={(e) => handleFieldChange("departmentId", Number(e.target.value))} required>
                <option value="">Select…</option>
                {departments.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
              </select>
            </label>
            <label>Duration (years) <input type="number" min="1" max="4" value={form.durationYears || ""} onChange={(e) => handleFieldChange("durationYears", Number(e.target.value))} required /></label>
          </>
        );
      case "courses":
        return (
          <>
            <label>Code <input value={form.code || ""} onChange={(e) => handleFieldChange("code", e.target.value)} required /></label>
            <label>Name <input value={form.name || ""} onChange={(e) => handleFieldChange("name", e.target.value)} required /></label>
            <label>Programme
              <select value={form.programmeId || ""} onChange={(e) => handleFieldChange("programmeId", Number(e.target.value))} required>
                <option value="">Select…</option>
                {programmes.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
              </select>
            </label>
            <label>Credit Value <input type="number" min="1" value={form.creditValue || ""} onChange={(e) => handleFieldChange("creditValue", Number(e.target.value))} required /></label>
            <label>Year Level <input type="number" min="1" max="3" value={form.yearLevel || ""} onChange={(e) => handleFieldChange("yearLevel", Number(e.target.value))} required /></label>
            <label>Semester Number <input type="number" min="1" max="2" value={form.semesterNumber || ""} onChange={(e) => handleFieldChange("semesterNumber", Number(e.target.value))} required /></label>
            <label>Prerequisite Course (optional)
              <select value={form.prerequisiteCourseId || ""} onChange={(e) => handleFieldChange("prerequisiteCourseId", e.target.value ? Number(e.target.value) : null)}>
                <option value="">None</option>
                {courses.map((c) => <option key={c.id} value={c.id}>{c.code} — {c.name}</option>)}
              </select>
            </label>
          </>
        );
      case "academicYears":
        return (
          <label>Name (e.g. 2027) <input value={form.name || ""} onChange={(e) => handleFieldChange("name", e.target.value)} required /></label>
        );
      case "semesters":
        return (
          <>
            <label>Academic Year
              <select value={form.academicYearId || ""} onChange={(e) => handleFieldChange("academicYearId", Number(e.target.value))} required>
                <option value="">Select…</option>
                {academicYears.map((y) => <option key={y.id} value={y.id}>{y.name}</option>)}
              </select>
            </label>
            <label>Semester Number <input type="number" min="1" max="2" value={form.semesterNumber || ""} onChange={(e) => handleFieldChange("semesterNumber", Number(e.target.value))} required /></label>
            <label>Start Date <input type="date" value={form.startDate || ""} onChange={(e) => handleFieldChange("startDate", e.target.value)} required /></label>
            <label>End Date <input type="date" value={form.endDate || ""} onChange={(e) => handleFieldChange("endDate", e.target.value)} required /></label>
          </>
        );
      case "courseAllocations":
        return (
          <>
            <label>Course
              <select value={form.courseId || ""} onChange={(e) => handleFieldChange("courseId", Number(e.target.value))} required>
                <option value="">Select…</option>
                {courses.map((c) => <option key={c.id} value={c.id}>{c.code} — {c.name}</option>)}
              </select>
            </label>
            <label>Semester
              <select value={form.semesterId || ""} onChange={(e) => handleFieldChange("semesterId", Number(e.target.value))} required>
                <option value="">Select…</option>
                {semesters.map((s) => <option key={s.id} value={s.id}>Year {s.academicYearId} — Sem {s.semesterNumber}{s.isActive ? " (active)" : ""}</option>)}
              </select>
            </label>
            <label>Lecturer Name <input value={form.lecturerName || ""} onChange={(e) => handleFieldChange("lecturerName", e.target.value)} required /></label>
          </>
        );
      default:
        return null;
    }
  };

  const renderRow = (row) => {
    switch (activeTab) {
      case "faculties":
        return <tr key={row.id}><td>{row.id}</td><td>{row.name}</td></tr>;
      case "departments": {
        const fac = faculties.find((f) => f.id === row.facultyId);
        return <tr key={row.id}><td>{row.id}</td><td>{row.name}</td><td>{fac?.name || "—"}</td></tr>;
      }
      case "programmes": {
        const dept = departments.find((d) => d.id === row.departmentId);
        return <tr key={row.id}><td>{row.id}</td><td>{row.name}</td><td>{dept?.name || "—"}</td><td>{row.durationYears} yrs</td></tr>;
      }
      case "courses": {
        const prog = programmes.find((p) => p.id === row.programmeId);
        const prereq = courses.find((c) => c.id === row.prerequisiteCourseId);
        return (
          <tr key={row.id}>
            <td>{row.code}</td><td>{row.name}</td><td>{prog?.name || "—"}</td>
            <td>{row.creditValue}</td><td>Y{row.yearLevel} S{row.semesterNumber}</td>
            <td>{prereq ? prereq.code : "—"}</td>
          </tr>
        );
      }
      case "academicYears":
        return <tr key={row.id}><td>{row.id}</td><td>{row.name}</td></tr>;
      case "semesters": {
        const year = academicYears.find((y) => y.id === row.academicYearId);
        return (
          <tr key={row.id}>
            <td>{year?.name || "—"}</td><td>Semester {row.semesterNumber}</td>
            <td>{row.startDate} → {row.endDate}</td>
            <td>{row.isActive ? <span className="as-active-badge">Active</span> : "—"}</td>
            <td>{!row.isActive && <button className="as-activate-btn" onClick={() => handleActivate(row.id)}>Activate</button>}</td>
          </tr>
        );
      }
      case "courseAllocations": {
        const course = courses.find((c) => c.id === row.courseId);
        const sem = semesters.find((s) => s.id === row.semesterId);
        return (
          <tr key={row.id}>
            <td>{course ? `${course.code} — ${course.name}` : "—"}</td>
            <td>{sem ? `Sem ${sem.semesterNumber}${sem.isActive ? " (active)" : ""}` : "—"}</td>
            <td>{row.lecturerName}</td>
          </tr>
        );
      }
      default:
        return null;
    }
  };

  const columnsByTab = {
    faculties: ["ID", "Name"],
    departments: ["ID", "Name", "Faculty"],
    programmes: ["ID", "Name", "Department", "Duration"],
    courses: ["Code", "Name", "Programme", "Credits", "Year/Sem", "Prerequisite"],
    academicYears: ["ID", "Name"],
    semesters: ["Year", "Semester", "Dates", "Status", "Action"],
    courseAllocations: ["Course", "Semester", "Lecturer"],
  };

  return (
    <DashboardLayout title="Academic Structure — M3" navItems={NAV}>
      {apiError && <div className="as-error">{apiError}</div>}

      <div className="as-tabs">
        {TABS.map((t) => (
          <button
            key={t.key}
            className={`as-tab${activeTab === t.key ? " as-tab-active" : ""}`}
            onClick={() => { setActiveTab(t.key); setFormOpen(false); }}
          >
            {t.label}
          </button>
        ))}
      </div>

      <div className="as-toolbar">
        <button className="as-add-btn" onClick={openForm}>+ Add {TABS.find((t) => t.key === activeTab)?.label.replace(/s$/, "")}</button>
      </div>

      {formOpen && (
        <form className="as-form" onSubmit={handleCreate}>
          {saveError && <div className="as-error">{saveError}</div>}
          <div className="as-form-fields">{renderFormFields()}</div>
          <div className="as-form-actions">
            <button type="submit" className="as-save-btn" disabled={saving}>{saving ? "Saving…" : "Save"}</button>
            <button type="button" className="as-cancel-btn" onClick={() => setFormOpen(false)}>Cancel</button>
          </div>
        </form>
      )}

      {isLoading ? (
        <p style={{ color: "var(--text-light)" }}>Loading…</p>
      ) : (
        <table className="as-table">
          <thead>
            <tr>{columnsByTab[activeTab].map((c) => <th key={c}>{c}</th>)}</tr>
          </thead>
          <tbody>
            {rows.length === 0 ? (
              <tr><td colSpan={columnsByTab[activeTab].length} className="as-empty">No records yet.</td></tr>
            ) : rows.map(renderRow)}
          </tbody>
        </table>
      )}
    </DashboardLayout>
  );
}
