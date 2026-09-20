import { Navigate } from "react-router-dom";
import { useMockAuth } from "../context/MockAuthContext";

export default function ProtectedRoute({ allowedRole, children }) {
  const { isAuthenticated, role, ready, mustChangePassword } = useMockAuth();

  if (!ready) {
    return null;
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  if (mustChangePassword) {
    return <Navigate to="/change-password" replace />;
  }

  if (role !== allowedRole) {
    return <Navigate to="/unauthorized" replace />;
  }

  return children;
}
