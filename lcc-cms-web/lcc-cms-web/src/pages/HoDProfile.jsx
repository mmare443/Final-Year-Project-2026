import StaffSelfProfile from "./StaffSelfProfile";
import { HOD_NAV } from "./hodNav";

export default function HoDProfile() {
  return <StaffSelfProfile title="My Profile" navItems={HOD_NAV} />;
}
