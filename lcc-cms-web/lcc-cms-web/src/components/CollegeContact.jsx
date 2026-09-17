import { CONTACT } from "../config/contactConfig";

export default function CollegeContact({ variant = "brief" }) {
  if (variant === "apply") {
    return (
      <div className="apply-note">
        <p>
          Application contact: {CONTACT.applicationContact}{" "}
          (<a href={`mailto:${CONTACT.applicationEmail}`}>{CONTACT.applicationEmail}</a>).
        </p>
        <p>
          K30 application fee — deposit to {CONTACT.bankAccountName}, account{" "}
          {CONTACT.bankAccountNumber}, {CONTACT.bank}.
        </p>
        <p>
          Enquiries: {CONTACT.phone} · WhatsApp {CONTACT.whatsApp} ·{" "}
          <a href={`mailto:${CONTACT.primaryEmail}`}>{CONTACT.primaryEmail}</a>
        </p>
        <p>{CONTACT.postalOneLine}</p>
      </div>
    );
  }

  return (
    <p className="login-intro">
      {CONTACT.institution} · {CONTACT.phone} ·{" "}
      <a href={`mailto:${CONTACT.primaryEmail}`}>{CONTACT.primaryEmail}</a>
    </p>
  );
}
