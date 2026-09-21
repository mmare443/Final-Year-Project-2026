import { useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { API_ORIGIN } from "../api";
import { CONTACT } from "../config/contactConfig";
import CollegeContact from "../components/CollegeContact";
import lccLogo from "../assets/lcc-logo.png";
import "./Login.css";

export default function ResetPassword() {
  const [params] = useSearchParams();
  const tokenFromLink = params.get("token") || "";
  const [token, setToken] = useState(tokenFromLink);
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState(null);
  const [success, setSuccess] = useState(null);
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError(null);
    setSuccess(null);
    if (newPassword !== confirmPassword) {
      setError("New password and confirmation do not match.");
      return;
    }
    setSubmitting(true);
    try {
      const res = await fetch(`${API_ORIGIN}/api/auth/reset-password`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ token, newPassword, confirmPassword }),
      });
      const text = await res.text();
      if (!res.ok) {
        throw new Error(text || `Reset failed (${res.status}).`);
      }
      const body = text ? JSON.parse(text) : {};
      setSuccess(body.message || "Password updated. You can now sign in.");
    } catch (err) {
      setError(err.message || "Reset failed.");
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
          <h1 className="login-heading">Reset password</h1>
          <p className="login-intro">
            Choose a new password. The reset link can be used once and expires after 60 minutes.
          </p>
        </div>

        {success ? (
          <p className="login-success">
            {success}{" "}
            <Link to="/login" className="back-link">Sign in</Link>
          </p>
        ) : (
          <form className="login-form" onSubmit={handleSubmit}>
            {!tokenFromLink && (
              <>
                <label className="login-label" htmlFor="reset-token">Reset token</label>
                <input
                  id="reset-token"
                  type="text"
                  name="token"
                  autoComplete="off"
                  value={token}
                  onChange={(e) => setToken(e.target.value)}
                  required
                />
              </>
            )}

            <label className="login-label" htmlFor="reset-password">New password</label>
            <input
              id="reset-password"
              type="password"
              autoComplete="new-password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              minLength={8}
              required
            />

            <label className="login-label" htmlFor="reset-confirm">Confirm new password</label>
            <input
              id="reset-confirm"
              type="password"
              autoComplete="new-password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              minLength={8}
              required
            />

            <p className="login-intro" style={{ marginTop: 8, marginBottom: 4, textAlign: "left" }}>
              At least 8 characters, with uppercase, lowercase, a number, and a special character.
              It cannot be the temporary laboratory password.
            </p>

            {error && <p className="login-error">{error}</p>}

            <button className="login-submit" type="submit" disabled={submitting}>
              {submitting ? "Saving…" : "Reset password"}
            </button>
          </form>
        )}

        <Link to="/login" className="back-link">← Back to sign in</Link>
        <CollegeContact />
      </div>
    </div>
  );
}
