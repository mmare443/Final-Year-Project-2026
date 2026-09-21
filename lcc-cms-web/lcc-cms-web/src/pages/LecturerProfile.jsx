import StaffSelfProfile from "./StaffSelfProfile";
import { LECTURER_NAV } from "./Attendance";

export default function LecturerProfile() {
  return <StaffSelfProfile title="My Profile" navItems={LECTURER_NAV} />;
}
