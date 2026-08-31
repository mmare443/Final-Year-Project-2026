import DashboardLayout from "../components/DashboardLayout";
import AdmissionsQueue from "../components/AdmissionsQueue";
import { useMockData } from "../context/MockDataContext";
import { useMockAuth, JOB_TITLES } from "../context/MockAuthContext";

/**
 * Job-title-based dashboard variants — UI only. Every job title here
 * authenticates with the same RegistrarAdmin role and hits the same
 * backend [Authorize(Policy = "RegistrarAdminOnly")] policy; this file
 * only decides which sections to SHOW, not what's permitted. See
 * MockAuthContext.jsx's header comment for the full reasoning.
 */

const NAV_BY_TITLE = {
  [JOB_TITLES.REGISTRAR]: [
    { label: "Overview", path: "/registrar" },
    { label: "Admissions", path: "/registrar" },
    { label: "Student Records", path: "/registrar/students" },
    { label: "Academic Structure", path: "/registrar/academic" },
    "Course Registration", "Staff Management", "Accommodation & Welfare",
    "System Administration", "Profile",
  ],
  [JOB_TITLES.DEAN_OF_STUDIES]: [
    { label: "Overview", path: "/registrar" },
    { label: "Academic Oversight", path: "/registrar/academic" },
    "HoD Reports",
    { label: "Student Records", path: "/registrar/students" },
    "Profile",
  ],
  [JOB_TITLES.ADMIN_OFFICER]: [
    { label: "Overview", path: "/registrar" },
    { label: "Admissions", path: "/registrar" },
    { label: "Student Records", path: "/registrar/students" },
    "Staff Management", "System Administration", "Profile",
  ],
  [JOB_TITLES.ACCOUNTS]: [
    { label: "Overview", path: "/registrar" },
    "Finance", "Profile",
  ],
};


export default function RegistrarAdminDashboard() {
  const { applications, STATUS } = useMockData();
  const { jobTitle } = useMockAuth();
  const effectiveTitle = jobTitle || JOB_TITLES.REGISTRAR;

  const pendingCount = applications.filter((a) => a.status === STATUS.APPLIED).length;
  const enrolledCount = applications.filter((a) => a.status === STATUS.APPROVED).length;

  const showAdmissions =
    effectiveTitle === JOB_TITLES.REGISTRAR || effectiveTitle === JOB_TITLES.ADMIN_OFFICER;
  const showDeanView = effectiveTitle === JOB_TITLES.DEAN_OF_STUDIES;
  const showAccountsView = effectiveTitle === JOB_TITLES.ACCOUNTS;

  return (
    <DashboardLayout
      title={`Registrar / Admin Dashboard — ${effectiveTitle}`}
      navItems={NAV_BY_TITLE[effectiveTitle]}
    >
      {showAdmissions && (
        <>
          <div className="dash-card-grid">
            <div className="dash-card">
              <h3>Pending Admissions</h3>
              <div className="dash-card-value">{pendingCount}</div>
            </div>
            <div className="dash-card">
              <h3>Total Enrolled Students</h3>
              <div className="dash-card-value">{enrolledCount}</div>
            </div>
            <div className="dash-card">
              <h3>Active Staff</h3>
              <div className="dash-card-value">—</div>
            </div>
            <div className="dash-card">
              <h3>Open Support Tickets</h3>
              <div className="dash-card-value">—</div>
            </div>
          </div>

          <h2 style={{ margin: "28px 0 14px", fontSize: 16, color: "var(--secondary)" }}>
            M1 — Admissions Queue
          </h2>
          <AdmissionsQueue />

          <p style={{ marginTop: 24, color: "var(--text-light)", fontSize: 13 }}>
            Admissions is live (mock data) — the two remaining cards stay
            placeholder until Staff Management (M9) and a support-ticket
            module exist. Programme list here is a placeholder for Academic
            Structure (M3), not yet built.
          </p>
        </>
      )}

      {showDeanView && (
        <>
          <div className="dash-card-grid">
            <div className="dash-card">
              <h3>Departments</h3>
              <div className="dash-card-value">—</div>
            </div>
            <div className="dash-card">
              <h3>HoDs Reporting</h3>
              <div className="dash-card-value">—</div>
            </div>
            <div className="dash-card">
              <h3>Pending Academic Approvals</h3>
              <div className="dash-card-value">—</div>
            </div>
          </div>
          <p style={{ marginTop: 24, color: "var(--text-light)", fontSize: 13 }}>
            Dean of Studies view — Academic Oversight and HoD Reports
            connect once Academic Structure (M3) and Course Registration
            (M4) are built. Same RegistrarAdmin permissions as every other
            job title on this role; this view just surfaces the sections
            relevant to academic oversight.
          </p>
        </>
      )}

      {showAccountsView && (
        <div className="admissions-error">
          Finance is confirmed out of scope for LCC-CMS (see Blueprint
          Rev 5) — it remains a separate process outside the system, and is
          a candidate for future integration. This view exists so the
          Accounts office has its own space, but there's no financial data
          or module here to connect to.
        </div>
      )}

      {!showAdmissions && !showDeanView && !showAccountsView && (
        <p style={{ color: "var(--text-light)", fontSize: 13 }}>
          Overview for {effectiveTitle}.
        </p>
      )}
    </DashboardLayout>
  );
}
