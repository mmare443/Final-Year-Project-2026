import lccLogo from "../assets/lcc-logo.png";
import "./PageHeader.css";

/**
 * Shared page title row: LCC logo + title + optional subtitle.
 * Used by DashboardLayout and standalone pages (login, apply, 403).
 * Pass `brand` on public portal pages to show LCC-CMS next to the logo,
 * with the page title stacked underneath (same classes as dashboards).
 */
export default function PageHeader({ brand, title, subtitle }) {
  const stacked = Boolean(brand);
  const mark = stacked ? brand : title;

  return (
    <div className={stacked ? "page-header-stack" : undefined}>
      <div className="page-header">
        <img
          src={lccLogo}
          alt="Lutheran Church College"
          className="page-header-logo"
        />
        <div className="page-header-text">
          {stacked ? (
            <p className="page-header-title">{mark}</p>
          ) : (
            <h1 className="page-header-title">{mark}</h1>
          )}
          {!stacked && subtitle ? (
            <p className="page-header-subtitle">{subtitle}</p>
          ) : null}
        </div>
      </div>
      {stacked && title ? (
        <h1 className="page-header-title page-header-heading">{title}</h1>
      ) : null}
      {stacked && subtitle ? (
        <p className="page-header-subtitle">{subtitle}</p>
      ) : null}
    </div>
  );
}
