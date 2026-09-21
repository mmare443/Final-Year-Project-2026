import { CONTACT } from "../config/contactConfig";
import logoUrl from "../assets/lcc-logo.png";
import printCss from "./recordPrint.css?raw";
import {
  escapePrintHtml,
  printedAtLabel,
  printHtmlDocument,
} from "./printRecord";

function kpiHtml(items) {
  return `
    <table class="print-fields" style="margin-bottom:16px">
      <tbody>
        ${items
          .map(
            (item) =>
              `<tr><th>${escapePrintHtml(item.label)}</th><td>${escapePrintHtml(item.value)}</td></tr>`
          )
          .join("")}
      </tbody>
    </table>`;
}

function tableHtml(headers, rows) {
  if (!rows.length) {
    return `<p>No rows to report.</p>`;
  }
  return `
    <table class="print-fields">
      <thead>
        <tr>${headers.map((h) => `<th>${escapePrintHtml(h)}</th>`).join("")}</tr>
      </thead>
      <tbody>
        ${rows
          .map(
            (row) =>
              `<tr>${row.map((cell) => `<td>${escapePrintHtml(cell)}</td>`).join("")}</tr>`
          )
          .join("")}
      </tbody>
    </table>`;
}

export function printManagementReport({
  documentTitle,
  printedBy,
  kpis = [],
  tableTitle,
  headers = [],
  rows = [],
}) {
  const printedAt = printedAtLabel();
  const html = `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
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
    ${kpiHtml(kpis)}
    ${tableTitle ? `<h2 class="print-section">${escapePrintHtml(tableTitle)}</h2>` : ""}
    ${headers.length ? tableHtml(headers, rows) : ""}
    <footer class="print-footer">
      ${escapePrintHtml(CONTACT.institution)} · ${escapePrintHtml(CONTACT.postalOneLine)}
    </footer>
  </article>
</body>
</html>`;
  printHtmlDocument(html);
}
