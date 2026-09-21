import StaffSelfProfile from "./StaffSelfProfile";
import { MANAGEMENT_NAV } from "./managementNav";
import principalPhoto from "../assets/lccpr.jpg";

export default function ManagementProfile() {
  return (
    <StaffSelfProfile
      title="My Profile"
      navItems={MANAGEMENT_NAV}
      fallbackPhoto={principalPhoto}
    />
  );
}
