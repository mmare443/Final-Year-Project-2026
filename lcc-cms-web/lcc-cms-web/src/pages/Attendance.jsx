import { useEffect, useMemo, useState } from "react";
import DashboardLayout from "../components/DashboardLayout";
import { useMockAuth, ROLES } from "../context/MockAuthContext";
import { useAcademicStructure } from "../context/AcademicStructureContext";
import { useStudents } from "../context/StudentsContext";
import {
  useAttendance,
  ATTENDANCE_STATUSES,
  ATTENDANCE_THRESHOLD,
} from "../context/AttendanceContext";
import "./Attendance.css";

const LECTURER_NAV = [
  { label: "Overview", path: "/lecturer" },
  "My Classes",
  { label: "Attendance", path: "/lecturer/attendance" },
  { label: "Assignments", path: "/lecturer/assignments" },
  { label: "Grading", path: "/lecturer/assignments" },
  "Profile",
];

const HOD_NAV = [
  { label: "Overview", path: "/hod" },
  { label: "Attendance", path: "/hod/attendance" },
  "Department Staff", "Programmes", "Academic Structure", "Reports", "Profile",
];

const STUDENT_NAV = [
  { label: "Overview", path: "/student" },
  "My Courses",
  { label: "Attendance", path: "/student/attendance" },
  { label: "Assignments", path: "/student/assignments" },
  "Results",
  { label: "Profile", path: "/student/profile" },
];

function allocationLabel(allocation, courses) {
  const course = courses.find((c) => c.id === allocation.courseId);
  if (!course) return `Allocation #${allocation.id}`;
  return `${course.code} — ${course.name} (${allocation.lecturerName})`;
}

function RateBadge({ rate }) {
  const low = rate.belowThreshold;
  return (
    <span className={`att-badge ${low ? "att-badge-low" : "att-badge-ok"}`}>
      {rate.ratePercent}%{low ? ` (below ${ATTENDANCE_THRESHOLD}%)` : ""}
    </span>
  );
}

function RateTable({ rows, empty }) {
  if (!rows.length) {
    return <p className="att-empty">{empty}</p>;
  }
  return (
    <table className="att-table">
      <thead>
        <tr>
          <th>Student</th>
          <th>Unit</th>
          <th>Marked</th>
          <th>Attended</th>
          <th>Rate</th>
        </tr>
      </thead>
      <tbody>
        {rows.map((r) => (
          <tr key={`${r.allocationId}-${r.studentId}`}>
            <td>{r.studentName} <span style={{ color: "var(--text-light)" }}>{r.studentId}</span></td>
            <td>{r.courseCode} — {r.courseName}</td>
            <td>{r.sessionsMarked}</td>
            <td>{r.sessionsAttended}</td>
            <td><RateBadge rate={r} /></td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function LecturerAttendance() {
  const { courseAllocations, courses, fetchAll, apiError: structureError } = useAcademicStructure();
  const {
    sessions, sessionDetail, rates, alerts, apiError,
    fetchSessions, fetchSession, openSession, saveMarks, fetchRates, fetchAlerts,
  } = useAttendance();

  const [allocationId, setAllocationId] = useState("");
  const [sessionDate, setSessionDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [activeSessionId, setActiveSessionId] = useState(null);
  const [marks, setMarks] = useState({});
  const [tab, setTab] = useState("register");
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState(null);

  useEffect(() => {
    fetchAll();
  }, [fetchAll]);

  useEffect(() => {
    if (!allocationId) return;
    fetchSessions(Number(allocationId));
    fetchRates({ allocationId: Number(allocationId) });
    fetchAlerts();
  }, [allocationId, fetchSessions, fetchRates, fetchAlerts]);

  useEffect(() => {
    if (!sessionDetail) return;
    const next = {};
    (sessionDetail.roster || []).forEach((s) => {
      const existing = (sessionDetail.marks || []).find((m) => m.studentId === s.studentId);
      next[s.studentId] = existing?.status || "Present";
    });
    setMarks(next);
  }, [sessionDetail]);

  const selectedAllocation = courseAllocations.find((a) => a.id === Number(allocationId));

  const handleOpen = async (e) => {
    e.preventDefault();
    setFormError(null);
    try {
      const created = await openSession(Number(allocationId), sessionDate);
      setActiveSessionId(created.id);
      await fetchSession(created.id);
      setTab("register");
    } catch (err) {
      setFormError(err.message || "Couldn't open session.");
    }
  };

  const handleLoadSession = async (id) => {
    setFormError(null);
    setActiveSessionId(id);
    await fetchSession(id);
    setTab("register");
  };

  const handleSave = async () => {
    if (!activeSessionId) return;
    setSaving(true);
    setFormError(null);
    try {
      const payload = Object.entries(marks).map(([studentId, status]) => ({ studentId, status }));
      await saveMarks(activeSessionId, payload);
      await fetchRates({ allocationId: Number(allocationId) });
      await fetchAlerts();
    } catch (err) {
      setFormError(err.message || "Couldn't save marks.");
    } finally {
      setSaving(false);
    }
  };

  const roster = sessionDetail?.roster || [];

  return (
    <>
      {(apiError || structureError) && (
        <p className="att-error">{apiError || structureError}</p>
      )}
      {formError && <p className="att-error">{formError}</p>}

      <div className="att-tabs">
        <button type="button" className={`att-tab${tab === "register" ? " att-tab-active" : ""}`} onClick={() => setTab("register")}>Take register</button>
        <button type="button" className={`att-tab${tab === "rates" ? " att-tab-active" : ""}`} onClick={() => setTab("rates")}>Rates &amp; reports</button>
      </div>

      <form className="att-toolbar" onSubmit={handleOpen}>
        <label>
          Class (course allocation)
          <select value={allocationId} onChange={(e) => { setAllocationId(e.target.value); setActiveSessionId(null); }} required>
            <option value="">Select…</option>
            {courseAllocations.map((a) => (
              <option key={a.id} value={a.id}>{allocationLabel(a, courses)}</option>
            ))}
          </select>
        </label>
        <label>
          Session date
          <input type="date" value={sessionDate} onChange={(e) => setSessionDate(e.target.value)} required />
        </label>
        <button type="submit" className="att-open-btn" disabled={!allocationId}>Open session</button>
      </form>

      <p className="att-note">
        Present, Late, and Excused count as attended. An alert is sent to the student and HoD
        the moment a unit rate falls below {ATTENDANCE_THRESHOLD}%. Lecturer scoping by staff
        record arrives with M9 — all allocations are listed for now.
      </p>

      {tab === "register" && (
        <>
          <h2 className="att-section-title">Sessions</h2>
          <table className="att-table">
            <thead>
              <tr>
                <th>Date</th>
                <th>Unit</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {sessions.length === 0 ? (
                <tr><td colSpan={3} className="att-empty">No sessions yet for this class.</td></tr>
              ) : sessions.map((s) => (
                <tr key={s.id}>
                  <td>{s.sessionDate}</td>
                  <td>{s.courseCode} — {s.courseName}</td>
                  <td>
                    <button type="button" className="att-open-btn" onClick={() => handleLoadSession(s.id)}>
                      {activeSessionId === s.id ? "Loaded" : "Mark"}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {activeSessionId && sessionDetail?.session && (
            <>
              <h2 className="att-section-title">
                Register — {sessionDetail.session.courseCode} ({sessionDetail.session.sessionDate})
              </h2>
              <table className="att-table">
                <thead>
                  <tr>
                    <th>Student</th>
                    <th>Mark</th>
                  </tr>
                </thead>
                <tbody>
                  {roster.length === 0 ? (
                    <tr><td colSpan={2} className="att-empty">No approved registrations for this class.</td></tr>
                  ) : roster.map((s) => (
                    <tr key={s.studentId}>
                      <td>{s.studentName}<br /><span style={{ color: "var(--text-light)" }}>{s.studentId}</span></td>
                      <td>
                        <div className="att-status">
                          {ATTENDANCE_STATUSES.map((status) => (
                            <label key={status}>
                              <input
                                type="radio"
                                name={`mark-${s.studentId}`}
                                checked={marks[s.studentId] === status}
                                onChange={() => setMarks((prev) => ({ ...prev, [s.studentId]: status }))}
                              />
                              {status}
                            </label>
                          ))}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <button type="button" className="att-save-btn" onClick={handleSave} disabled={saving || roster.length === 0}>
                {saving ? "Saving…" : "Save marks"}
              </button>
            </>
          )}
        </>
      )}

      {tab === "rates" && (
        <>
          <h2 className="att-section-title">
            Unit report{selectedAllocation ? ` — ${allocationLabel(selectedAllocation, courses)}` : ""}
          </h2>
          <RateTable rows={rates} empty="Select a class to see rates, or mark a session first." />
        </>
      )}

      {alerts.filter((a) => !allocationId || a.allocationId === Number(allocationId)).length > 0 && (
        <>
          <h2 className="att-section-title">Low-attendance alerts</h2>
          <div className="att-alert-list">
            {alerts
              .filter((a) => !allocationId || a.allocationId === Number(allocationId))
              .map((a) => (
                <div key={a.id} className="att-alert">
                  <strong>{a.studentName} ({a.studentId})</strong>
                  <span>{a.courseCode} — {a.courseName}: {a.ratePercent}% (threshold {ATTENDANCE_THRESHOLD}%)</span>
                </div>
              ))}
          </div>
        </>
      )}
    </>
  );
}

function HoDAttendance() {
  const { courseAllocations, courses, fetchAll, apiError: structureError } = useAcademicStructure();
  const { alerts, rates, apiError, fetchAlerts, fetchRates, fetchReport } = useAttendance();
  const { allStudents, fetchAllStudents } = useStudents();
  const [view, setView] = useState("alerts");
  const [allocationId, setAllocationId] = useState("");
  const [studentId, setStudentId] = useState("");
  const [reportRows, setReportRows] = useState([]);
  const [reportError, setReportError] = useState(null);

  useEffect(() => {
    fetchAll();
    fetchAllStudents();
    fetchAlerts();
    fetchRates();
  }, [fetchAll, fetchAllStudents, fetchAlerts, fetchRates]);

  const runReport = async (nextView) => {
    setView(nextView);
    setReportError(null);
    try {
      if (nextView === "unit" && allocationId) {
        setReportRows(await fetchReport({ view: "unit", allocationId: Number(allocationId) }));
      } else if (nextView === "student" && studentId) {
        setReportRows(await fetchReport({ view: "student", studentId }));
      } else {
        setReportRows([]);
      }
    } catch (err) {
      setReportError(err.message);
    }
  };

  return (
    <>
      {(apiError || structureError) && (
        <p className="att-error">{apiError || structureError}</p>
      )}
      {reportError && <p className="att-error">{reportError}</p>}

      <div className="dash-card-grid" style={{ marginBottom: 20 }}>
        <div className="dash-card">
          <h3>Low-attendance alerts</h3>
          <div className="dash-card-value">{alerts.length}</div>
        </div>
        <div className="dash-card">
          <h3>Students below {ATTENDANCE_THRESHOLD}%</h3>
          <div className="dash-card-value">{rates.filter((r) => r.belowThreshold).length}</div>
        </div>
      </div>

      <div className="att-tabs">
        <button type="button" className={`att-tab${view === "alerts" ? " att-tab-active" : ""}`} onClick={() => setView("alerts")}>Monitoring</button>
        <button type="button" className={`att-tab${view === "unit" ? " att-tab-active" : ""}`} onClick={() => runReport("unit")}>Report by unit</button>
        <button type="button" className={`att-tab${view === "student" ? " att-tab-active" : ""}`} onClick={() => runReport("student")}>Report by student</button>
      </div>

      {view === "alerts" && (
        <>
          <h2 className="att-section-title">Alerts (student + HoD, auto at {ATTENDANCE_THRESHOLD}%)</h2>
          {alerts.length === 0 ? (
            <p className="att-empty">No active low-attendance alerts.</p>
          ) : (
            <div className="att-alert-list">
              {alerts.map((a) => (
                <div key={a.id} className="att-alert">
                  <strong>{a.studentName} ({a.studentId})</strong>
                  <span>{a.courseCode} — {a.courseName}: {a.ratePercent}%</span>
                </div>
              ))}
            </div>
          )}
          <h2 className="att-section-title">All unit rates</h2>
          <RateTable rows={rates} empty="No attendance recorded yet." />
        </>
      )}

      {view === "unit" && (
        <>
          <div className="att-toolbar">
            <label>
              Unit
              <select value={allocationId} onChange={(e) => setAllocationId(e.target.value)}>
                <option value="">Select…</option>
                {courseAllocations.map((a) => (
                  <option key={a.id} value={a.id}>{allocationLabel(a, courses)}</option>
                ))}
              </select>
            </label>
            <button type="button" className="att-open-btn" onClick={() => runReport("unit")}>Run report</button>
          </div>
          <RateTable rows={reportRows} empty="Select a unit and run the report." />
        </>
      )}

      {view === "student" && (
        <>
          <div className="att-toolbar">
            <label>
              Student
              <select value={studentId} onChange={(e) => setStudentId(e.target.value)}>
                <option value="">Select…</option>
                {allStudents.map((s) => (
                  <option key={s.id} value={s.id}>{s.fullName} ({s.id})</option>
                ))}
              </select>
            </label>
            <button type="button" className="att-open-btn" onClick={() => runReport("student")}>Run report</button>
          </div>
          <RateTable rows={reportRows} empty="Select a student and run the report." />
        </>
      )}
    </>
  );
}

function StudentAttendance() {
  const { myProfile, fetchMyProfile } = useStudents();
  const { rates, alerts, apiError, fetchRates, fetchAlerts } = useAttendance();

  useEffect(() => {
    (async () => {
      const profile = myProfile || await fetchMyProfile();
      const id = profile?.id;
      if (!id) return;
      try {
        await fetchRates({ studentId: id });
        await fetchAlerts(id);
      } catch {
        /* apiError is set by fetchRates/fetchAlerts callers that don't swallow —
           fetchRates throws; show apiError from context after a local catch */
      }
    })();
  }, [myProfile, fetchMyProfile, fetchRates, fetchAlerts]);

  const overall = useMemo(() => {
    if (!rates.length) return null;
    const marked = rates.reduce((sum, r) => sum + r.sessionsMarked, 0);
    const attended = rates.reduce((sum, r) => sum + r.sessionsAttended, 0);
    if (!marked) return 0;
    return Math.round((attended * 1000) / marked) / 10;
  }, [rates]);

  return (
    <>
      {apiError && <p className="att-error">{apiError}</p>}
      <div className="dash-card-grid" style={{ marginBottom: 20 }}>
        <div className="dash-card">
          <h3>Overall attendance</h3>
          <div className="dash-card-value">{overall == null ? "—" : `${overall}%`}</div>
        </div>
        <div className="dash-card">
          <h3>Units below {ATTENDANCE_THRESHOLD}%</h3>
          <div className="dash-card-value">{rates.filter((r) => r.belowThreshold).length}</div>
        </div>
      </div>

      {alerts.length > 0 && (
        <div className="att-alert-list">
          {alerts.map((a) => (
            <div key={a.id} className="att-alert">
              <strong>Attendance alert — {a.courseCode}</strong>
              <span>
                Your rate in {a.courseName} is {a.ratePercent}%, below the {ATTENDANCE_THRESHOLD}% requirement.
                Your Head of Department has been notified.
              </span>
            </div>
          ))}
        </div>
      )}

      <h2 className="att-section-title">By unit</h2>
      <RateTable rows={rates} empty="No attendance has been recorded for you yet." />
    </>
  );
}

export default function Attendance() {
  const { role } = useMockAuth();

  const nav =
    role === ROLES.LECTURER ? LECTURER_NAV :
    role === ROLES.HOD ? HOD_NAV :
    STUDENT_NAV;

  const title =
    role === ROLES.LECTURER ? "Attendance — take register" :
    role === ROLES.HOD ? "Attendance monitoring" :
    "My attendance";

  return (
    <DashboardLayout title={title} navItems={nav}>
      {role === ROLES.LECTURER && <LecturerAttendance />}
      {role === ROLES.HOD && <HoDAttendance />}
      {role === ROLES.STUDENT && <StudentAttendance />}
    </DashboardLayout>
  );
}

export { LECTURER_NAV, HOD_NAV, STUDENT_NAV };
