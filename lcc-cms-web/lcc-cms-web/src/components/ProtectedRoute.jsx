import { Navigate } from "react-router-dom";
import { useMockAuth } from "../context/MockAuthContext";

/**
 * Guards a dashboard route. `allowedRole` mirrors what the real
 * [Authorize(Policy = "...Only")] policies will enforce on the backend
 * (see Backend & Frontend Scaffold Guide, Step 4) — this is the same
 * shape, just checked against mock state instead of a JWT for now.
 */
export default function ProtectedRoute({ allowedRole, children }) {
  const { isAuthenticated, role } = useMockAuth();

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  if (role !== allowedRole) {
    return <Navigate to="/unauthorized" replace />;
  }

  return children;
}
