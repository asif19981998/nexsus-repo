# UGC Internship Programme — Build Plan

Companion to [01-requirements.md](01-requirements.md). This plan describes **how the programme is built as a real system**: database, backend API and frontend, and what comes next.

> **Status (24 Nov 2026).** The first version of this plan described a browser-only prototype (Zustand + `localStorage`). That prototype has been **removed**. The programme now runs on a SQL Server database behind an ASP.NET Core API, and the Next.js frontend only calls that API.

## 1. Fit with the rest of Nexus

| Nexus today | UGC Internship Policy | Decision |
| --- | --- | --- |
| Companies host internships; UGC **co-funds** the stipend 50/50 (`funding`, `/ugc/co-funding`). These parts are still a browser-only prototype. | UGC **hosts** the intern and pays 100% from its own budget (cl.10, cl.18). | A **separate module** with its own database, API and screens. It does not reuse the `opportunities` → `applications` → `funding` chain. |
| Six prototype portals (student, faculty, organization, university admin, UGC, helpdesk) with mock logins. | Applicants, UGC Administration, departments, Accounts. | Its own sign-in and roles: **Admin**, **Mentor**, **Student**. Users live in the database. |
| Mock audit and notifications in `localStorage`. | Every step must be traceable (cl.16, FR-X1). | Real `AuditLogs` and `Notifications` tables, written in the same transaction as the change. |

## 2. Where it appears

```
Sign in             /login
Student             /internship/programme       (Programme, My application, My internship)
Admin / Mentor      /internship/manage          (Overview, Batches, Applications, Interns, Allowance)
Admin               /internship/users           (create users, set role, reset password, deactivate)
Everyone            /internship/notifications
API                 http://localhost:5080/api/v1   (Swagger: /swagger)
```

After sign-in, the user goes to the first screen their permissions allow. A menu item or tab is shown only to roles that hold its permission.

## 3. Architecture

```
backend/                                  ASP.NET Core 8, EF Core, SQL Server
├─ Nexus.Domain           entities and enums (Batch, Application, Internship, Attendance, Bill, Report,
│                         Evaluation, Department, InternshipPolicy, User, Role, Permission …)
├─ Nexus.Application      services, DTOs, permission list, policy rules (Rules/InternshipRules.cs)
├─ Nexus.Infrastructure   DbContext, configurations, repositories, migrations, seeder, JWT
├─ Nexus.API              controllers, [HasPermission] authorization, Program.cs
└─ Nexus.BuildingBlocks   exceptions → ProblemDetails (400/401/403/404/409)

nexus-main - Copy/src/                    Next.js (App Router), JavaScript
├─ lib/api.js                             fetch client: sends the JWT, signs out on 401
├─ hooks/useApi.js                        useApiQuery, useAction, useSession().can(permission)
├─ store/useAuthStore.js                  token and signed-in user (role + permission codes)
├─ lib/internshipPermissions.js           permission codes (mirror of Permissions.cs), tabs, menu
├─ components/layout/InternshipShell.js   portal frame; refreshes the user from /auth/me
└─ app/(internship)/internship/           programme/, manage/, users/, notifications/
```

Build rules:

1. **Policy numbers live in the database.** The single `InternshipPolicy` row holds 20 seats, 2 batches a year, 3–6 months, BDT 20,000, 80% attendance, 15 working days and the form limits. `Departments`, `EvaluationCriteria` and `GradeBands` hold the lookups. Screens read them from `GET /internship/setup`. To change a rule, change the row. No code change is needed.
2. **Rules are enforced on the server.** Every calculation (eligibility, working days, attendance %, bills, deductions, withheld last month, grade) is in `InternshipRules.cs`. The frontend shows the results; it never computes them. (The one exception is the net-amount preview in the mentor's monthly evaluation form; the server works out the saved figure.)
3. **Permissions come from the database.** The `RolePermissions` table links roles to permission codes. The API checks it on every request, so a change applies at once, without signing in again. The frontend only hides what a role cannot do.
4. **One change, one transaction.** A status change, its history row, the audit entry and the notifications are saved together, or not at all.
5. **Schema changes go through EF Core migrations.** The API applies pending migrations at start-up.

## 4. Roles

| Role | Can do (default grants) |
| --- | --- |
| **Student** | See the programme and eligibility, apply and withdraw, see own application and internship, check in and out, request leave, submit the monthly report (the month's bill), submit the final report, view payment slips. |
| **Mentor** | See **only the interns assigned to them**, decide leave and mark absences, review the final report, submit the evaluation (cl.14). |
| **Admin** | Everything else: overview, batches and cl.6 overrides, screening, test results, selection, placement, all interns, transfer, completion, certificate, termination, allowance payment with slips, users. |

The seeder grants these defaults only to a role that has no permissions yet, so later database edits are never overwritten. The permission codes are listed in `backend/Nexus.Application/Security/Permissions.cs`.

## 5. Screens

### 5.1 Student — `/internship/programme`

| Tab | Content |
| --- | --- |
| **Programme** | Policy at a glance, open batch, eligibility checklist with the reason for each rule, apply form (statement, up to 3 preferred departments). |
| **My application** | Status, test result, history timeline, withdraw. |
| **My internship** *(Active or later)* | Department and mentor, today's check-in/out, attendance % with the 80% line, recent days, leave requests, monthly reports and allowance (submit a report when a month ends, review, slip) and deadline, evaluation, certificate. |

### 5.2 Admin and Mentor — `/internship/manage`

| Tab | Who | Content |
| --- | --- | --- |
| **Overview** | Admin | Open batches, candidates in selection, seats left, active interns, attendance risks, "needs action" list, funnel. |
| **Batches** | Admin | List, create, move status; the override dialog when a cl.6 limit is exceeded. |
| **Applications** | Admin | Filter by batch and status; shortlist, reject, waitlist, record test, select (capped at capacity), place in a department with a mentor. |
| **Interns** | Admin; Mentor sees own interns only | Placement history, transfer, attendance, leave decisions, mark absence, report review, evaluation, complete, issue certificate or "Not certified", terminate. |
| **Allowance** | Admin | Months by status with attendance, deduction and review score; pay an evaluated month and issue a slip; last-month hold. |

### 5.3 Admin — `/internship/users`

Create a user with a role (Admin, Mentor or Student), edit, reset password, activate or deactivate. A mentor needs a department. A student needs academic details (university, programme, level, year, graduation month, identity verified); the eligibility check reads them. Newest users are listed first.

## 6. Workflow

```
Student        Admin (UGC Administration)   Mentor (department)    Admin (Accounts)
  │ check eligibility
  │ apply ───────────▶ screen ──▶ Shortlisted / Rejected
  │              ◀──── test invite
  │ sit test ────────▶ record score ──▶ rank
  │              ◀──── Selected (≤ seats) / Waitlisted
  │                    place with mentor ──▶ Active
  │ check in/out daily ───────────────────▶ decides leave, marks absence
  │ month ends: submit monthly report ─▶ mentor reviews (4 criteria) + absent days ─▶ pay, slip
  │ final report (≤15 working days after end) ─▶ accept / revise
  │                                         evaluation + recommendation
  │                    complete, then issue certificate (grade) or "Not certified"
  │                    last-month allowance released ──────────▶ pay
```

## 7. Phases

| Phase | Scope | Status |
| --- | --- | --- |
| **0 — Confirm** | Settle open questions A-1…A-14 with UGC (grade bands, departments, divisor). The defaults are database rows, so the answers need data changes only. | Open |
| **1 — Browser prototype** | Clickable demo with `localStorage`. | Done, then **removed** |
| **2 — Real system** | Database, API, JWT sign-in, roles and permissions from the database, audit, notifications, all screens above, user management, demo seed. | **Done** |
| **3 — Hardening** | Automated tests for `InternshipRules` and the services; production configuration (secret JWT key, connection string, CORS, HTTPS); CSV export of interns, attendance and bills; printable certificate; Bangla labels; accessibility pass; backups. | Next |
| **4 — Production features** | Split Admin into Administration / Accounts / Commission with separation of duties; file uploads (CV, transcript, report); e-signature and QR-verified certificate PDF; payment file for UGC Accounts; exam system import; placement at outside institutions (cl.8); self-registration with identity check. | Later |

## 8. What is real and what is still simple

| Concern | Now | Later |
| --- | --- | --- |
| Data | SQL Server database behind the API | Same, with backups and monitoring |
| Sign-in | Email + password, JWT (8 hours), lockout after 5 failures | Self-registration, password reset by email, SSO |
| Test | Admin types the score | Exam system import or upload |
| Attendance | Intern clicks check-in/out | Biometric/ID feed or supervised sign-off |
| Payment | "Mark paid" | Bank / GovPay file |
| Certificate | On-screen certificate | Signed PDF with QR verification |
| Documents | Fixed checklist (CV, Transcript) | Real file upload |
| Roles | Admin, Mentor, Student | Administration, Accounts and Commission split |

## 9. Demo script (for review)

Run the backend and the frontend (see `backend/README.md`), then open http://localhost:3000/login. Password for every demo account: `demo123`.

1. Sign in as `student@nexus.demo` → **My UGC Internship** → see eligibility → **Apply** to the open batch.
2. Sign out; sign in as `admin@nexus.demo` → **UGC Internship → Applications** → Shortlist → record test 86 → Select.
3. Same tab → **Place** in "Research" with mentor Dr. Mahfuza Khatun and a start date → status Active.
4. Back as the student: **My internship** → check in, request leave; when a month has ended, submit its monthly report.
5. Sign in as `mentor@nexus.demo` → **My interns** → approve the leave.
6. Student submits the final report. Mentor accepts it and submits the evaluation.
7. Admin marks the internship complete and issues the certificate; the mentor evaluates each monthly report; **Allowance** → pay and open the slip.
8. Student opens the certificate.
9. Admin tries to create a third 2026 batch → blocked until an override reason is given.

## 10. Risks

| Risk | Mitigation |
| --- | --- |
| Policy gaps (grades, divisor) give wrong numbers | All defaults are database rows, listed in §8 of the requirements; UGC can confirm and change them without a release. |
| A wrong permission grant exposes data | The API checks every endpoint; mentors are limited to their own interns on the server, not only in the UI. |
| JWT key or connection string leaks | `appsettings.Development.json` is git-ignored; production reads `Jwt__Key` and the connection string from the environment. |
| Confusion with company internships | Separate name "UGC Internship", separate screens and roles. |
| No automated tests yet | Phase 3 starts with tests for `InternshipRules` and the service rules. |
