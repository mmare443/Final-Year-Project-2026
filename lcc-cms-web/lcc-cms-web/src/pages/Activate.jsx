import { useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { API_ORIGIN } from "../api";
import PageHeader from "../components/PageHeader";
import "./Login.css";

export default function Activate() {
  const [params] = useSearchParams();
  const tokenFromLink = params.get("token") || "";
  const [token, setToken] = useState(tokenFromLink);
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState(null);
  const [success, setSuccess] = useState(null);
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError(null);
    setSuccess(null);
    if (password !== confirmPassword) {
      setError("Password and confirmation do not match.");
      return;
    }
    setSubmitting(true);
    try {
      const res = await fetch(`${API_ORIGIN}/api/auth/activate`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ token, password, confirmPassword }),
      });
      const text = await res.text();
      if (!res.ok) {
        throw new Error(text || `Activation failed (${res.status}).`);
      }
      const body = text ? JSON.parse(text) : {};
      setSuccess(body.message || "Password set. You can now sign in.");
    } catch (err) {
      setError(err.message || "Activation failed.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="login-page">
      <div className="login-card">
        <PageHeader brand="LCC-CMS" title="Activate Account" subtitle="College Portal" />
        <p className="login-intro">
          Set a password using the activation token sent after your admission was approved.
        </p>

        {success ? (
          <p className="login-intro">
            {success}{" "}
            <Link to="/login" className="back-link">
              Sign in
            </Link>
          </p>
        ) : (
          <form className="login-form" onSubmit={handleSubmit}>
            <label className="login-label" htmlFor="activate-token">Activation token</label>
            <input
              id="activate-token"
              type="text"
              name="token"
              autoComplete="off"
              value={token}
              onChange={(e) => setToken(e.target.value)}
              required
            />

            <label className="login-label" htmlFor="activate-password">Password</label>
            <input
              id="activate-password"
              type="password"
              name="password"
              autoComplete="new-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              minLength={8}
              required
            />

            <label className="login-label" htmlFor="activate-confirm">Confirm password</label>
            <input
              id="activate-confirm"
              type="password"
              name="confirmPassword"
              autoComplete="new-password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              minLength={8}
              required
            />

            {error && <p className="login-error">{error}</p>}

            <button className="login-submit" type="submit" disabled={submitting}>
              {submitting ? "Activating…" : "Set password"}
            </button>
          </form>
        )}

        <Link to="/login" className="back-link">← Sign in</Link>
      </div>
    </div>
  );
}
