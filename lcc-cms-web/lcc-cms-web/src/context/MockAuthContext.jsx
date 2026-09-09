import { createContext, useContext, useState, useEffect, useCallback } from "react";
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

const MockAuthContext = createContext(null);

export function MockAuthProvider({ children }) {
  const [role, setRole] = useState(null);
  const [jobTitle, setJobTitle] = useState(null);
  const [displayName, setDisplayName] = useState("");
  const [avatarUrl, setAvatarUrl] = useState(null);
  const [ready, setReady] = useState(false);

  const applyMe = (me) => {
    setRole(me.role);
    setJobTitle(me.jobTitle ?? null);
    setDisplayName(me.email || "");
  };

  const signOut = useCallback(() => {
    clearAccessToken();
    setRole(null);
    setJobTitle(null);
    setDisplayName("");
    setAvatarUrl(null);
  }, []);

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
        if (!cancelled) applyMe(me);
      } catch {
        if (!cancelled) signOut();
      } finally {
        if (!cancelled) setReady(true);
      }
    };
    restore();
    return () => { cancelled = true; };
  }, [signOut]);

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

    const meRes = await apiFetch(`${API_ORIGIN}/api/me`);
    if (meRes.ok) {
      applyMe(await meRes.json());
    } else {
      setRole(data.role);
      setDisplayName(data.email || email);
      setJobTitle(null);
    }
    return data.role;
  };

  const setAvatar = (file) => {
    if (!file) return;
    const objectUrl = URL.createObjectURL(file);
    setAvatarUrl(objectUrl);
  };

  const value = {
    isAuthenticated: role !== null,
    ready,
    role,
    jobTitle,
    displayName,
    avatarUrl,
    setAvatar,
    login,
    signOut,
  };

  return (
    <MockAuthContext.Provider value={value}>
      {children}
    </MockAuthContext.Provider>
  );
}

export function useMockAuth() {
  const ctx = useContext(MockAuthContext);
  if (!ctx) {
    throw new Error("useMockAuth must be used within a MockAuthProvider");
  }
  return ctx;
}
