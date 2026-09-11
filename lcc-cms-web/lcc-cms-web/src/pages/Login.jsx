import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useMockAuth, ROLES } from "../context/MockAuthContext";
import { PUBLIC_SITE_URL } from "../config";
import PageHeader from "../components/PageHeader";
import "./Login.css";

const ROLE_ROUTES = {
  [ROLES.STUDENT]: "/student",
  [ROLES.LECTURER]: "/lecturer",
  [ROLES.HOD]: "/hod",
  [ROLES.REGISTRAR_ADMIN]: "/registrar",
  [ROLES.MANAGEMENT_PRINCIPAL]: "/management",
};

export default function Login() {
  const { login } = useMockAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState(null);
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      const role = await login(email, password);
      navigate(ROLE_ROUTES[role] || "/unauthorized");
    } catch (err) {
      setError(err.message || "Sign in failed.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="login-page">
      <div className="login-card">
        <PageHeader brand="LCC-CMS" title="Sign In" subtitle="College Portal" />
        <p className="login-intro">
          Sign in with your LCC email and password to open the College portal.
        </p>

        <form className="login-form" onSubmit={handleSubmit}>
          <label className="login-label" htmlFor="login-email">Email</label>
          <input
            id="login-email"
            type="email"
            name="email"
            autoComplete="username"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
          />

          <label className="login-label" htmlFor="login-password">Password</label>
          <input
            id="login-password"
            type="password"
            name="password"
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
          />

          {error && <p className="login-error">{error}</p>}

          <button className="login-submit" type="submit" disabled={submitting}>
            {submitting ? "Signing in…" : "Sign in"}
          </button>
        </form>

        <a href={PUBLIC_SITE_URL} className="back-link">← Return to LCC website</a>
      </div>
    </div>
  );
}
