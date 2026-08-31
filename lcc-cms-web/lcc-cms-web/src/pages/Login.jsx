import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useMockAuth, ROLES, ROLE_LABELS, JOB_TITLES } from "../context/MockAuthContext";
import { PUBLIC_SITE_URL } from "../config";
import lccLogo from "../assets/lcc-logo.png";
import "./Login.css";

const ROLE_ROUTES = {
  [ROLES.STUDENT]: "/student",
  [ROLES.LECTURER]: "/lecturer",
  [ROLES.HOD]: "/hod",
  [ROLES.REGISTRAR_ADMIN]: "/registrar",
  [ROLES.MANAGEMENT_PRINCIPAL]: "/management",
};

export default function Login() {
  const { signIn } = useMockAuth();
  const navigate = useNavigate();
  const [expandedRole, setExpandedRole] = useState(null);

  const handleSelectRole = (role) => {
    // Registrar/Admin has job-title sub-choices (UI only, see
    // MockAuthContext's header comment) — expand instead of navigating.
    if (role === ROLES.REGISTRAR_ADMIN) {
      setExpandedRole(expandedRole === role ? null : role);
      return;
    }
    signIn(role);
    navigate(ROLE_ROUTES[role]);
  };

  const handleSelectJobTitle = (jobTitle) => {
    signIn(ROLES.REGISTRAR_ADMIN, jobTitle);
    navigate(ROLE_ROUTES[ROLES.REGISTRAR_ADMIN]);
  };

  return (
    <div className="login-page">
      <div className="login-card">
        <img src={lccLogo} alt="Lutheran Church College, Banz" className="login-logo-img" />
        <h1>Welcome Back</h1>
        <p className="login-intro">
          Sign in with your LCC account to access the appropriate College portal.
        </p>

        <div className="mock-banner">
          ⚠ Development mode — real Microsoft sign-in isn't wired up yet.
          Pick a role below to preview that portal.
        </div>

        <div className="role-list">
          {Object.values(ROLES).map((role) => (
            <div key={role}>
              <button
                className="role-btn"
                onClick={() => handleSelectRole(role)}
              >
                Continue as {ROLE_LABELS[role]}
                {role === ROLES.REGISTRAR_ADMIN && (
                  <span className="role-btn-hint">
                    {expandedRole === role ? "▲" : "▼"}
                  </span>
                )}
              </button>

              {role === ROLES.REGISTRAR_ADMIN && expandedRole === role && (
                <div className="job-title-list">
                  {Object.values(JOB_TITLES).map((title) => (
                    <button
                      key={title}
                      className="job-title-btn"
                      onClick={() => handleSelectJobTitle(title)}
                    >
                      {title}
                    </button>
                  ))}
                </div>
              )}
            </div>
          ))}
        </div>

        <a href={PUBLIC_SITE_URL} className="back-link">← Return to LCC website</a>
      </div>
    </div>
  );
}
