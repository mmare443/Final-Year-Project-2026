import { useEffect, useState } from "react";
import DashboardLayout from "../components/DashboardLayout";
import { Briefcase, Building2, UserCircle } from "../components/ProfileIcons";
import { API_ORIGIN, apiFetch } from "../api";
import { HOD_NAV } from "./hodNav";
import "./StudentRecords.css";
import "./ManagementProfile.css";

function displayOrDash(value) {
  if (value == null || String(value).trim() === "") return "—";
  return value;
}

function roleLabel(me) {
  return displayOrDash(me.roleSql || me.role);
}

function emailInitial(email) {
  const local = String(email || "").trim();
  if (!local) return "";
  return local.charAt(0).toUpperCase();
}

export default function HoDProfile() {
  const [me, setMe] = useState(null);
  const [staff, setStaff] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [apiError, setApiError] = useState(null);

  useEffect(() => {
    (async () => {
      setIsLoading(true);
      try {
        const res = await apiFetch(`${API_ORIGIN}/api/me`);
        if (!res.ok) throw new Error(`API returned ${res.status}`);
        const record = await res.json();
        setMe(record);

        if (record.staffId) {
          const staffRes = await apiFetch(`${API_ORIGIN}/api/staff/${record.staffId}`);
          if (staffRes.ok) {
            setStaff(await staffRes.json());
          }
        }

        setApiError(null);
      } catch {
        setMe(null);
        setStaff(null);
        setApiError(
          "Couldn't reach the backend API. Make sure `dotnet run` is running on http://localhost:5000."
        );
      } finally {
        setIsLoading(false);
      }
    })();
  }, []);

  const initial = emailInitial(me?.email);

  return (
    <DashboardLayout title="HoD Profile" navItems={HOD_NAV}>
      {apiError && <div className="records-error">{apiError}</div>}
      {isLoading && (
        <p style={{ color: "var(--text-light)", marginBottom: 18 }}>
          Loading profile…
        </p>
      )}

      {!isLoading && me && (
        <>
          <div className="profile-avatar">
            <div className="profile-avatar-circle" aria-hidden="true">
              {initial ? (
                <span className="profile-avatar-initial">{initial}</span>
              ) : (
                <UserCircle className="profile-avatar-icon" />
              )}
            </div>
            <p className="profile-avatar-caption">{displayOrDash(me.email)}</p>
          </div>

          <div className="dash-card-grid">
            <section className="dash-card">
              <h3 className="profile-card-title">
                <UserCircle aria-hidden="true" />
                <span>Account Information</span>
              </h3>
              <dl className="profile-card-fields">
                <div>
                  <dt>Email</dt>
                  <dd>{displayOrDash(me.email)}</dd>
                </div>
                <div>
                  <dt>Role</dt>
                  <dd>{roleLabel(me)}</dd>
                </div>
              </dl>
            </section>

            <section className="dash-card">
              <h3 className="profile-card-title">
                <Briefcase aria-hidden="true" />
                <span>Employment Information</span>
              </h3>
              <dl className="profile-card-fields">
                <div>
                  <dt>Job Title</dt>
                  <dd>{displayOrDash(me.jobTitle || staff?.jobTitle)}</dd>
                </div>
              </dl>
            </section>

            <section className="dash-card">
              <h3 className="profile-card-title">
                <Building2 aria-hidden="true" />
                <span>Organisation Information</span>
              </h3>
              <dl className="profile-card-fields">
                <div>
                  <dt>Department</dt>
                  <dd>{displayOrDash(staff?.departmentName)}</dd>
                </div>
                <div>
                  <dt>Faculty</dt>
                  <dd>{displayOrDash(staff?.facultyName)}</dd>
                </div>
              </dl>
            </section>
          </div>
        </>
      )}
    </DashboardLayout>
  );
}
