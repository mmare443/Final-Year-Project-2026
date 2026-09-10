import { useEffect, useState } from "react";
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

export default function ManagementPrincipalDashboard() {
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
    <DashboardLayout title="Management / Principal Dashboard" navItems={MANAGEMENT_NAV}>
      {apiError && <div className="records-error">{apiError}</div>}
      {isLoading && (
        <p style={{ color: "var(--text-light)", marginBottom: 18 }}>
          Loading institutional summary…
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

      <p style={{ marginTop: 24, color: "var(--text-light)", fontSize: 13 }}>
        Executive snapshot from enrolled students, staff, academic structure,
        active hostel allocations, and open welfare cases.
      </p>
    </DashboardLayout>
  );
}
