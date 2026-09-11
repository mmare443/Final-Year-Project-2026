import lccLogo from "../assets/lcc-logo.png";
import "./PageHeader.css";

/**
 * Shared page title row: LCC logo + title + optional subtitle.
 * Used by DashboardLayout and standalone pages (login, apply, 403).
 */
export default function PageHeader({ title, subtitle }) {
  return (
    <div className="page-header">
      <img
        src={lccLogo}
        alt="Lutheran Church College"
        className="page-header-logo"
      />
      <div className="page-header-text">
        <h1 className="page-header-title">{title}</h1>
        {subtitle ? (
          <p className="page-header-subtitle">{subtitle}</p>
        ) : null}
      </div>
    </div>
  );
}
