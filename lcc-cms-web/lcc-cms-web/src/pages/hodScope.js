import { API_ORIGIN, apiFetch } from "../api";

export function rowId(row, camel, pascal) {
  return row?.[camel] ?? row?.[pascal] ?? row?.id ?? null;
}

export async function loadHodDepartment() {
  const meRes = await apiFetch(`${API_ORIGIN}/api/me`);
  if (!meRes.ok) throw new Error(`API returned ${meRes.status}`);
  const me = await meRes.json();

  let staff = null;
  if (me.staffId) {
    const staffRes = await apiFetch(`${API_ORIGIN}/api/staff/${me.staffId}`);
    if (staffRes.ok) staff = await staffRes.json();
  }

  return {
    me,
    staff,
    departmentId: staff?.departmentId ?? null,
    departmentName: staff?.departmentName ?? null,
    facultyName: staff?.facultyName ?? null,
  };
}
