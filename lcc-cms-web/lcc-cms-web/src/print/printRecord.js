import { API_ORIGIN, apiFetch } from "../api";
import { CONTACT } from "../config/contactConfig";
import logoUrl from "../assets/lcc-logo.png";
import printCss from "./recordPrint.css?raw";

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
  try {
    const res = await apiFetch(`${API_ORIGIN}/api/profile/photo/${encodeURIComponent(userId)}`);
    if (!res.ok) return null;
    const blob = await res.blob();
    if (!blob || blob.size === 0) return null;
    return await new Promise((resolve) => {
      const reader = new FileReader();
      reader.onload = () => resolve(String(reader.result || ""));
      reader.onerror = () => resolve(null);
      reader.readAsDataURL(blob);
    });
  } catch {
    return null;
  }
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
  <script>
    window.addEventListener("load", function () {
      window.focus();
      window.print();
    });
  <\/script>
</body>
</html>`;
}

export function printHtmlDocument(html) {
  const iframe = document.createElement("iframe");
  iframe.className = "record-print-frame";
  iframe.setAttribute("aria-hidden", "true");
  iframe.style.cssText = "position:fixed;right:0;bottom:0;width:0;height:0;border:0;visibility:hidden;";
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
  const trigger = () => {
    try {
      frameWindow.focus();
      frameWindow.print();
    } catch {
      cleanup();
    }
  };

  if (frameDoc.readyState === "complete") {
    window.setTimeout(trigger, 80);
  } else {
    iframe.onload = trigger;
  }

  window.setTimeout(cleanup, 120000);
}
