import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useMockAuth, ROLE_HOME } from "../context/MockAuthContext";
import { PUBLIC_SITE_URL } from "../config";
import { CONTACT } from "../config/contactConfig";
import CollegeContact from "../components/CollegeContact";
import lccLogo from "../assets/lcc-logo.png";
import "./Login.css";

const ROLE_ROUTES = ROLE_HOME;

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
      const result = await login(email, password);
      if (result.mustChangePassword !== false) {
        navigate("/change-password", { replace: true });
      } else {
        navigate(ROLE_ROUTES[result.role] || "/unauthorized");
      }
    } catch (err) {
      setError(err.message || "Sign in failed.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="login-page">
      <div className="login-card">
        <div className="login-brand-block">
          <img
            src={lccLogo}
            alt={CONTACT.institution}
            className="login-hero-logo"
          />
          <p className="login-brand-mark">LCC-CMS</p>
          <h1 className="login-heading">Welcome Back</h1>
          <p className="login-intro">
            Sign in with your LCC account to access the appropriate College portal.
          </p>
        </div>

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

        <Link to="/forgot-password" className="forgot-link">Forgot Password?</Link>

        <a href={PUBLIC_SITE_URL} className="back-link">← Return to LCC website</a>
        <CollegeContact />
      </div>
    </div>
  );
}
