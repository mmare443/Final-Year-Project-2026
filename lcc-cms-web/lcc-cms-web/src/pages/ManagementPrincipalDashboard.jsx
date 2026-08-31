import DashboardLayout from "../components/DashboardLayout";

const NAV = [
  { label: "Overview", path: "/management" },
  "Institution Reports", "Enrolment Analytics", "Staff Overview", "Announcements", "Profile",
];

export default function ManagementPrincipalDashboard() {
  return (
    <DashboardLayout title="Management / Principal Dashboard" navItems={NAV}>
      <div className="dash-card-grid">
        <div className="dash-card">
          <h3>Total Enrolment</h3>
          <div className="dash-card-value">—</div>
        </div>
        <div className="dash-card">
          <h3>Faculties</h3>
          <div className="dash-card-value">—</div>
        </div>
        <div className="dash-card">
          <h3>Academic Staff</h3>
          <div className="dash-card-value">—</div>
        </div>
        <div className="dash-card">
          <h3>Programmes Offered</h3>
          <div className="dash-card-value">—</div>
        </div>
      </div>
      <p style={{ marginTop: 24, color: "var(--text-light)", fontSize: 13 }}>
        Placeholder data — wire up to the Web API's Reporting &amp; Analytics
        module endpoints once the backend is live.
      </p>
    </DashboardLayout>
  );
}
