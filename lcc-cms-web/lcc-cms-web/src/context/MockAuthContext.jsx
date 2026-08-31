import { createContext, useContext, useState } from "react";

/**
 * MOCK AUTH CONTEXT — TEMPORARY.
 *
 * This stands in for real Entra ID / MSAL.js authentication while cloud
 * access (Azure or GCP) is pending. It stores a fake "signed in as" role in
 * memory so every dashboard, route guard, and role-based UI element can be
 * built and tested right now.
 *
 * The 5 roles below match the SRS FR-12.2 RBAC matrix exactly — Student,
 * Lecturer, HoD, Registrar/Admin, Management/Principal. No new roles were
 * added for the Admin Officer / Accounts / Dean of Studies distinction —
 * per M12's explicit "no ad hoc roles" business rule, those three (plus
 * Deputy Principal, folded into Management/Principal) share a role with
 * real, individually-authenticated accounts. `jobTitle` below is a UI-only
 * concern: it picks which dashboard sections render, NOT what the backend
 * authorizes — every job title under RegistrarAdmin hits the exact same
 * [Authorize(Policy = "RegistrarAdminOnly")] backend policy.
 *
 * TO REPLACE LATER (once Entra ID access is confirmed):
 *   1. Delete this file.
 *   2. Install @azure/msal-browser + @azure/msal-react.
 *   3. Wrap <App /> in <MsalProvider> instead of <MockAuthProvider>.
 *   4. Replace useMockAuth() calls with useMsal() + the ID token's
 *      `roles` claim (see the Backend & Frontend Scaffold Guide, Step 5).
 *   5. `jobTitle` becomes a real lookup against the `staff` table (M9)
 *      after login, keyed by the signed-in user's account — not part of
 *      the Entra ID token itself.
 *   6. Nothing in the dashboard components themselves needs to change —
 *      they only read `role`, `jobTitle`, and `signOut()` from context.
 *
 * AVATAR NOTE: `avatarUrl` below is a client-side-only preview (an
 * in-browser object URL for whatever image the user picks) — it is NOT
 * uploaded anywhere and disappears on refresh. Real ID photo storage
 * follows the same pattern already built for M1's admissions documents
 * (see AdmissionsController.cs / MockDataContext.jsx): once a `staff` or
 * `students` table and matching API endpoint exist (M2/M9), swap this for
 * a real multipart upload and a persisted photo URL.
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

// Job titles under the RegistrarAdmin role, per the LCCB org-structure
// mapping table (Module Specification, M12). Display/UI-only — see the
// file header comment above.
export const JOB_TITLES = {
  REGISTRAR: "Registrar",
  DEAN_OF_STUDIES: "Dean of Studies",
  ADMIN_OFFICER: "Admin Officer",
  ACCOUNTS: "Accounts",
};

const MockAuthContext = createContext(null);

export function MockAuthProvider({ children }) {
  const [role, setRole] = useState(null); // null = signed out
  const [jobTitle, setJobTitle] = useState(null);
  const [displayName, setDisplayName] = useState("");
  const [avatarUrl, setAvatarUrl] = useState(null);

  const signIn = (selectedRole, selectedJobTitle = null) => {
    setRole(selectedRole);
    setJobTitle(selectedJobTitle);
    const handle = selectedJobTitle
      ? selectedJobTitle.toLowerCase().replace(/\s+/g, ".")
      : selectedRole.toLowerCase();
    setDisplayName(`test.${handle}@lccb.ac.pg`);
  };

  const signOut = () => {
    setRole(null);
    setJobTitle(null);
    setDisplayName("");
    setAvatarUrl(null);
  };

  const setAvatar = (file) => {
    if (!file) return;
    // Client-side-only preview — see the AVATAR NOTE above.
    const objectUrl = URL.createObjectURL(file);
    setAvatarUrl(objectUrl);
  };

  const value = {
    isAuthenticated: role !== null,
    role,
    jobTitle,
    displayName,
    avatarUrl,
    setAvatar,
    signIn,
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
