import StaffSelfProfile from "./StaffSelfProfile";
import { MANAGEMENT_NAV } from "./managementNav";

export default function ManagementProfile() {
  return (
    <StaffSelfProfile
      title="My Profile"
      navItems={MANAGEMENT_NAV}
    />
  );
}
