import { useState } from "react";
import { Navigate, useNavigate } from "react-router-dom";
import { useMockAuth, ROLE_HOME } from "../context/MockAuthContext";
import { API_ORIGIN, apiFetch } from "../api";
import { CONTACT } from "../config/contactConfig";
import CollegeContact from "../components/CollegeContact";
import lccLogo from "../assets/lcc-logo.png";
import "./Login.css";

export default function ChangePassword() {
  const { isAuthenticated, ready, role, mustChangePassword, completePasswordChange, loadProfilePhoto, signOut } =
    useMockAuth();
  const navigate = useNavigate();
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState(null);
  const [submitting, setSubmitting] = useState(false);

  if (ready && !isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError(null);
    if (newPassword !== confirmPassword) {
      setError("New password and confirmation do not match.");
      return;
    }
    setSubmitting(true);
    try {
      const res = await apiFetch(`${API_ORIGIN}/api/account/change-password`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ currentPassword, newPassword, confirmPassword }),
      });
      const text = await res.text();
      if (!res.ok) {
        throw new Error(text || `Password change failed (${res.status}).`);
      }
      completePasswordChange();
      try {
        await loadProfilePhoto();
      } catch {
        /* photo is optional after first login */
      }
      navigate(ROLE_HOME[role] || "/login", { replace: true });
    } catch (err) {
      setError(err.message || "Password change failed.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="login-page">
      <div className="login-card">
        <div className="login-brand-block">
          <img src={lccLogo} alt={CONTACT.institution} className="login-hero-logo" />
          <p className="login-brand-mark">LCC-CMS</p>
          <h1 className="login-heading">Change password</h1>
          <p className="login-intro">
            {mustChangePassword
              ? "You must set a new password before using the College portal."
              : "Update your portal password."}
          </p>
        </div>

        <form className="login-form" onSubmit={handleSubmit}>
          <label className="login-label" htmlFor="current-password">Current password</label>
          <input
            id="current-password"
            type="password"
            autoComplete="current-password"
            value={currentPassword}
            onChange={(e) => setCurrentPassword(e.target.value)}
            required
          />

          <label className="login-label" htmlFor="new-password">New password</label>
          <input
            id="new-password"
            type="password"
            autoComplete="new-password"
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
            minLength={8}
            required
          />

          <label className="login-label" htmlFor="confirm-password">Confirm new password</label>
          <input
            id="confirm-password"
            type="password"
            autoComplete="new-password"
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            minLength={8}
            required
          />

          <p className="login-intro" style={{ marginTop: 8, marginBottom: 4, textAlign: "left" }}>
            At least 8 characters, with uppercase, lowercase, a number, and a special character.
            It cannot match your current or temporary password.
          </p>

          {error && <p className="login-error">{error}</p>}

          <button className="login-submit" type="submit" disabled={submitting}>
            {submitting ? "Saving…" : "Save new password"}
          </button>
        </form>

        <button type="button" className="back-link" onClick={signOut} style={{ border: "none", background: "none", cursor: "pointer" }}>
          Sign out
        </button>
        <CollegeContact />
      </div>
    </div>
  );
}
