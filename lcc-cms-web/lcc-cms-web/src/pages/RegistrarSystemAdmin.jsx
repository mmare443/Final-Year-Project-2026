import { useEffect, useState } from "react";
import DashboardLayout from "../components/DashboardLayout";
import { useAcademicStructure } from "../context/AcademicStructureContext";
import { useStudents } from "../context/StudentsContext";
import { API_ORIGIN, apiFetch } from "../api";
import { REGISTRAR_NAV } from "./registrarNav";
import "./AcademicStructure.css";
import "./StudentRecords.css";

const TABS = [
  { key: "users", label: "Users" },
  { key: "students", label: "Students" },
  { key: "staff", label: "Staff" },
  { key: "faculties", label: "Faculties" },
  { key: "programmes", label: "Programmes" },
  { key: "rooms", label: "Rooms" },
];

export default function RegistrarSystemAdmin() {
  const { faculties, programmes, fetchAll } = useAcademicStructure();
  const { allStudents, fetchAllStudents } = useStudents();
  const [tab, setTab] = useState("users");
  const [users, setUsers] = useState([]);
  const [staff, setStaff] = useState([]);
  const [rooms, setRooms] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [apiError, setApiError] = useState(null);

  useEffect(() => {
    (async () => {
      setIsLoading(true);
      try {
        await Promise.all([fetchAll(), fetchAllStudents()]);
        const [usersRes, staffRes, roomsRes] = await Promise.all([
          apiFetch(`${API_ORIGIN}/api/users/simple`),
          apiFetch(`${API_ORIGIN}/api/staff`),
          apiFetch(`${API_ORIGIN}/api/rooms`),
        ]);
        if (!usersRes.ok || !staffRes.ok || !roomsRes.ok) {
          throw new Error("API returned an error");
        }
        setUsers(await usersRes.json());
        setStaff(await staffRes.json());
        setRooms(await roomsRes.json());
        setApiError(null);
      } catch {
        setApiError(
          "Couldn't reach the backend API. Make sure `dotnet run` is running on https://api.lccbportal.org."
        );
      } finally {
        setIsLoading(false);
      }
    })();
  }, [fetchAll, fetchAllStudents]);

  const tables = {
    users: {
      columns: ["User ID", "Email", "Role"],
      rows: users.map((u) => (
        <tr key={u.userId}>
          <td>{u.userId}</td>
          <td>{u.email}</td>
          <td>{u.role}</td>
        </tr>
      )),
    },
    students: {
      columns: ["Student ID", "Name", "Email", "Programme"],
      rows: allStudents.map((s) => (
        <tr key={s.id}>
          <td>{s.id}</td>
          <td>{s.fullName}</td>
          <td>{s.email}</td>
          <td>{s.programme}</td>
        </tr>
      )),
    },
    staff: {
      columns: ["Staff ID", "Email", "Role", "Department"],
      rows: staff.map((s) => (
        <tr key={s.staffId}>
          <td>{s.staffId}</td>
          <td>{s.email}</td>
          <td>{s.role || s.roleSql}</td>
          <td>{s.departmentName}</td>
        </tr>
      )),
    },
    faculties: {
      columns: ["ID", "Name"],
      rows: faculties.map((f) => (
        <tr key={f.facultyId ?? f.id}>
          <td>{f.facultyId ?? f.id}</td>
          <td>{f.facultyName ?? f.name}</td>
        </tr>
      )),
    },
    programmes: {
      columns: ["ID", "Name", "Duration (years)"],
      rows: programmes.map((p) => (
        <tr key={p.programmeId ?? p.id}>
          <td>{p.programmeId ?? p.id}</td>
          <td>{p.programmeName ?? p.name}</td>
          <td>{p.durationYears}</td>
        </tr>
      )),
    },
    rooms: {
      columns: ["Hostel", "Room", "Capacity", "Occupied"],
      rows: rooms.map((r) => (
        <tr key={r.roomId}>
          <td>{r.hostelName}</td>
          <td>{r.roomNumber}</td>
          <td>{r.capacity}</td>
          <td>{r.activeOccupants}</td>
        </tr>
      )),
    },
  };

  const current = tables[tab];

  return (
    <DashboardLayout title="System Administration" navItems={REGISTRAR_NAV}>
      <p style={{ color: "var(--text-light)", fontSize: 13, marginBottom: 18 }}>
        Directory of users, students, staff, faculties, programmes, and rooms.
      </p>

      {apiError && <div className="records-error">{apiError}</div>}

      <div className="as-tabs">
        {TABS.map((t) => (
          <button
            key={t.key}
            type="button"
            className={`as-tab${tab === t.key ? " as-tab-active" : ""}`}
            onClick={() => setTab(t.key)}
          >
            {t.label}
          </button>
        ))}
      </div>

      {isLoading ? (
        <p style={{ color: "var(--text-light)" }}>Loading directory…</p>
      ) : (
        <table className="records-table">
          <thead>
            <tr>
              {current.columns.map((col) => (
                <th key={col}>{col}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {current.rows.length === 0 ? (
              <tr>
                <td colSpan={current.columns.length}>No records.</td>
              </tr>
            ) : (
              current.rows
            )}
          </tbody>
        </table>
      )}
    </DashboardLayout>
  );
}
