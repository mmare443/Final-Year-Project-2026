# LCC-CMS Web (React SPA)

## Run locally
```bash
npm install
npm run dev
```
Then open http://localhost:5173 — it redirects straight to `/login`.

## What this is
This is the authenticated system's frontend, per Blueprint Rev 5 / SRS
architecture (React SPA + ASP.NET Core Web API). It's separate from the
static public website (`index.html`, `html/login.html`) — those stay static
and unauthenticated; this app is what `html/login.html`'s Sign In button
will link into once deployed.

## Mock authentication (temporary)
Real Entra ID sign-in isn't wired up yet — cloud/tenant access is still
pending. `src/context/MockAuthContext.jsx` stands in for it: the login page
lets you pick any of the 5 SRS-confirmed roles (Student, Lecturer, HoD,
Registrar/Admin, Management/Principal) and drops you into that role's
dashboard, with route protection identical in shape to what the real
backend policies will enforce.

Try it:
1. `npm run dev`, open `/login`
2. Click "Continue as Student" → lands on `/student`
3. Manually navigate to `/lecturer` in the address bar → redirected to
   `/unauthorized`, because your mock role is Student. This is the same
   behavior a real 403 from the Web API will produce once auth is real.
4. "Sign out" returns you to `/login`.

## Replacing the mock with real Entra ID (once cloud access is confirmed)
See `MockAuthContext.jsx`'s top comment for the exact swap steps, and the
"Backend & Frontend Scaffold Guide (v2, SRS-Aligned)" document, Step 5, for
the full MSAL.js wiring. Short version: delete the mock context, install
`@azure/msal-browser` + `@azure/msal-react`, wrap `<App />` in
`<MsalProvider>` instead of `<MockAuthProvider>`, and swap `useMockAuth()`
calls for `useMsal()` + the ID token's `roles` claim. None of the dashboard
page components need to change — they only read `role` and call
`signOut()` from context, regardless of which provider supplies them.

## Structure
```
src/
├── context/MockAuthContext.jsx    # temporary auth stand-in
├── components/
│   ├── ProtectedRoute.jsx         # role-based route guard
│   ├── DashboardLayout.jsx        # shared sidebar/header shell
│   └── DashboardLayout.css
├── pages/
│   ├── Login.jsx / .css           # role switcher (temporary)
│   ├── Unauthorized.jsx
│   ├── StudentDashboard.jsx
│   ├── LecturerDashboard.jsx
│   ├── HoDDashboard.jsx
│   ├── RegistrarAdminDashboard.jsx
│   └── ManagementPrincipalDashboard.jsx
├── App.jsx                        # routes
├── main.jsx                       # BrowserRouter + render root
└── index.css                      # brand palette + Poppins
```

Each dashboard currently shows placeholder stat cards (`—`) with a note on
which Web API module endpoints they'll connect to. Build out real module UI
inside these files as each backend module comes online.
