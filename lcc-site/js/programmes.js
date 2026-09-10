const API_ORIGIN = "http://localhost:5000";

function byId(id) {
    return document.getElementById(id);
}

function escapeHtml(value) {
    return String(value ?? "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;");
}

function formatDuration(years) {
    const n = Number(years);
    if (!Number.isFinite(n) || n <= 0) return "Duration not listed";
    const label = n === 1 ? "year" : "years";
    return Number.isInteger(n) ? `${n} ${label}` : `${n} ${label}`;
}

async function fetchJson(path) {
    const res = await fetch(`${API_ORIGIN}${path}`);
    if (!res.ok) {
        throw new Error(`API returned ${res.status}`);
    }
    return res.json();
}

function groupTree(faculties, departments, programmes) {
    const deptsByFaculty = new Map();
    for (const dept of departments) {
        const facultyId = dept.facultyId;
        if (!deptsByFaculty.has(facultyId)) {
            deptsByFaculty.set(facultyId, []);
        }
        deptsByFaculty.get(facultyId).push(dept);
    }

    const programmesByDept = new Map();
    for (const programme of programmes) {
        const departmentId = programme.departmentId;
        if (!programmesByDept.has(departmentId)) {
            programmesByDept.set(departmentId, []);
        }
        programmesByDept.get(departmentId).push(programme);
    }

    return [...faculties]
        .sort((a, b) => String(a.facultyName).localeCompare(String(b.facultyName)))
        .map((faculty) => {
            const facultyDepts = (deptsByFaculty.get(faculty.facultyId) || [])
                .slice()
                .sort((a, b) => String(a.departmentName).localeCompare(String(b.departmentName)))
                .map((dept) => ({
                    ...dept,
                    programmes: (programmesByDept.get(dept.departmentId) || [])
                        .slice()
                        .sort((a, b) => String(a.programmeName).localeCompare(String(b.programmeName))),
                }));
            return { ...faculty, departments: facultyDepts };
        });
}

function renderProgrammeCard(programme) {
    return `
        <article class="info-card">
            <i class="fa-solid fa-graduation-cap" aria-hidden="true"></i>
            <h3>${escapeHtml(programme.programmeName)}</h3>
            <p class="programme-duration">${escapeHtml(formatDuration(programme.durationYears))}</p>
            <a href="apply.html?programmeId=${encodeURIComponent(programme.programmeId)}">Apply Now</a>
        </article>
    `;
}

function renderTree(tree) {
    const withProgrammes = tree.filter((faculty) =>
        faculty.departments.some((dept) => dept.programmes.length > 0)
    );

    if (withProgrammes.length === 0) {
        return `<p class="programmes-status">No programmes have been published yet.</p>`;
    }

    return withProgrammes.map((faculty) => {
        const departmentsHtml = faculty.departments
            .filter((dept) => dept.programmes.length > 0)
            .map((dept) => `
                <div class="department-block">
                    <h3>${escapeHtml(dept.departmentName)}</h3>
                    <div class="card-grid">
                        ${dept.programmes.map(renderProgrammeCard).join("")}
                    </div>
                </div>
            `)
            .join("");

        return `
            <section class="faculty-block" aria-labelledby="faculty-${faculty.facultyId}">
                <h2 id="faculty-${faculty.facultyId}">${escapeHtml(faculty.facultyName)}</h2>
                ${departmentsHtml}
            </section>
        `;
    }).join("");
}

document.addEventListener("DOMContentLoaded", async () => {
    const status = byId("programmes-status");
    const root = byId("programmes-root");
    if (!status || !root) return;

    try {
        const [programmes, departments, faculties] = await Promise.all([
            fetchJson("/api/academic-structure/programmes"),
            fetchJson("/api/academic-structure/departments"),
            fetchJson("/api/academic-structure/faculties"),
        ]);

        root.innerHTML = renderTree(groupTree(faculties, departments, programmes));
        status.hidden = true;
    } catch (err) {
        status.textContent =
            "Couldn't load programmes. Make sure the College API is running on http://localhost:5000.";
        console.error(err);
    }
});
