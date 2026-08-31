import { useEffect, useState } from "react";
import DashboardLayout from "../components/DashboardLayout";
import { useMockAuth, ROLES } from "../context/MockAuthContext";
import { useAcademicStructure } from "../context/AcademicStructureContext";
import { useStudents } from "../context/StudentsContext";
import { useLearning } from "../context/LearningContext";
import { API_ORIGIN } from "../context/MockDataContext";
import { LECTURER_NAV, STUDENT_NAV } from "./Attendance";
import "./Attendance.css";
import "./Learning.css";

function allocationLabel(allocation, courses) {
  const course = courses.find((c) => c.id === allocation.courseId);
  if (!course) return `Allocation #${allocation.id}`;
  return `${course.code} — ${course.name} (${allocation.lecturerName})`;
}

function formatDue(iso) {
  if (!iso) return "—";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString();
}

function fileHref(path) {
  if (!path) return "#";
  return `${API_ORIGIN}${path}`;
}

function LecturerLearning() {
  const { courseAllocations, courses, fetchAll, apiError: structureError } = useAcademicStructure();
  const {
    materials, assignments, submissions, apiError,
    fetchMaterials, fetchAssignments, fetchSubmissions,
    uploadMaterial, deleteMaterial, createAssignment, updateAssignment,
    deleteAssignment, gradeSubmission,
  } = useLearning();

  const [tab, setTab] = useState("materials");
  const [allocationId, setAllocationId] = useState("");
  const [formError, setFormError] = useState(null);
  const [materialTitle, setMaterialTitle] = useState("");
  const [materialFile, setMaterialFile] = useState(null);
  const [assignmentForm, setAssignmentForm] = useState({
    title: "", instructions: "", dueDate: "", maxMarks: "100", allowLateSubmissions: false,
  });
  const [gradeAssignmentId, setGradeAssignmentId] = useState("");
  const [gradeDrafts, setGradeDrafts] = useState({});

  useEffect(() => {
    fetchAll();
  }, [fetchAll]);

  useEffect(() => {
    if (!allocationId) return;
    fetchMaterials({ allocationId: Number(allocationId) });
    fetchAssignments({ allocationId: Number(allocationId) });
  }, [allocationId, fetchMaterials, fetchAssignments]);

  const selectedId = Number(allocationId) || null;

  const handleUploadMaterial = async (e) => {
    e.preventDefault();
    setFormError(null);
    try {
      await uploadMaterial(Number(allocationId), materialTitle, materialFile);
      setMaterialTitle("");
      setMaterialFile(null);
      e.target.reset();
    } catch (err) {
      setFormError(err.message);
    }
  };

  const handleCreateAssignment = async (e) => {
    e.preventDefault();
    setFormError(null);
    try {
      await createAssignment({
        allocationId: Number(allocationId),
        title: assignmentForm.title,
        instructions: assignmentForm.instructions,
        dueDate: new Date(assignmentForm.dueDate).toISOString(),
        maxMarks: Number(assignmentForm.maxMarks),
        allowLateSubmissions: assignmentForm.allowLateSubmissions,
      });
      setAssignmentForm({
        title: "", instructions: "", dueDate: "", maxMarks: "100", allowLateSubmissions: false,
      });
    } catch (err) {
      setFormError(err.message);
    }
  };

  const toggleLate = async (assignment) => {
    setFormError(null);
    try {
      await updateAssignment(assignment.id, {
        allocationId: assignment.allocationId,
        title: assignment.title,
        instructions: assignment.instructions,
        dueDate: assignment.dueDate,
        maxMarks: assignment.maxMarks,
        allowLateSubmissions: !assignment.allowLateSubmissions,
      });
    } catch (err) {
      setFormError(err.message);
    }
  };

  const loadGrading = async (id) => {
    setGradeAssignmentId(id);
    setFormError(null);
    try {
      const rows = await fetchSubmissions(id);
      const drafts = {};
      rows.forEach((s) => {
        drafts[s.id] = { marks: s.marksAwarded ?? "", feedback: s.feedback ?? "" };
      });
      setGradeDrafts(drafts);
    } catch (err) {
      setFormError(err.message);
    }
  };

  const handleGrade = async (submissionId) => {
    const draft = gradeDrafts[submissionId];
    setFormError(null);
    try {
      await gradeSubmission(submissionId, Number(draft.marks), draft.feedback);
    } catch (err) {
      setFormError(err.message);
    }
  };

  return (
    <>
      {(apiError || structureError) && <p className="att-error">{apiError || structureError}</p>}
      {formError && <p className="att-error">{formError}</p>}

      <div className="att-toolbar">
        <label>
          Class (course allocation)
          <select value={allocationId} onChange={(e) => setAllocationId(e.target.value)}>
            <option value="">Select…</option>
            {courseAllocations.map((a) => (
              <option key={a.id} value={a.id}>{allocationLabel(a, courses)}</option>
            ))}
          </select>
        </label>
      </div>

      <p className="att-note">
        Files are stored locally for now (same pattern as admissions documents). Azure Blob Storage
        is the production target in the SRS. Late submissions are blocked unless you turn on
        “allow late” for that assignment — accepted late work is flagged.
      </p>

      <div className="att-tabs">
        <button type="button" className={`att-tab${tab === "materials" ? " att-tab-active" : ""}`} onClick={() => setTab("materials")}>Materials</button>
        <button type="button" className={`att-tab${tab === "assignments" ? " att-tab-active" : ""}`} onClick={() => setTab("assignments")}>Assignments</button>
        <button type="button" className={`att-tab${tab === "grading" ? " att-tab-active" : ""}`} onClick={() => setTab("grading")}>Grading</button>
      </div>

      {tab === "materials" && (
        <>
          <form className="att-toolbar learn-form" onSubmit={handleUploadMaterial}>
            <label>
              Title
              <input value={materialTitle} onChange={(e) => setMaterialTitle(e.target.value)} required disabled={!selectedId} />
            </label>
            <label>
              File
              <input type="file" onChange={(e) => setMaterialFile(e.target.files[0] || null)} required disabled={!selectedId} />
            </label>
            <button type="submit" className="att-open-btn" disabled={!selectedId}>Upload</button>
          </form>
          <table className="att-table">
            <thead>
              <tr>
                <th>Title</th>
                <th>File</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {materials.length === 0 ? (
                <tr><td colSpan={3} className="att-empty">No materials for this class yet.</td></tr>
              ) : materials.map((m) => (
                <tr key={m.id}>
                  <td>{m.title}</td>
                  <td><a href={fileHref(m.path)} target="_blank" rel="noreferrer">{m.fileName}</a></td>
                  <td>
                    <button type="button" className="att-open-btn" onClick={() => deleteMaterial(m.id)}>Remove</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}

      {tab === "assignments" && (
        <>
          <form className="as-form learn-assign-form" onSubmit={handleCreateAssignment}>
            <div className="as-form-fields">
              <label>
                Title
                <input value={assignmentForm.title} onChange={(e) => setAssignmentForm((p) => ({ ...p, title: e.target.value }))} required disabled={!selectedId} />
              </label>
              <label>
                Due date
                <input type="datetime-local" value={assignmentForm.dueDate} onChange={(e) => setAssignmentForm((p) => ({ ...p, dueDate: e.target.value }))} required disabled={!selectedId} />
              </label>
              <label>
                Max marks
                <input type="number" min="1" step="0.5" value={assignmentForm.maxMarks} onChange={(e) => setAssignmentForm((p) => ({ ...p, maxMarks: e.target.value }))} required disabled={!selectedId} />
              </label>
              <label className="learn-check">
                <input type="checkbox" checked={assignmentForm.allowLateSubmissions} onChange={(e) => setAssignmentForm((p) => ({ ...p, allowLateSubmissions: e.target.checked }))} disabled={!selectedId} />
                Allow late submissions
              </label>
            </div>
            <label className="learn-instructions">
              Instructions
              <textarea value={assignmentForm.instructions} onChange={(e) => setAssignmentForm((p) => ({ ...p, instructions: e.target.value }))} rows={3} disabled={!selectedId} />
            </label>
            <button type="submit" className="att-save-btn" disabled={!selectedId}>Create assignment</button>
          </form>

          <table className="att-table">
            <thead>
              <tr>
                <th>Assignment</th>
                <th>Due</th>
                <th>Max</th>
                <th>Late</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {assignments.length === 0 ? (
                <tr><td colSpan={5} className="att-empty">No assignments for this class yet.</td></tr>
              ) : assignments.map((a) => (
                <tr key={a.id}>
                  <td>
                    <strong>{a.title}</strong>
                    <div className="learn-muted">{a.instructions}</div>
                  </td>
                  <td>{formatDue(a.dueDate)}</td>
                  <td>{a.maxMarks}</td>
                  <td>
                    <span className={`att-badge ${a.allowLateSubmissions ? "att-badge-ok" : "att-badge-late"}`}>
                      {a.allowLateSubmissions ? "Allowed" : "Restricted"}
                    </span>
                  </td>
                  <td>
                    <button type="button" className="att-open-btn" onClick={() => toggleLate(a)}>
                      {a.allowLateSubmissions ? "Block late" : "Allow late"}
                    </button>
                    {" "}
                    <button type="button" className="att-open-btn" onClick={() => deleteAssignment(a.id)}>Delete</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}

      {tab === "grading" && (
        <>
          <div className="att-toolbar">
            <label>
              Assignment
              <select
                value={gradeAssignmentId}
                onChange={(e) => {
                  const id = e.target.value;
                  if (id) loadGrading(id);
                  else setGradeAssignmentId("");
                }}
                disabled={!selectedId}
              >
                <option value="">Select…</option>
                {assignments.map((a) => (
                  <option key={a.id} value={a.id}>{a.title}</option>
                ))}
              </select>
            </label>
          </div>
          <table className="att-table">
            <thead>
              <tr>
                <th>Student</th>
                <th>File</th>
                <th>Late</th>
                <th>Marks</th>
                <th>Feedback</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {submissions.length === 0 ? (
                <tr><td colSpan={6} className="att-empty">No submissions yet.</td></tr>
              ) : submissions.map((s) => (
                <tr key={s.id}>
                  <td>{s.studentName}<br /><span className="learn-muted">{s.studentId}</span></td>
                  <td><a href={fileHref(s.fileUrl)} target="_blank" rel="noreferrer">{s.fileName}</a></td>
                  <td>{s.isLate ? <span className="att-badge att-badge-late">Late</span> : "—"}</td>
                  <td>
                    <input
                      className="learn-marks"
                      type="number"
                      min="0"
                      value={gradeDrafts[s.id]?.marks ?? ""}
                      onChange={(e) => setGradeDrafts((p) => ({
                        ...p,
                        [s.id]: { ...p[s.id], marks: e.target.value },
                      }))}
                    />
                  </td>
                  <td>
                    <input
                      className="learn-feedback"
                      value={gradeDrafts[s.id]?.feedback ?? ""}
                      onChange={(e) => setGradeDrafts((p) => ({
                        ...p,
                        [s.id]: { ...p[s.id], feedback: e.target.value },
                      }))}
                    />
                  </td>
                  <td>
                    <button type="button" className="att-save-btn" onClick={() => handleGrade(s.id)}>Save</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </>
  );
}

function StudentLearning() {
  const { myProfile, fetchMyProfile } = useStudents();
  const {
    materials, assignments, submissions, apiError,
    fetchMaterials, fetchAssignments, fetchMySubmissions, submitWork,
  } = useLearning();
  const [formError, setFormError] = useState(null);
  const [files, setFiles] = useState({});

  useEffect(() => {
    (async () => {
      const profile = myProfile || await fetchMyProfile();
      if (!profile?.id) return;
      fetchMaterials({ studentId: profile.id });
      fetchAssignments({ studentId: profile.id });
      fetchMySubmissions(profile.id);
    })();
  }, [myProfile, fetchMyProfile, fetchMaterials, fetchAssignments, fetchMySubmissions]);

  const submissionFor = (assignmentId) =>
    submissions.find((s) => s.assignmentId === assignmentId);

  const handleSubmit = async (assignmentId) => {
    const file = files[assignmentId];
    if (!file || !myProfile?.id) return;
    setFormError(null);
    try {
      await submitWork(assignmentId, myProfile.id, file);
      setFiles((p) => ({ ...p, [assignmentId]: null }));
    } catch (err) {
      setFormError(err.message);
    }
  };

  return (
    <>
      {apiError && <p className="att-error">{apiError}</p>}
      {formError && <p className="att-error">{formError}</p>}

      <h2 className="att-section-title">Learning materials</h2>
      <table className="att-table">
        <thead>
          <tr>
            <th>Unit</th>
            <th>Title</th>
            <th>File</th>
          </tr>
        </thead>
        <tbody>
          {materials.length === 0 ? (
            <tr><td colSpan={3} className="att-empty">No materials posted yet.</td></tr>
          ) : materials.map((m) => (
            <tr key={m.id}>
              <td>{m.courseCode}</td>
              <td>{m.title}</td>
              <td><a href={fileHref(m.path)} target="_blank" rel="noreferrer">{m.fileName}</a></td>
            </tr>
          ))}
        </tbody>
      </table>

      <h2 className="att-section-title">Assignments</h2>
      {assignments.map((a) => {
        const sub = submissionFor(a.id);
        return (
          <div key={a.id} className="learn-card">
            <h3>{a.courseCode} — {a.title}</h3>
            <p className="learn-muted">{a.instructions}</p>
            <p>Due: {formatDue(a.dueDate)} · Max {a.maxMarks}
              {a.allowLateSubmissions ? " · late submissions allowed" : " · late submissions restricted"}
            </p>
            {sub && (
              <p>
                Submitted {formatDue(sub.submittedAt)}
                {sub.isLate && <span className="att-badge att-badge-late" style={{ marginLeft: 8 }}>Late</span>}
                {" · "}
                <a href={fileHref(sub.fileUrl)} target="_blank" rel="noreferrer">{sub.fileName}</a>
              </p>
            )}
            {sub?.marksAwarded != null && (
              <p><strong>Mark:</strong> {sub.marksAwarded} / {a.maxMarks}
                {sub.feedback ? ` — ${sub.feedback}` : ""}
              </p>
            )}
            <div className="att-toolbar">
              <label>
                File
                <input type="file" onChange={(e) => setFiles((p) => ({ ...p, [a.id]: e.target.files[0] || null }))} />
              </label>
              <button type="button" className="att-save-btn" onClick={() => handleSubmit(a.id)}>
                {sub ? "Resubmit" : "Submit"}
              </button>
            </div>
          </div>
        );
      })}
      {assignments.length === 0 && <p className="att-empty">No assignments for your enrolled units yet.</p>}
    </>
  );
}

export default function Learning() {
  const { role } = useMockAuth();
  const isLecturer = role === ROLES.LECTURER;

  return (
    <DashboardLayout
      title={isLecturer ? "Learning & assignments" : "My assignments"}
      navItems={isLecturer ? LECTURER_NAV : STUDENT_NAV}
    >
      {isLecturer ? <LecturerLearning /> : <StudentLearning />}
    </DashboardLayout>
  );
}
