document.addEventListener("DOMContentLoaded", () => {
    const form = document.getElementById("contact-form");
    const status = document.getElementById("contact-status");
    if (!form || !status) return;

    form.addEventListener("submit", (event) => {
        event.preventDefault();
        status.hidden = false;
        status.classList.remove("is-error", "is-success");

        if (!form.reportValidity()) {
            status.classList.add("is-error");
            status.textContent = "Please complete the required fields.";
            return;
        }

        const c = window.LCC_CONTACT || {};
        form.reset();
        status.classList.add("is-success");
        status.textContent =
            `Thank you. Your message has been recorded on this page. For a formal enquiry, email ${c.primaryEmail || "the College"} or phone ${c.phone || "the College"}.`;
    });
});
