import { useNavigate } from "react-router-dom";
import { useMockAuth } from "../context/MockAuthContext";

export default function Unauthorized() {
  const { signOut } = useMockAuth();
  const navigate = useNavigate();

  const handleBack = () => {
    signOut();
    navigate("/login");
  };

  return (
    <div style={{
      minHeight: "100vh", display: "flex", flexDirection: "column",
      alignItems: "center", justifyContent: "center", gap: 12,
      fontFamily: "var(--font-main)", background: "var(--background)",
      textAlign: "center", padding: 24,
    }}>
      <h1 style={{ color: "var(--secondary)" }}>403 — Not Authorized</h1>
      <p style={{ color: "var(--text-light)", maxWidth: 380 }}>
        Your role doesn't have access to this portal. This mirrors the real
        backend policy check that will reject this the same way once the
        Web API is live.
      </p>
      <button
        onClick={handleBack}
        style={{
          marginTop: 10, background: "var(--primary)", color: "#fff",
          border: 0, padding: "10px 20px", borderRadius: 8, fontWeight: 600,
        }}
      >
        Back to sign in
      </button>
    </div>
  );
}
