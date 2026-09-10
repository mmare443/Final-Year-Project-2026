import { useEffect, useState } from "react";
import DashboardLayout from "../components/DashboardLayout";
import { useStudents } from "../context/StudentsContext";
import { API_ORIGIN, apiFetch } from "../api";
import { STUDENT_NAV } from "./Attendance";
import "./StudentRecords.css";

const GRADE_POINTS = { A: 4, B: 3, C: 2, D: 1, F: 0 };

function letterPoints(letter) {
  if (!letter) return null;
  const key = String(letter).trim().toUpperCase();
  return Object.prototype.hasOwnProperty.call(GRADE_POINTS, key)
    ? GRADE_POINTS[key]
    : null;
}

function formatGpa(value) {
  if (value == null || value === "") return "—";
  const n = Number(value);
  return Number.isFinite(n) ? n.toFixed(2) : "—";
}

export default function Results() {
  const { myProfile, fetchMyProfile } = useStudents();
  const [courses, setCourses] = useState([]);
  const [semesterGpa, setSemesterGpa] = useState(null);
  const [cgpa, setCgpa] = useState(null);
  const [isLoading, setIsLoading] = useState(false);
  const [apiError, setApiError] = useState(null);

  useEffect(() => {
    (async () => {
      setIsLoading(true);
      try {
        const profile = myProfile || await fetchMyProfile();
        const studentNumber = profile?.id;
        if (!studentNumber) {
          setApiError("Couldn't load your student profile.");
          return;
        }

        const [transcriptRes, gpaRes, cgpaRes] = await Promise.all([
          apiFetch(`${API_ORIGIN}/api/students/${encodeURIComponent(studentNumber)}/transcript`),
          apiFetch(`${API_ORIGIN}/api/students/${encodeURIComponent(studentNumber)}/gpa`),
          apiFetch(`${API_ORIGIN}/api/students/${encodeURIComponent(studentNumber)}/cgpa`),
        ]);

        if (!transcriptRes.ok || !gpaRes.ok || !cgpaRes.ok) {
          throw new Error("API error");
        }

        const transcript = await transcriptRes.json();
        const gpa = await gpaRes.json();
        const cgpaRecord = await cgpaRes.json();
        setCourses(transcript.completedCourses || []);
        setSemesterGpa(gpa.gpa);
        setCgpa(cgpaRecord.cgpa);
        setApiError(null);
      } catch {
        setApiError(
          "Couldn't reach the backend API. Make sure `dotnet run` is running on http://localhost:5000."
        );
      } finally {
        setIsLoading(false);
      }
    })();
  }, [myProfile, fetchMyProfile]);

  return (
    <DashboardLayout title="Results" navItems={STUDENT_NAV}>
      <p style={{ color: "var(--text-light)", fontSize: 13, marginBottom: 18 }}>
        Published, completed courses only. Semester GPA is the active semester;
        CGPA is cumulative.
      </p>

      {apiError && <div className="records-error">{apiError}</div>}

      <div className="dash-card-grid" style={{ marginBottom: 18 }}>
        <div className="dash-card">
          <h3>Semester GPA</h3>
          <div className="dash-card-value">{formatGpa(semesterGpa)}</div>
        </div>
        <div className="dash-card">
          <h3>CGPA</h3>
          <div className="dash-card-value">{formatGpa(cgpa)}</div>
        </div>
      </div>

      {isLoading ? (
        <p style={{ color: "var(--text-light)" }}>Loading results…</p>
      ) : (
        <table className="records-table">
          <thead>
            <tr>
              <th>Course code</th>
              <th>Course name</th>
              <th>Grade</th>
              <th>Credit value</th>
              <th>Grade points</th>
            </tr>
          </thead>
          <tbody>
            {courses.length === 0 && (
              <tr>
                <td colSpan={5} style={{ color: "var(--text-light)" }}>
                  No published results yet.
                </td>
              </tr>
            )}
            {courses.map((row, index) => {
              const points = letterPoints(row.courseLetter);
              const credits = Number(row.credits);
              const quality = points == null || !Number.isFinite(credits)
                ? "—"
                : (points * credits).toFixed(1);
              return (
                <tr key={`${row.courseCode}-${index}`}>
                  <td>{row.courseCode}</td>
                  <td>{row.courseName}</td>
                  <td>{row.courseLetter}</td>
                  <td>{row.credits}</td>
                  <td>{quality}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}
    </DashboardLayout>
  );
}
