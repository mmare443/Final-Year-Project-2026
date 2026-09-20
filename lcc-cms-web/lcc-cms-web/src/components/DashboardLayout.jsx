import { NavLink, useLocation } from "react-router-dom";
import { useMockAuth, ROLE_LABELS, ROLES, avatarInitials } from "../context/MockAuthContext";
import { PUBLIC_SITE_URL } from "../config";
import { CONTACT } from "../config/contactConfig";
import { isNavItemActive, resolveActiveNavPath } from "../nav/isNavActive";
import lccLogo from "../assets/lcc-logo.png";
import "./DashboardLayout.css";

function portalLabel(role) {
  if (role === ROLES.STUDENT) return "Student Portal";
  if (role === ROLES.LECTURER) return "Lecturer Portal";
  if (role) return "Staff Portal";
  return null;
}

export default function DashboardLayout({ title, subtitle, navItems = [], children }) {
  const {
    role,
    displayName,
    email,
    studentNumber,
    staffNumber,
    avatarUrl,
    signOut,
  } = useMockAuth();
  const { pathname } = useLocation();
  const activePath = resolveActiveNavPath(navItems, pathname);
  const portal = portalLabel(role);
  const identityNumber = studentNumber || staffNumber || "";
  const showPageTitle = Boolean(title) && title !== displayName;

  const handleSignOut = () => {
    signOut();
    window.location.href = PUBLIC_SITE_URL;
  };

  return (
    <div className="dash-shell">
      <aside className="dash-sidebar">
        <div className="dash-logo">
          <img src={lccLogo} alt={CONTACT.institution} className="dash-logo-img" />
          <span className="dash-logo-text">LCC-CMS</span>
        </div>
        {portal && <p className="dash-portal-name">{portal}</p>}
        <nav className="dash-nav">
          {navItems.map((item) => {
            const label = typeof item === "string" ? item : item.label;
            const path = typeof item === "string" ? null : item.path;

            if (!path) {
              return (
                <span key={label} className="dash-nav-item dash-nav-item-disabled" title="Not built yet">
                  {label}
                </span>
              );
            }

            const isActive = isNavItemActive(path, activePath);
            return (
              <NavLink
                key={`${label}:${path}`}
                to={path}
                end
                className={`dash-nav-item${isActive ? " dash-nav-item-active" : ""}`}
                aria-current={isActive ? "page" : undefined}
              >
                {label}
              </NavLink>
            );
          })}
        </nav>
        <a href={PUBLIC_SITE_URL} className="dash-site-link">
          ← LCC Website
        </a>
      </aside>

      <div className="dash-main">
        <header className="dash-header">
          <div className="dash-identity">
            <div className="dash-identity-photo" aria-hidden={!avatarUrl}>
              {avatarUrl ? (
                <img src={avatarUrl} alt="" className="dash-identity-img" />
              ) : (
                <span className="dash-identity-fallback">
                  {avatarInitials(displayName) || "?"}
                </span>
              )}
            </div>
            <div className="dash-identity-text">
              <p className="dash-identity-name">{displayName || "—"}</p>
              {identityNumber ? (
                <p className="dash-identity-id">{identityNumber}</p>
              ) : null}
            </div>
          </div>

          <div className="dash-user">
            <div className="dash-user-info">
              <span className="dash-user-email">{email || displayName}</span>
              <span className="dash-user-role">{ROLE_LABELS[role] || role}</span>
            </div>
            <button className="dash-signout" onClick={handleSignOut}>
              Sign Out
            </button>
          </div>
        </header>

        <main className="dash-content">
          {showPageTitle ? (
            <div className="dash-page-heading">
              <h1 className="dash-page-title">{title}</h1>
              {subtitle ? <p className="dash-page-subtitle">{subtitle}</p> : null}
            </div>
          ) : null}
          {children}
        </main>
        <p className="dash-contact-footer">
          {CONTACT.institution} · {CONTACT.phone} · {CONTACT.primaryEmail}
        </p>
      </div>
    </div>
  );
}
