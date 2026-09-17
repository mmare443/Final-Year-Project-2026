function escapeHtml(value) {
    return String(value ?? "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;");
}

function fillPublicContact() {
    const c = window.LCC_CONTACT;
    if (!c) return;

    document.querySelectorAll("[data-contact-institution]").forEach((el) => {
        el.textContent = c.institution;
    });
    document.querySelectorAll("[data-contact-postal]").forEach((el) => {
        el.textContent = c.postalOneLine;
    });

    document.querySelectorAll("[data-contact-footer]").forEach((el) => {
        el.innerHTML = `
                <h4>Contact</h4>
                <p>Phone: ${escapeHtml(c.phone)}</p>
                <p>WhatsApp: ${escapeHtml(c.whatsApp)}</p>
                <p>Fax: ${escapeHtml(c.fax)}</p>
                <p>Email: <a href="mailto:${escapeHtml(c.primaryEmail)}">${escapeHtml(c.primaryEmail)}</a></p>
                <p>Address: ${escapeHtml(c.postalOneLine)}</p>`;
    });

    document.querySelectorAll("[data-contact-cards]").forEach((el) => {
        el.innerHTML = `
                        <article class="info-card">
                            <i class="fa-solid fa-location-dot" aria-hidden="true"></i>
                            <h3>Postal Address</h3>
                            <p>${escapeHtml(c.postalLine1)}<br>
                               ${escapeHtml(c.postalLine2)}<br>
                               ${escapeHtml(c.postalLine3)}<br>
                               ${escapeHtml(c.postalLine4)}</p>
                        </article>
                        <article class="info-card">
                            <i class="fa-solid fa-phone" aria-hidden="true"></i>
                            <h3>Phone</h3>
                            <p><a href="${escapeHtml(c.phoneHref)}">${escapeHtml(c.phone)}</a></p>
                        </article>
                        <article class="info-card">
                            <i class="fa-brands fa-whatsapp" aria-hidden="true"></i>
                            <h3>WhatsApp</h3>
                            <p>${escapeHtml(c.whatsApp)}</p>
                        </article>
                        <article class="info-card">
                            <i class="fa-solid fa-fax" aria-hidden="true"></i>
                            <h3>Fax</h3>
                            <p>${escapeHtml(c.fax)}</p>
                        </article>
                        <article class="info-card">
                            <i class="fa-solid fa-envelope" aria-hidden="true"></i>
                            <h3>Email</h3>
                            <p><a href="mailto:${escapeHtml(c.primaryEmail)}">${escapeHtml(c.primaryEmail)}</a></p>
                        </article>
                        <article class="info-card">
                            <i class="fa-solid fa-user-tie" aria-hidden="true"></i>
                            <h3>Applications</h3>
                            <p>${escapeHtml(c.applicationContact)}<br>
                               <a href="mailto:${escapeHtml(c.applicationEmail)}">${escapeHtml(c.applicationEmail)}</a></p>
                        </article>`;
    });

    document.querySelectorAll("[data-contact-apply-info]").forEach((el) => {
        el.innerHTML = `
                        <i class="fa-solid fa-building-columns" aria-hidden="true"></i>
                        <h2>Application contact &amp; fee</h2>
                        <p>${escapeHtml(c.applicationContact)} —
                           <a href="mailto:${escapeHtml(c.applicationEmail)}">${escapeHtml(c.applicationEmail)}</a></p>
                        <p>K30 application fee: ${escapeHtml(c.bankAccountName)},
                           account ${escapeHtml(c.bankAccountNumber)},
                           ${escapeHtml(c.bank)}.</p>
                        <p>${escapeHtml(c.phone)} · WhatsApp ${escapeHtml(c.whatsApp)}</p>`;
    });
}

document.addEventListener("DOMContentLoaded", () => {
    fillPublicContact();

    const toggle = document.querySelector(".menu-toggle");
    const nav = document.querySelector("#main-navigation");

    if (!toggle || !nav) return;

    toggle.addEventListener("click", () => {
        const open = nav.classList.toggle("is-open");
        toggle.setAttribute("aria-expanded", String(open));
    });

    nav.querySelectorAll("a").forEach(link => {
        link.addEventListener("click", () => {
            nav.classList.remove("is-open");
            toggle.setAttribute("aria-expanded", "false");
        });
    });
});
