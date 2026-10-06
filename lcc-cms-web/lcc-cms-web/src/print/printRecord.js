import { CONTACT } from "../config/contactConfig";
import logoUrl from "../assets/lcc-logo.png";
import printCss from "./recordPrint.css?raw";
import {
  displayUrlToDataUrl,
  fetchAuthorizedPhotoDataUrl,
  ownProfilePhotoRequestUrl,
  userProfilePhotoRequestUrl,
} from "../profilePhoto";

export function escapePrintHtml(value) {
  return String(value ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

export function printedAtLabel(date = new Date()) {
  return date.toLocaleString("en-PG", {
    day: "numeric",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

export async function fetchUserPhotoDataUrl(userId) {
  if (userId == null || userId === "") return null;
  return fetchAuthorizedPhotoDataUrl(userProfilePhotoRequestUrl(userId));
}

export async function fetchOwnPhotoDataUrl() {
  return fetchAuthorizedPhotoDataUrl(ownProfilePhotoRequestUrl());
}

export function buildRecordPrintHtml({
  documentTitle,
  fields,
  extraTitle,
  extraText,
  photoDataUrl,
  printedBy,
  printedAt,
}) {
  const rows = (fields || [])
    .map(([label, value]) => (
      `<tr><th>${escapePrintHtml(label)}</th><td>${escapePrintHtml(value || "—")}</td></tr>`
    ))
    .join("");

  const photo = photoDataUrl
    ? `<img class="print-photo" src="${escapePrintHtml(photoDataUrl)}" alt="Profile photo">`
    : `<div class="print-photo-fallback">No photo</div>`;

  const extra = extraTitle
    ? `<h2 class="print-section">${escapePrintHtml(extraTitle)}</h2>
       <div class="print-extra">${escapePrintHtml(extraText || "—")}</div>`
    : "";

  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <title>${escapePrintHtml(documentTitle)}</title>
  <style>${printCss}</style>
</head>
<body>
  <article class="print-sheet">
    <header class="print-header">
      <img class="print-logo" src="${escapePrintHtml(logoUrl)}" alt="${escapePrintHtml(CONTACT.institution)}">
      <div>
        <h1 class="print-college">${escapePrintHtml(CONTACT.institution)}</h1>
        <p class="print-doc-title">${escapePrintHtml(documentTitle)}</p>
      </div>
    </header>
    <p class="print-meta">
      <span>Printed date: ${escapePrintHtml(printedAt)}</span>
      <span>Printed by: ${escapePrintHtml(printedBy || "—")}</span>
    </p>
    <div class="print-identity">
      ${photo}
      <table class="print-fields">${rows}</table>
    </div>
    ${extra}
    <footer class="print-footer">
      ${escapePrintHtml(CONTACT.postalOneLine)} · ${escapePrintHtml(CONTACT.phone)} · ${escapePrintHtml(CONTACT.primaryEmail)}
    </footer>
  </article>
</body>
</html>`;
}

export function staffProfilePrintFields(row) {
  return [
    ["Staff ID", row.staffNumber],
    ["Full Name", row.fullName],
    ["Portal Email", row.email],
    ["Role", row.roleSql || row.role],
    ["Job Title", row.jobTitle],
    ["Department", row.departmentName],
    ["Status", row.status],
    ["Phone Number", row.phoneNumber],
    ["Personal Email", row.personalEmail],
    ["Postal Address", row.postalAddress],
    ["Province", row.province],
    ["District", row.district],
    ["Village", row.village],
    ["Emergency Contact Name", row.emergencyContactName],
    ["Emergency Contact Phone", row.emergencyContactPhone],
    ["Relationship", row.emergencyRelationship],
  ];
}

export async function printStaffProfile(row, printedBy, options = {}) {
  const displayed = await displayUrlToDataUrl(options.photoSrc);
  const photoDataUrl = displayed
    || (options.own
      ? await fetchOwnPhotoDataUrl()
      : await fetchUserPhotoDataUrl(row.userId ?? row.staffId));
  const html = buildRecordPrintHtml({
    documentTitle: "Staff Profile Report",
    photoDataUrl,
    printedBy,
    printedAt: printedAtLabel(),
    fields: staffProfilePrintFields(row),
    extraTitle: "Additional Staff Information",
    extraText: row.employmentDetails,
  });
  printHtmlDocument(html);
}

export function printHtmlDocument(html) {
  const iframe = document.createElement("iframe");
  iframe.className = "record-print-frame";
  iframe.setAttribute("aria-hidden", "true");
  iframe.style.cssText = "position:fixed;left:-10000px;top:0;width:800px;height:1100px;border:0;";
  document.body.appendChild(iframe);

  const frameWindow = iframe.contentWindow;
  const frameDoc = frameWindow?.document;
  if (!frameWindow || !frameDoc) {
    iframe.remove();
    return;
  }

  frameDoc.open();
  frameDoc.write(html);
  frameDoc.close();

  const cleanup = () => {
    if (iframe.parentNode) iframe.remove();
  };

  frameWindow.onafterprint = cleanup;
  let started = false;
  const trigger = () => {
    if (started) return;
    const go = () => {
      if (started) return;
      started = true;
      try {
        frameWindow.focus();
        frameWindow.print();
      } catch {
        cleanup();
      }
    };
    const photo = frameDoc.querySelector(".print-photo");
    if (!photo || photo.complete) {
      window.setTimeout(go, 50);
      return;
    }
    photo.addEventListener("load", () => window.setTimeout(go, 50), { once: true });
    photo.addEventListener("error", () => window.setTimeout(go, 50), { once: true });
  };

  iframe.onload = trigger;
  window.setTimeout(trigger, 300);

  window.setTimeout(cleanup, 120000);
}
