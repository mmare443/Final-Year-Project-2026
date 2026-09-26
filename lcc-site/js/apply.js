const MAX_FILE_SIZE = 5 * 1024 * 1024;

function byId(id) {
    return document.getElementById(id);
}

async function loadProgrammes() {
    const select = byId("programme-select");
    const status = byId("apply-form-status");
    if (!select) return [];

    const res = await fetch(window.collegeApiUrl("/api/academic-structure/programmes"));
    if (!res.ok) {
        throw new Error(`API returned ${res.status}`);
    }

    const programmes = await res.json();
    programmes.sort((a, b) =>
        String(a.programmeName).localeCompare(String(b.programmeName))
    );

    const params = new URLSearchParams(window.location.search);
    const requestedId = params.get("programmeId");

    select.innerHTML = "";
    if (programmes.length === 0) {
        select.innerHTML = `<option value="">No programmes published yet</option>`;
        if (status) {
            status.hidden = false;
            status.textContent = "No programmes are available to apply for yet.";
        }
        return programmes;
    }

    const placeholder = document.createElement("option");
    placeholder.value = "";
    placeholder.textContent = "Select a programme…";
    select.appendChild(placeholder);

    for (const programme of programmes) {
        const option = document.createElement("option");
        option.value = String(programme.programmeId);
        option.textContent = programme.programmeName;
        option.dataset.name = programme.programmeName;
        if (requestedId && String(programme.programmeId) === requestedId) {
            option.selected = true;
        }
        select.appendChild(option);
    }

    return programmes;
}

function validateFiles(form) {
    const required = [
        "letterOfInterest",
        "passportPhoto",
        "feeDepositSlip",
        "grade10Certificate",
        "grade12Certificate",
        "referenceLetter1",
        "referenceLetter2",
    ];

    for (const name of required) {
        const input = form.elements[name];
        if (!input?.files?.[0]) {
            return `Missing required document: ${name}.`;
        }
    }

    for (const input of form.querySelectorAll('input[type="file"]')) {
        const file = input.files?.[0];
        if (!file) continue;
        if (file.size > MAX_FILE_SIZE) {
            return `${file.name} is too large. Maximum size is 5 MB.`;
        }
    }

    return null;
}

document.addEventListener("DOMContentLoaded", async () => {
    const form = byId("application-form");
    const errorEl = byId("apply-form-error");
    const statusEl = byId("apply-form-status");
    const successEl = byId("application-success");
    const submitBtn = byId("apply-submit");

    try {
        await loadProgrammes();
    } catch (err) {
        if (statusEl) {
            statusEl.hidden = false;
            statusEl.textContent =
                "Unable to connect to the College API.";
        }
        console.error(err);
    }

    if (!form) return;

    let submitting = false;
    form.addEventListener("submit", async (event) => {
        event.preventDefault();
        if (submitting) return;
        submitting = true;
        if (errorEl) {
            errorEl.hidden = true;
            errorEl.textContent = "";
        }

        const programmeSelect = byId("programme-select");
        const programmeId = programmeSelect?.value;
        const programmeName = programmeSelect?.selectedOptions?.[0]?.dataset?.name
            || programmeSelect?.selectedOptions?.[0]?.textContent
            || "";

        if (!programmeId) {
            submitting = false;
            if (errorEl) {
                errorEl.hidden = false;
                errorEl.textContent = "Please select a programme.";
            }
            return;
        }

        const fileError = validateFiles(form);
        if (fileError) {
            submitting = false;
            if (errorEl) {
                errorEl.hidden = false;
                errorEl.textContent = fileError;
            }
            return;
        }

        const body = new FormData(form);
        body.set("programmeId", programmeId);
        body.set("programme", programmeName);

        if (submitBtn) {
            submitBtn.disabled = true;
            submitBtn.textContent = "Submitting...";
        }

        try {
            const res = await fetch(window.collegeApiUrl("/api/admissions"), {
                method: "POST",
                body,
            });
            if (!res.ok) {
                throw new Error("submit failed");
            }

            await res.json();
            form.reset();
            if (successEl) {
                successEl.hidden = false;
                successEl.innerHTML = "<h2>Application submitted successfully.</h2>";
                successEl.scrollIntoView({ behavior: "smooth" });
                window.setTimeout(() => {
                    successEl.hidden = true;
                    successEl.innerHTML = "";
                }, 6000);
            }
        } catch (err) {
            if (errorEl) {
                errorEl.hidden = false;
                errorEl.textContent = "Unable to submit the application. Please try again.";
            }
        } finally {
            submitting = false;
            if (submitBtn) {
                submitBtn.disabled = false;
                submitBtn.textContent = "Submit application";
            }
        }
    });
});
