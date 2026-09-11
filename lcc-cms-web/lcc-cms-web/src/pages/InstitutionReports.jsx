import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import DashboardLayout from "../components/DashboardLayout";
import { API_ORIGIN, apiFetch } from "../api";
import { MANAGEMENT_NAV } from "./managementNav";
import "./StudentRecords.css";

const KPI = [
  { key: "students", label: "Students" },
  { key: "staff", label: "Staff" },
  { key: "programmes", label: "Programmes" },
  { key: "faculties", label: "Faculties" },
  { key: "activeAccommodation", label: "Active Accommodation" },
  { key: "openWelfareCases", label: "Open Welfare Cases" },
];

export default function InstitutionReports() {
  const [summary, setSummary] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [apiError, setApiError] = useState(null);

  useEffect(() => {
    (async () => {
      setIsLoading(true);
      try {
        const res = await apiFetch(`${API_ORIGIN}/api/reports/summary`);
        if (!res.ok) throw new Error(`API returned ${res.status}`);
        setSummary(await res.json());
        setApiError(null);
      } catch {
        setSummary(null);
        setApiError(
          "Couldn't reach the backend API. Make sure `dotnet run` is running on http://localhost:5000."
        );
      } finally {
        setIsLoading(false);
      }
    })();
  }, []);

  return (
    <DashboardLayout title="Institution Reports" navItems={MANAGEMENT_NAV}>
      {apiError && <div className="records-error">{apiError}</div>}
      {isLoading && (
        <p style={{ color: "var(--text-light)", marginBottom: 18 }}>
          Loading institution reports…
        </p>
      )}

      <div className="dash-card-grid">
        {KPI.map((item) => (
          <div className="dash-card" key={item.key}>
            <h3>{item.label}</h3>
            <div className="dash-card-value">
              {summary == null ? "—" : summary[item.key]}
            </div>
          </div>
        ))}
      </div>

      <p style={{ marginTop: 24, marginBottom: 14, color: "var(--text-light)", fontSize: 13 }}>
        College-wide totals from the same sources as Overview. Open Enrolment
        Analytics or Staff Overview for the detailed breakdowns.
      </p>

      <p style={{ display: "flex", gap: 18, flexWrap: "wrap" }}>
        <Link to="/management/enrolment" style={{ color: "var(--primary)", fontWeight: 700 }}>
          Enrolment Analytics
        </Link>
        <Link to="/management/staff" style={{ color: "var(--primary)", fontWeight: 700 }}>
          Staff Overview
        </Link>
      </p>
    </DashboardLayout>
  );
}
