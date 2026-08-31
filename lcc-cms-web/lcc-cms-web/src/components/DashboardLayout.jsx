import { useRef } from "react";
import { NavLink } from "react-router-dom";
import { useMockAuth, ROLE_LABELS } from "../context/MockAuthContext";
import { PUBLIC_SITE_URL } from "../config";
import lccLogo from "../assets/lcc-logo.png";
import "./DashboardLayout.css";

/**
 * `navItems` accepts either a plain string (legacy — renders as an inert,
 * visibly-disabled placeholder, since that module isn't built yet) or an
 * object { label, path } once a real page exists for it. This lets each
 * dashboard's sidebar honestly distinguish "built, click me" from
 * "planned, not wired up yet" instead of every item looking equally dead.
 */
export default function DashboardLayout({ title, navItems = [], children }) {
  const { role, displayName, avatarUrl, setAvatar, signOut } = useMockAuth();
  const avatarInputRef = useRef(null);

  const handleSignOut = () => {
    signOut();
    window.location.href = PUBLIC_SITE_URL;
  };

  const handleAvatarClick = () => {
    avatarInputRef.current?.click();
  };

  const handleAvatarChange = (e) => {
    const file = e.target.files[0];
    if (file) setAvatar(file);
  };

  return (
    <div className="dash-shell">
      <aside className="dash-sidebar">
        <div className="dash-logo">
          <img src={lccLogo} alt="Lutheran Church College, Banz" className="dash-logo-img" />
          <span className="dash-logo-text">LCC-CMS</span>
        </div>
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

            return (
              <NavLink
                key={label}
                to={path}
                end
                className={({ isActive }) =>
                  `dash-nav-item${isActive ? " dash-nav-item-active" : ""}`
                }
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
          <h1>{title}</h1>
          <div className="dash-user">
            <div className="dash-user-info">
              <span className="dash-user-name">{displayName}</span>
              <span className="dash-user-role">{ROLE_LABELS[role]}</span>
            </div>

            <button
              type="button"
              className="dash-avatar"
              onClick={handleAvatarClick}
              title="Upload ID photo / avatar"
            >
              {avatarUrl ? (
                <img src={avatarUrl} alt="Your photo" className="dash-avatar-img" />
              ) : (
                <span className="dash-avatar-placeholder">＋</span>
              )}
            </button>
            <input
              ref={avatarInputRef}
              type="file"
              accept="image/jpeg,image/png"
              onChange={handleAvatarChange}
              className="dash-avatar-input"
            />

            <button className="dash-signout" onClick={handleSignOut}>
              Sign out
            </button>
          </div>
        </header>

        <main className="dash-content">{children}</main>
      </div>
    </div>
  );
}
