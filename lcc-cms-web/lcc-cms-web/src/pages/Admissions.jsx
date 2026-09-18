import DashboardLayout from "../components/DashboardLayout";
import AdmissionsQueue from "../components/AdmissionsQueue";
import { REGISTRAR_NAV } from "./registrarNav";

export default function Admissions() {
  return (
    <DashboardLayout title="Admissions" navItems={REGISTRAR_NAV}>
      <AdmissionsQueue />
    </DashboardLayout>
  );
}
