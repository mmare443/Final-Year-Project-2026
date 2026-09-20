import { createContext, useContext, useState, useEffect, useCallback, useRef } from "react";
import { API_ORIGIN, apiFetch, setAccessToken, getAccessToken, clearAccessToken } from "../api";

/**
 * Auth context — local JWT (email/password), not Microsoft Entra / MSAL.
 * Dashboards still read role, jobTitle, displayName, and signOut().
 */

export const ROLES = {
  STUDENT: "Student",
  LECTURER: "Lecturer",
  HOD: "HoD",
  REGISTRAR_ADMIN: "RegistrarAdmin",
  MANAGEMENT_PRINCIPAL: "ManagementPrincipal",
};

export const ROLE_LABELS = {
  [ROLES.STUDENT]: "Student",
  [ROLES.LECTURER]: "Lecturer",
  [ROLES.HOD]: "Head of Department",
  [ROLES.REGISTRAR_ADMIN]: "Registrar / Admin",
  [ROLES.MANAGEMENT_PRINCIPAL]: "Management / Principal",
};

export const JOB_TITLES = {
  REGISTRAR: "Registrar",
  DEAN_OF_STUDIES: "Dean of Studies",
  ADMIN_OFFICER: "Admin Officer",
  ACCOUNTS: "Accounts",
};

const MUST_CHANGE_KEY = "lcc_must_change_password";

function readMustChange() {
  return sessionStorage.getItem(MUST_CHANGE_KEY) === "1";
}

function writeMustChange(value) {
  if (value) sessionStorage.setItem(MUST_CHANGE_KEY, "1");
  else sessionStorage.removeItem(MUST_CHANGE_KEY);
}

export const ROLE_HOME = {
  [ROLES.STUDENT]: "/student",
  [ROLES.LECTURER]: "/lecturer",
  [ROLES.HOD]: "/hod",
  [ROLES.REGISTRAR_ADMIN]: "/registrar",
  [ROLES.MANAGEMENT_PRINCIPAL]: "/management",
};

function readMustChangeFlag(obj) {
  if (!obj || typeof obj !== "object") return null;
  if (Object.prototype.hasOwnProperty.call(obj, "mustChangePassword")) {
    return !!obj.mustChangePassword;
  }
  if (Object.prototype.hasOwnProperty.call(obj, "MustChangePassword")) {
    return !!obj.MustChangePassword;
  }
  return null;
}

const MockAuthContext = createContext(null);

export function MockAuthProvider({ children }) {
  const [role, setRole] = useState(null);
  const [jobTitle, setJobTitle] = useState(null);
  const [displayName, setDisplayName] = useState("");
  const [email, setEmail] = useState("");
  const [studentNumber, setStudentNumber] = useState("");
  const [staffNumber, setStaffNumber] = useState("");
  const [avatarUrl, setAvatarUrl] = useState(null);
  const [mustChangePassword, setMustChangePassword] = useState(readMustChange);
  const [ready, setReady] = useState(false);
  const avatarObjectUrlRef = useRef(null);

  const clearAvatar = useCallback(() => {
    if (avatarObjectUrlRef.current) {
      URL.revokeObjectURL(avatarObjectUrlRef.current);
      avatarObjectUrlRef.current = null;
    }
    setAvatarUrl(null);
  }, []);

  const loadProfilePhoto = useCallback(async () => {
    try {
      const res = await apiFetch(`${API_ORIGIN}/api/profile/photo`);
      if (!res.ok) {
        clearAvatar();
        return;
      }
      const blob = await res.blob();
      const objectUrl = URL.createObjectURL(blob);
      if (avatarObjectUrlRef.current) {
        URL.revokeObjectURL(avatarObjectUrlRef.current);
      }
      avatarObjectUrlRef.current = objectUrl;
      setAvatarUrl(objectUrl);
    } catch {
      clearAvatar();
    }
  }, [clearAvatar]);

  const applyMe = (me) => {
    setRole(me.role);
    setJobTitle(me.jobTitle ?? null);
    setDisplayName(me.displayName || me.email || "");
    setEmail(me.email || "");
    setStudentNumber(me.studentNumber || "");
    setStaffNumber(me.staffNumber || "");
    const must = readMustChangeFlag(me);
    if (must === true) {
      setMustChangePassword(true);
      writeMustChange(true);
    } else if (must === false) {
      setMustChangePassword(false);
      writeMustChange(false);
    }
  };

  const signOut = useCallback(() => {
    clearAccessToken();
    writeMustChange(false);
    setMustChangePassword(false);
    setRole(null);
    setJobTitle(null);
    setDisplayName("");
    setEmail("");
    setStudentNumber("");
    setStaffNumber("");
    clearAvatar();
  }, [clearAvatar]);

  useEffect(() => {
    let cancelled = false;
    const restore = async () => {
      const token = getAccessToken();
      if (!token) {
        if (!cancelled) setReady(true);
        return;
      }
      try {
        const res = await apiFetch(`${API_ORIGIN}/api/me`);
        if (!res.ok) throw new Error("session");
        const me = await res.json();
        if (!cancelled) {
          applyMe(me);
          if (readMustChangeFlag(me) !== true && !readMustChange()) {
            await loadProfilePhoto();
          }
        }
      } catch {
        if (!cancelled) signOut();
      } finally {
        if (!cancelled) setReady(true);
      }
    };
    restore();
    return () => { cancelled = true; };
  }, [signOut, loadProfilePhoto]);

  const login = async (email, password) => {
    const res = await fetch(`${API_ORIGIN}/api/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email, password }),
    });
    if (res.status === 401) {
      throw new Error("Invalid email or password.");
    }
    if (!res.ok) {
      const text = await res.text().catch(() => "");
      throw new Error(text || `Login failed (${res.status}).`);
    }
    const data = await res.json();
    setAccessToken(data.token);
    let must = readMustChangeFlag(data);
    // Fail closed: a missing flag from an outdated API must not open the portal.
    if (must === null) must = true;
    setMustChangePassword(must);
    writeMustChange(must);

    const meRes = await apiFetch(`${API_ORIGIN}/api/me`);
    if (meRes.ok) {
      const me = await meRes.json();
      applyMe(me);
      const meMust = readMustChangeFlag(me);
      if (meMust === true) {
        must = true;
        setMustChangePassword(true);
        writeMustChange(true);
      }
      if (!must) await loadProfilePhoto();
    } else {
      setRole(data.role);
      setDisplayName(data.displayName || data.email || email);
      setEmail(data.email || email);
      setJobTitle(null);
      clearAvatar();
    }
    return { role: data.role, mustChangePassword: must };
  };

  const completePasswordChange = () => {
    setMustChangePassword(false);
    writeMustChange(false);
  };

  const refreshMe = async () => {
    const res = await apiFetch(`${API_ORIGIN}/api/me`);
    if (!res.ok) return;
    applyMe(await res.json());
  };

  const uploadProfilePhoto = async (file) => {
    if (!file) return;
    const formData = new FormData();
    formData.append("photo", file);
    const res = await apiFetch(`${API_ORIGIN}/api/profile/photo`, {
      method: "POST",
      body: formData,
    });
    if (!res.ok) {
      const text = await res.text().catch(() => "");
      throw new Error(text || `Photo upload failed (${res.status}).`);
    }
    await loadProfilePhoto();
  };

  const setAvatar = (file) => {
    uploadProfilePhoto(file).catch(() => {});
  };

  const value = {
    isAuthenticated: role !== null,
    ready,
    role,
    jobTitle,
    displayName,
    email,
    studentNumber,
    staffNumber,
    avatarUrl,
    mustChangePassword,
    setAvatar,
    uploadProfilePhoto,
    loadProfilePhoto,
    completePasswordChange,
    refreshMe,
    login,
    signOut,
  };

  return (
    <MockAuthContext.Provider value={value}>
      {children}
    </MockAuthContext.Provider>
  );
}

export function avatarInitials(displayName) {
  const local = String(displayName || "").trim().split("@")[0];
  const parts = local.split(/[.\s_-]+/).filter(Boolean);
  if (parts.length >= 2) {
    return (parts[0].charAt(0) + parts[1].charAt(0)).toUpperCase();
  }
  if (parts[0]) {
    return parts[0].slice(0, 2).toUpperCase();
  }
  return "";
}

export function useMockAuth() {
  const ctx = useContext(MockAuthContext);
  if (!ctx) {
    throw new Error("useMockAuth must be used within a MockAuthProvider");
  }
  return ctx;
}
