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

        form.reset();
        status.classList.add("is-success");
        status.textContent =
            "Thank you. Your message has been recorded on this page. For a formal enquiry, email info@lccb.ac.pg or phone the College.";
    });
});
