import { useState } from "react";
import { Link } from "react-router-dom";
import { API_ORIGIN } from "../api";
import { CONTACT } from "../config/contactConfig";
import CollegeContact from "../components/CollegeContact";
import lccLogo from "../assets/lcc-logo.png";
import "./Login.css";

export default function ForgotPassword() {
  const [email, setEmail] = useState("");
  const [error, setError] = useState(null);
  const [message, setMessage] = useState(null);
  const [resetLink, setResetLink] = useState(null);
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError(null);
    setMessage(null);
    setResetLink(null);
    setSubmitting(true);
    try {
      const res = await fetch(`${API_ORIGIN}/api/auth/forgot-password`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email }),
      });
      const text = await res.text();
      if (!res.ok) {
        throw new Error(text || `Request failed (${res.status}).`);
      }
      const body = text ? JSON.parse(text) : {};
      setMessage(
        body.message
          || "If an account exists for that email, a password reset link has been issued."
      );
      if (body.resetLink) setResetLink(body.resetLink);
    } catch (err) {
      setError(err.message || "Could not send a reset request.");
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
          <h1 className="login-heading">Forgot password</h1>
          <p className="login-intro">
            Enter your college email. If an account exists, a reset link is issued for 60 minutes.
          </p>
        </div>

        <form className="login-form" onSubmit={handleSubmit}>
          <label className="login-label" htmlFor="forgot-email">Email</label>
          <input
            id="forgot-email"
            type="email"
            name="email"
            autoComplete="username"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
          />
          {error && <p className="login-error">{error}</p>}
          {message && <p className="login-success">{message}</p>}
          {resetLink && (
            <p className="login-intro" style={{ textAlign: "left", margin: "8px 0 0" }}>
              Laboratory reset link:{" "}
              <Link to={`${new URL(resetLink, window.location.origin).pathname}${new URL(resetLink, window.location.origin).search}`}>
                Reset password
              </Link>
            </p>
          )}
          <button className="login-submit" type="submit" disabled={submitting}>
            {submitting ? "Sending…" : "Send reset link"}
          </button>
        </form>

        <Link to="/login" className="back-link">← Back to sign in</Link>
        <CollegeContact />
      </div>
    </div>
  );
}
