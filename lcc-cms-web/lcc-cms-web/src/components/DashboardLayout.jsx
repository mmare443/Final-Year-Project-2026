import { useRef } from "react";
import { NavLink, useLocation } from "react-router-dom";
import { useMockAuth, ROLE_LABELS } from "../context/MockAuthContext";
import { PUBLIC_SITE_URL } from "../config";
import { CONTACT } from "../config/contactConfig";
import { isNavItemActive, resolveActiveNavPath } from "../nav/isNavActive";
import lccLogo from "../assets/lcc-logo.png";
import PageHeader from "./PageHeader";
import "./DashboardLayout.css";

/**
 * `navItems` accepts either a plain string (legacy — renders as an inert,
 * visibly-disabled placeholder, since that module isn't built yet) or an
 * object { label, path } once a real page exists for it. This lets each
 * dashboard's sidebar honestly distinguish "built, click me" from
 * "planned, not wired up yet" instead of every item looking equally dead.
 */
export default function DashboardLayout({ title, subtitle, navItems = [], children }) {
  const { role, displayName, avatarUrl, setAvatar, signOut } = useMockAuth();
  const { pathname } = useLocation();
  const activePath = resolveActiveNavPath(navItems, pathname);
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
          <img src={lccLogo} alt={CONTACT.institution} className="dash-logo-img" />
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
          <PageHeader title={title} subtitle={subtitle} />
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
        <p className="dash-contact-footer">
          {CONTACT.institution} · {CONTACT.phone} · {CONTACT.primaryEmail}
        </p>
      </div>
    </div>
  );
}
