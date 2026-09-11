import { useCallback, useEffect, useState } from "react";
import DashboardLayout from "../components/DashboardLayout";
import { API_ORIGIN, apiFetch } from "../api";
import { REGISTRAR_NAV } from "./registrarNav";
import "./AcademicStructure.css";
import "./StudentRecords.css";

const TABS = [
  { key: "rooms", label: "Rooms" },
  { key: "allocations", label: "Allocations" },
  { key: "welfare", label: "Welfare" },
];

export default function AccommodationWelfare() {
  const [tab, setTab] = useState("rooms");
  const [hostels, setHostels] = useState([]);
  const [rooms, setRooms] = useState([]);
  const [allocations, setAllocations] = useState([]);
  const [cases, setCases] = useState([]);
  const [staff, setStaff] = useState([]);
  const [students, setStudents] = useState([]);
  const [notes, setNotes] = useState([]);
  const [selectedCaseId, setSelectedCaseId] = useState(null);
  const [apiError, setApiError] = useState(null);
  const [saveError, setSaveError] = useState(null);
  const [hostelName, setHostelName] = useState("");
  const [roomForm, setRoomForm] = useState({ hostelId: "", roomNumber: "", capacity: 2 });
  const [allocForm, setAllocForm] = useState({ studentId: "", roomId: "" });
  const [caseForm, setCaseForm] = useState({ studentId: "", assignedOfficerId: "", caseType: "" });
  const [noteText, setNoteText] = useState("");

  const load = useCallback(async () => {
    try {
      const [hRes, rRes, aRes, wRes, sRes, uRes] = await Promise.all([
        apiFetch(`${API_ORIGIN}/api/hostels`),
        apiFetch(`${API_ORIGIN}/api/rooms`),
        apiFetch(`${API_ORIGIN}/api/accommodation`),
        apiFetch(`${API_ORIGIN}/api/welfare-cases`),
        apiFetch(`${API_ORIGIN}/api/staff`),
        apiFetch(`${API_ORIGIN}/api/users/simple`),
      ]);
      if (![hRes, rRes, aRes, wRes, sRes, uRes].every((r) => r.ok)) {
        throw new Error("API error");
      }
      setHostels(await hRes.json());
      setRooms(await rRes.json());
      setAllocations(await aRes.json());
      setCases(await wRes.json());
      setStaff(await sRes.json());
      const users = await uRes.json();
      setStudents(users.filter((u) => String(u.role).toLowerCase() === "student"));
      setApiError(null);
    } catch {
      setApiError(
        "Couldn't reach the backend API. Make sure `dotnet run` is running on http://localhost:5000."
      );
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const post = async (url, body) => {
    const res = await apiFetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    if (!res.ok) {
      const message = await res.text().catch(() => null);
      throw new Error(message || `API returned ${res.status}`);
    }
    return res.json();
  };

  const handleHostel = async (e) => {
    e.preventDefault();
    setSaveError(null);
    try {
      await post(`${API_ORIGIN}/api/hostels`, { hostelName });
      setHostelName("");
      await load();
    } catch (err) {
      setSaveError(err.message);
    }
  };

  const handleRoom = async (e) => {
    e.preventDefault();
    setSaveError(null);
    try {
      await post(`${API_ORIGIN}/api/rooms`, {
        hostelId: Number(roomForm.hostelId),
        roomNumber: roomForm.roomNumber,
        capacity: Number(roomForm.capacity),
      });
      setRoomForm({ hostelId: "", roomNumber: "", capacity: 2 });
      await load();
    } catch (err) {
      setSaveError(err.message);
    }
  };

  const handleAllocate = async (e) => {
    e.preventDefault();
    setSaveError(null);
    try {
      await post(`${API_ORIGIN}/api/accommodation/allocate`, {
        studentId: Number(allocForm.studentId),
        roomId: Number(allocForm.roomId),
      });
      setAllocForm({ studentId: "", roomId: "" });
      await load();
    } catch (err) {
      setSaveError(err.message);
    }
  };

  const handleVacate = async (id) => {
    setSaveError(null);
    try {
      const res = await apiFetch(`${API_ORIGIN}/api/accommodation/${id}/vacate`, { method: "PUT" });
      if (!res.ok) {
        const message = await res.text().catch(() => null);
        throw new Error(message || `API returned ${res.status}`);
      }
      await load();
    } catch (err) {
      setSaveError(err.message);
    }
  };

  const handleCase = async (e) => {
    e.preventDefault();
    setSaveError(null);
    try {
      await post(`${API_ORIGIN}/api/welfare-cases`, {
        studentId: Number(caseForm.studentId),
        assignedOfficerId: Number(caseForm.assignedOfficerId),
        caseType: caseForm.caseType,
      });
      setCaseForm({ studentId: "", assignedOfficerId: "", caseType: "" });
      await load();
    } catch (err) {
      setSaveError(err.message);
    }
  };

  const openNotes = async (caseId) => {
    setSelectedCaseId(caseId);
    setNoteText("");
    const res = await apiFetch(`${API_ORIGIN}/api/welfare-cases/${caseId}/notes`);
    if (res.ok) setNotes(await res.json());
  };

  const handleNote = async (e) => {
    e.preventDefault();
    if (!selectedCaseId) return;
    setSaveError(null);
    try {
      await post(`${API_ORIGIN}/api/welfare-cases/${selectedCaseId}/notes`, { note: noteText });
      setNoteText("");
      await openNotes(selectedCaseId);
    } catch (err) {
      setSaveError(err.message);
    }
  };

  const studentOptions = students.map((s) => (
    <option key={s.userId} value={s.userId}>
      {s.email}
    </option>
  ));

  return (
    <DashboardLayout title="Accommodation & Welfare" navItems={REGISTRAR_NAV}>
      {apiError && <div className="records-error">{apiError}</div>}
      {saveError && <div className="records-error">{saveError}</div>}

      <div className="as-tabs">
        {TABS.map((t) => (
          <button
            key={t.key}
            className={`as-tab${tab === t.key ? " as-tab-active" : ""}`}
            onClick={() => setTab(t.key)}
          >
            {t.label}
          </button>
        ))}
      </div>

      {tab === "rooms" && (
        <>
          <form className="as-form" onSubmit={handleHostel}>
            <div className="as-form-fields">
              <label>New hostel
                <input value={hostelName} onChange={(e) => setHostelName(e.target.value)} required />
              </label>
            </div>
            <div className="as-form-actions">
              <button type="submit" className="as-save-btn">Add hostel</button>
            </div>
          </form>
          <form className="as-form" onSubmit={handleRoom}>
            <div className="as-form-fields">
              <label>Hostel
                <select value={roomForm.hostelId} onChange={(e) => setRoomForm({ ...roomForm, hostelId: e.target.value })} required>
                  <option value="">Select…</option>
                  {hostels.map((h) => (
                    <option key={h.hostelId} value={h.hostelId}>{h.hostelName}</option>
                  ))}
                </select>
              </label>
              <label>Room number
                <input value={roomForm.roomNumber} onChange={(e) => setRoomForm({ ...roomForm, roomNumber: e.target.value })} required />
              </label>
              <label>Capacity
                <input type="number" min="1" value={roomForm.capacity} onChange={(e) => setRoomForm({ ...roomForm, capacity: e.target.value })} required />
              </label>
            </div>
            <div className="as-form-actions">
              <button type="submit" className="as-save-btn">Add room</button>
            </div>
          </form>
          <table className="records-table">
            <thead>
              <tr>
                <th>Hostel</th>
                <th>Room</th>
                <th>Capacity</th>
                <th>Occupants</th>
                <th>Available</th>
              </tr>
            </thead>
            <tbody>
              {rooms.map((r) => (
                <tr key={r.roomId}>
                  <td>{r.hostelName}</td>
                  <td>{r.roomNumber}</td>
                  <td>{r.capacity}</td>
                  <td>{r.activeOccupants}</td>
                  <td>{r.availableSpaces}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}

      {tab === "allocations" && (
        <>
          <form className="as-form" onSubmit={handleAllocate}>
            <div className="as-form-fields">
              <label>Student
                <select value={allocForm.studentId} onChange={(e) => setAllocForm({ ...allocForm, studentId: e.target.value })} required>
                  <option value="">Select…</option>
                  {studentOptions}
                </select>
              </label>
              <label>Room
                <select value={allocForm.roomId} onChange={(e) => setAllocForm({ ...allocForm, roomId: e.target.value })} required>
                  <option value="">Select…</option>
                  {rooms.map((r) => (
                    <option key={r.roomId} value={r.roomId}>
                      {r.hostelName} {r.roomNumber} ({r.availableSpaces} free)
                    </option>
                  ))}
                </select>
              </label>
            </div>
            <div className="as-form-actions">
              <button type="submit" className="as-save-btn">Allocate</button>
            </div>
          </form>
          <table className="records-table">
            <thead>
              <tr>
                <th>Student</th>
                <th>Room</th>
                <th>Status</th>
                <th>Allocated</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {allocations.map((a) => (
                <tr key={a.accommodationId}>
                  <td>{a.studentNumber} {a.studentEmail}</td>
                  <td>{a.hostelName} {a.roomNumber}</td>
                  <td>{a.status}</td>
                  <td>{a.dateAllocated}</td>
                  <td>
                    {a.status === "Active" && (
                      <button className="records-edit-btn" onClick={() => handleVacate(a.accommodationId)}>Vacate</button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}

      {tab === "welfare" && (
        <>
          <form className="as-form" onSubmit={handleCase}>
            <div className="as-form-fields">
              <label>Student
                <select value={caseForm.studentId} onChange={(e) => setCaseForm({ ...caseForm, studentId: e.target.value })} required>
                  <option value="">Select…</option>
                  {studentOptions}
                </select>
              </label>
              <label>Officer
                <select value={caseForm.assignedOfficerId} onChange={(e) => setCaseForm({ ...caseForm, assignedOfficerId: e.target.value })} required>
                  <option value="">Select…</option>
                  {staff.map((s) => (
                    <option key={s.staffId} value={s.staffId}>{s.email} ({s.jobTitle})</option>
                  ))}
                </select>
              </label>
              <label>Case type
                <input value={caseForm.caseType} onChange={(e) => setCaseForm({ ...caseForm, caseType: e.target.value })} required />
              </label>
            </div>
            <div className="as-form-actions">
              <button type="submit" className="as-save-btn">Open case</button>
            </div>
          </form>
          <table className="records-table">
            <thead>
              <tr>
                <th>Student</th>
                <th>Type</th>
                <th>Status</th>
                <th>Officer</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {cases.map((c) => (
                <tr key={c.caseId}>
                  <td>{c.studentNumber}</td>
                  <td>{c.caseType}</td>
                  <td>{c.status}</td>
                  <td>{c.assignedOfficerEmail}</td>
                  <td>
                    <button className="records-edit-btn" onClick={() => openNotes(c.caseId)}>Notes</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {selectedCaseId && (
            <form className="as-form" onSubmit={handleNote}>
              <p style={{ fontSize: 13, color: "var(--text-light)" }}>Notes for case #{selectedCaseId}</p>
              <ul>
                {notes.map((n) => (
                  <li key={n.caseNoteId}>{n.createdAt}: {n.staffEmail} — {n.note}</li>
                ))}
              </ul>
              <div className="as-form-fields">
                <label>Add note
                  <input value={noteText} onChange={(e) => setNoteText(e.target.value)} required />
                </label>
              </div>
              <div className="as-form-actions">
                <button type="submit" className="as-save-btn">Add note</button>
              </div>
            </form>
          )}
        </>
      )}
    </DashboardLayout>
  );
}
