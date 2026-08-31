import { Routes, Route, Navigate } from "react-router-dom";
import { MockAuthProvider, ROLES } from "./context/MockAuthContext";
import { MockDataProvider } from "./context/MockDataContext";
import { StudentsProvider } from "./context/StudentsContext";
import { AcademicStructureProvider } from "./context/AcademicStructureContext";
import { RegistrationsProvider } from "./context/RegistrationsContext";
import { AttendanceProvider } from "./context/AttendanceContext";
import { LearningProvider } from "./context/LearningContext";
import ProtectedRoute from "./components/ProtectedRoute";

import Login from "./pages/Login";
import Apply from "./pages/Apply";
import Unauthorized from "./pages/Unauthorized";
import StudentDashboard from "./pages/StudentDashboard";
import StudentProfile from "./pages/StudentProfile";
import LecturerDashboard from "./pages/LecturerDashboard";
import HoDDashboard from "./pages/HoDDashboard";
import RegistrarAdminDashboard from "./pages/RegistrarAdminDashboard";
import StudentRecords from "./pages/StudentRecords";
import AcademicStructure from "./pages/AcademicStructure";
import ManagementPrincipalDashboard from "./pages/ManagementPrincipalDashboard";
import Attendance from "./pages/Attendance";
import Learning from "./pages/Learning";

function App() {
  return (
    <MockDataProvider>
      <StudentsProvider>
        <AcademicStructureProvider>
          <RegistrationsProvider>
            <AttendanceProvider>
              <LearningProvider>
              <MockAuthProvider>
                <Routes>
                  <Route path="/" element={<Navigate to="/login" replace />} />
                  <Route path="/login" element={<Login />} />
                  <Route path="/apply" element={<Apply />} />
                  <Route path="/unauthorized" element={<Unauthorized />} />

                  <Route
                    path="/student"
                    element={
                      <ProtectedRoute allowedRole={ROLES.STUDENT}>
                        <StudentDashboard />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="/student/profile"
                    element={
                      <ProtectedRoute allowedRole={ROLES.STUDENT}>
                        <StudentProfile />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="/student/attendance"
                    element={
                      <ProtectedRoute allowedRole={ROLES.STUDENT}>
                        <Attendance />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="/student/assignments"
                    element={
                      <ProtectedRoute allowedRole={ROLES.STUDENT}>
                        <Learning />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="/lecturer"
                    element={
                      <ProtectedRoute allowedRole={ROLES.LECTURER}>
                        <LecturerDashboard />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="/lecturer/attendance"
                    element={
                      <ProtectedRoute allowedRole={ROLES.LECTURER}>
                        <Attendance />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="/lecturer/assignments"
                    element={
                      <ProtectedRoute allowedRole={ROLES.LECTURER}>
                        <Learning />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="/hod"
                    element={
                      <ProtectedRoute allowedRole={ROLES.HOD}>
                        <HoDDashboard />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="/hod/attendance"
                    element={
                      <ProtectedRoute allowedRole={ROLES.HOD}>
                        <Attendance />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="/registrar"
                    element={
                      <ProtectedRoute allowedRole={ROLES.REGISTRAR_ADMIN}>
                        <RegistrarAdminDashboard />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="/registrar/students"
                    element={
                      <ProtectedRoute allowedRole={ROLES.REGISTRAR_ADMIN}>
                        <StudentRecords />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="/registrar/academic"
                    element={
                      <ProtectedRoute allowedRole={ROLES.REGISTRAR_ADMIN}>
                        <AcademicStructure />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="/management"
                    element={
                      <ProtectedRoute allowedRole={ROLES.MANAGEMENT_PRINCIPAL}>
                        <ManagementPrincipalDashboard />
                      </ProtectedRoute>
                    }
                  />

                  <Route path="*" element={<Navigate to="/login" replace />} />
                </Routes>
              </MockAuthProvider>
              </LearningProvider>
            </AttendanceProvider>
          </RegistrationsProvider>
        </AcademicStructureProvider>
      </StudentsProvider>
    </MockDataProvider>
  );
}

export default App;
