import StaffSelfProfile from "./StaffSelfProfile";
import { REGISTRAR_NAV } from "./registrarNav";

export default function RegistrarProfile() {
  return <StaffSelfProfile title="My Profile" navItems={REGISTRAR_NAV} />;
}
