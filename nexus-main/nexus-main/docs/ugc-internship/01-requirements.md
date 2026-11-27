# UGC Internship Programme — Requirements Document

| | |
| --- | --- |
| **Source** | ইউজিসি ইন্টার্নশিপ নীতিমালা (UGC Internship Policy), University Grants Commission of Bangladesh. 3 scanned pages, 18 clauses, signed by the Secretary and the Chairman. Images are in `D:\nexus\new_re\`. |
| **Target product** | Nexus — National Digital Matchmaking Hub: the UGC Internship module (SQL Server database, ASP.NET Core API, Next.js frontend) |
| **Document status** | Draft v0.2 for review (24 Nov 2026). v0.1 targeted a browser-only prototype; that prototype has been removed and the module now runs on a real database and API. |
| **Language note** | Clause numbers (`cl.N`) refer to the Bengali policy. Sub-clauses use the policy's own lettering: ক=a, খ=b, গ=c, ঘ=d, ঙ=e, চ=f, ছ=g, জ=h, ঝ=i, ঞ=j, ট=k, ঠ=l. |

---

## 1. Purpose

The policy defines an internship programme that UGC runs **inside UGC itself**. UGC takes graduate and postgraduate learners as interns in its own departments, pays a monthly allowance, and issues a graded completion certificate.

Nexus today covers a different case: companies host interns and UGC co-funds the stipend (50/50). The two must not be confused. This document defines what Nexus needs so that the **UGC-hosted internship** can be run end to end on the platform: batch intake, selection, placement into UGC departments, attendance, allowance, evaluation and certification.

## 2. Policy summary

| Clause | What the policy says |
| --- | --- |
| cl.1 | Name: "UGC Internship Policy". |
| cl.2 | Definitions: Commission, Government, Chairman, Member, Full Commission, Secretary, Officer, Employee, Student, Certificate, Intern, Equipment & Facilities, Allowance. "Student" = 4th-year undergraduate or postgraduate learner of a university (cl.2 ঝ). |
| cl.3 | Applies to UGC's interns. |
| cl.4 | Overrides any other policy or order on internships. |
| cl.5 | Aims: real-world experience for graduates; suitable workplaces in an IT-enabled country; skilled workforce for export-led growth; close the skilled-professional gap; build professional skills. |
| cl.6 | **20 interns per batch**; **at most 2 batches per year**; the Commission may raise or lower these numbers when needed. |
| cl.7 | Eligible: (ক) any-subject 4th-year undergrad or postgraduate student, or (খ) a person within **1 year** of finishing a bachelor's/master's, or (গ) holder of an equivalent foreign degree within 1 year. |
| cl.8 | Duration **3 to 6 months**, **not renewable**. Served at UGC or at an institution nominated by UGC. |
| cl.9 | Selection: UGC calls for applications each year; candidates chosen by competitive **written / oral / online** test. |
| cl.10 | Allowance **BDT 20,000 per month** (excluding VAT and tax), paid by UGC. The intern submits a bill at month end; after review of **attendance, conduct, discipline and work skill**, UGC Finance, Accounts & Budget pays it. |
| cl.11 | Conditions (ক–ঠ): follow instructions; obey UGC rules; care for equipment; follow the assigned task plan; keep information confidential; **cannot change institution** during the internship; be present every working day; **sign the attendance register** daily (register is attached to the bill); **unapproved absence = one day's allowance deducted per day**; no absence without a valid reason, and apply through the department to Administration; **overall 80% attendance** required; submit a **full report within 15 working days of completion** to the placing department, and the **last month's allowance is paid only after a satisfactory report**; UGC may **cancel** the internship for conduct or discipline breaches. |
| cl.12 | Placement and transfer: interns are placed in one or more departments as needed and can be transferred between departments; Administration manages this. |
| cl.13 | Programme and nature of work: Administration assigns work that matches the intern's education, experience and background. |
| cl.14 | Evaluation and monitoring: each department monitors and evaluates; experts from academia or industry may be involved; at the end each department sends a **final evaluation report with a recommendation** to the Commission. |
| cl.15 | Certificate: on successful completion, graded **Outstanding (অসাধারণ), Excellent (অতি উত্তম), Good (উত্তম), Satisfactory (সন্তোষজনক)**. No certificate if the standard is not met. |
| cl.16 | Clarification and amendment: UGC's decision is final in a dispute; only the **Full Commission** may amend the policy. |
| cl.17 | Honoraria and other costs for staff and experts who run selection, exams, evaluation and supervision are paid at government rates. |
| cl.18 | Programme costs are paid from UGC's regular budget. |

## 3. Actors

| Actor | In the policy | In Nexus | Needs |
| --- | --- | --- | --- |
| Applicant / Intern | "Intern", "Student" | **Student** role | Check eligibility, apply, take the test, see status, sign attendance, request leave, submit the monthly report (the month's bill) and the final report, view payment slips and the certificate |
| Administration | cl.12, cl.13, cl.11(জ) | **Admin** role | Run batches, screen applicants, record test results, select, place, transfer, complete, terminate, manage users |
| Placing department / supervisor | cl.10(খ), cl.11(ট), cl.14 | **Mentor** role (sees only the interns assigned to them) | Decide leave, mark absences, review each month (attendance, conduct, discipline, work skill) and set the absent days to deduct, review the final report, submit the final evaluation and recommendation |
| Finance, Accounts & Budget | cl.10(খ) | **Admin** role (for now) | Pay evaluated months and issue payment slips |
| Commission | cl.6(গ), cl.15, cl.16 | **Admin** role (for now) | Override batch limits, issue certificate; amend policy numbers in the database |
| Helpdesk | — | Not in this module yet | Handle intern queries and disputes |

Users, roles and permissions are stored in the database. The Admin creates every user and chooses the role. Each role's permissions are rows in `RolePermissions`, so they can be changed without a release. A later phase can split Admin into Administration, Accounts and Commission (see the plan, Phase 4).

## 4. Functional requirements

Priority: **M** must have for the first release, **S** should have, **C** could have later.

### 4.1 Batch management

| ID | Requirement | Clause | Pri |
| --- | --- | --- | --- |
| FR-B1 | UGC can create a batch with name, year, capacity, application window, start date and duration. | 6, 8 | M |
| FR-B2 | Default capacity is **20**. A capacity above 20 needs an override with a written reason. | 6(ক), 6(গ) | M |
| FR-B3 | A calendar year may hold **2 batches**. A third needs an override with a written reason. | 6(খ), 6(গ) | M |
| FR-B4 | Duration must be 3–6 months. Other values are rejected. | 8 | M |
| FR-B5 | Batch status moves through: Draft → Applications open → Selection → Running → Completed (and Closed/Cancelled). | 9 | M |
| FR-B6 | Every override is shown on the batch and written to the audit log. | 6(গ) | M |

### 4.2 Application and eligibility

| ID | Requirement | Clause | Pri |
| --- | --- | --- | --- |
| FR-A1 | The student sees an eligibility check that lists each rule and whether it is met, with the reason. | 7 | M |
| FR-A2 | Eligible by (a) 4th-year/postgraduate status, or (b) graduated within the last 12 months. | 7(ক–গ) | M |
| FR-A3 | Platform rule: identity must be verified in Nexus (the Admin records this on the student's profile). | — | M |
| FR-A4 | A student who already holds a UGC internship (active or finished) cannot apply again. | 8 | M |
| FR-A5 | A student can have only one live application at a time. | — | M |
| FR-A6 | Application form: statement of purpose, up to 3 preferred departments, document checklist (CV, transcript). | 9, 13 | M |
| FR-A7 | Student can withdraw before selection. | — | S |
| FR-A8 | Applications are accepted only while the batch is "Applications open" and inside its window. | 9 | M |

### 4.3 Selection

| ID | Requirement | Clause | Pri |
| --- | --- | --- | --- |
| FR-S1 | UGC screens each application: shortlist or reject (with reason). | 9 | M |
| FR-S2 | UGC records the test: mode (written / oral / online), date and score out of 100. | 9 | M |
| FR-S3 | UGC can select only candidates with a recorded score, and only up to the batch capacity. | 6, 9 | M |
| FR-S4 | Candidates over capacity can be waitlisted and promoted when a seat opens. | 6 | S |
| FR-S5 | Ranked list of tested candidates by score. | 9 | M |
| FR-S6 | Student is notified at each status change. | — | M |

### 4.4 Placement and transfer

| ID | Requirement | Clause | Pri |
| --- | --- | --- | --- |
| FR-P1 | UGC places a selected intern in a UGC department with a supervisor, start date and duration (3–6 months). Status becomes **Active**. | 8, 12 | M |
| FR-P2 | UGC can transfer an active intern to another department. The history of placements is kept. | 12 | M |
| FR-P3 | The student sees department, supervisor, dates and the task plan. | 13 | M |
| FR-P4 | An intern cannot be moved to an organisation outside the UGC-nominated list. | 8, 11(চ) | S |

### 4.5 Attendance and leave

| ID | Requirement | Clause | Pri |
| --- | --- | --- | --- |
| FR-T1 | Intern checks in and out each working day. This is the digital attendance register. | 11(ছ, জ) | M |
| FR-T2 | Working days are Sunday–Thursday. Friday and Saturday are weekend. | — | M |
| FR-T3 | A past working day with no check-in counts as an **unapproved absence**. | 11(ঝ) | M |
| FR-T4 | Intern can request leave with a reason for a date. UGC approves or rejects. | 11(ঞ) | M |
| FR-T5 | UGC can mark an absence as approved or unapproved. | 11(ঝ) | M |
| FR-T6 | Attendance % is shown to the intern and UGC, with a warning below 80%. | 11(ঞ) | M |

### 4.6 Allowance (stipend)

| ID | Requirement | Clause | Pri |
| --- | --- | --- | --- |
| FR-M1 | Monthly allowance is BDT 20,000, shown as excluding VAT and tax. | 10(ক) | M |
| FR-M2 | Months appear one at a time: a month is listed once it starts. Gross = monthly allowance prorated by the month's working days that fall in the internship. | 10 | M |
| FR-M3 | When a month ends the intern submits a **monthly report** on that month's work. It is the month's bill, and the month's attendance register (daily check-in/out) is attached to it. The report cannot be edited after submission. | 10(খ), 11(জ) | M |
| FR-M4 | The mentor (or Admin) reviews the month on **attendance, conduct, discipline and work skill** (0–25 each, total out of 100) with a note, checks the register and enters the **absent days to deduct**. The register's unapproved absences are offered as the starting figure. The review can be changed until the month is paid. | 10(খ), 11(ঝ) | M |
| FR-M5 | Accounts pays only an evaluated month. Paying fixes the net amount and issues a **payment slip** (number, gross, deduction, net, review) that the intern, mentor and Admin can open and print. | 10(খ) | M |
| FR-M6 | The **last month's** allowance stays "Withheld" until the final report is accepted. | 11(ট) | M |

A month moves through: **Accruing** (running) → **Report due** (ended) → **Awaiting evaluation** (report submitted) → **Ready to pay** (or **Withheld** for the last month) → **Paid** (slip issued).

### 4.7 Final report, evaluation and certificate

| ID | Requirement | Clause | Pri |
| --- | --- | --- | --- |
| FR-E1 | Intern submits a final report. The deadline is 15 working days after the internship ends. Late submission is flagged. | 11(ট) | M |
| FR-E2 | Department accepts the report or asks for revision. | 11(ট) | M |
| FR-E3 | Department submits an evaluation: criteria scores plus a written recommendation. | 14 | M |
| FR-E4 | System suggests a grade from the evaluation and checks the 80% attendance rule. | 11(ঞ), 15 | M |
| FR-E5 | UGC issues the certificate with one grade: Outstanding, Excellent, Good or Satisfactory. | 15 | M |
| FR-E6 | If standards are not met, the outcome is "Not certified" with the reasons. | 15 | M |
| FR-E7 | Student can view the certificate and its number. | 15 | M |
| FR-E8 | UGC can terminate an internship for a conduct or discipline breach, with a reason. | 11(ঠ) | M |

### 4.8 Cross-cutting

| ID | Requirement | Pri |
| --- | --- | --- |
| FR-X1 | Every state change writes an audit entry (actor, action, entity, details). | M |
| FR-X2 | Every status change creates an in-app notification for the affected user. | M |
| FR-X3 | All data is stored in the database behind the API. A change and its audit entry and notifications are saved in one transaction. | M |
| FR-X4 | A UGC overview shows the intake funnel, batch usage, active interns, attendance risks and pending actions. | M |
| FR-X5 | Student's certificate and internship appear in the Opportunity Passport. *Not built: the Passport is still part of the browser prototype.* | S |
| FR-X6 | Export intern list, attendance sheet and bills as CSV. *Not built yet (plan, Phase 3).* | S |
| FR-X7 | Bangla labels for key screens. *Not built yet; grade names already carry Bangla.* | C |
| FR-X8 | Sign-in with email and password; every API call checks the caller's permission. A mentor reaches only their own interns. | M |
| FR-X9 | The Admin manages users: create with a role, edit, reset password, activate or deactivate. | M |
| FR-X10 | Policy numbers, departments, evaluation criteria and grade bands are database settings, read by every screen. | M |

## 5. Business rules

| ID | Rule |
| --- | --- |
| BR-01 | A batch has at most 20 interns unless overridden (cl.6). |
| BR-02 | A calendar year has at most 2 batches unless overridden (cl.6). |
| BR-03 | Overrides need a reason, are visible on the batch, and are audited (cl.6 গ). |
| BR-04 | Eligible = (4th-year or postgraduate) OR (graduated ≤ 12 months ago) (cl.7). |
| BR-05 | Duration is 3–6 months and cannot be renewed (cl.8). |
| BR-06 | Selection needs a test score (cl.9). |
| BR-07 | Allowance = prorated gross − (absent days entered by the mentor × daily rate) (cl.10, 11 ঝ). |
| BR-08 | Daily rate = monthly allowance ÷ working days in that month (assumption A-1). |
| BR-09 | Attendance % = days present ÷ working days in the internship period; must be ≥ 80% (cl.11 ঞ). |
| BR-10 | Final report due 15 working days after the end date (cl.11 ট). |
| BR-11 | Last month's allowance is released only after the report is accepted (cl.11 ট). |
| BR-12 | Certificate is issued only if: attendance ≥ 80%, report accepted, evaluation submitted and score ≥ pass mark (cl.11, 14, 15). |
| BR-13 | Grade bands (assumption A-2): ≥ 90 Outstanding; 80–89 Excellent; 70–79 Good; 60–69 Satisfactory; < 60 Not certified. |
| BR-14 | An intern cannot change institution during the internship (cl.11 চ). Only UGC can transfer between UGC departments (cl.12). |
| BR-15 | UGC may terminate for conduct or discipline breaches (cl.11 ঠ). |

## 6. Data model (database)

```
Users, Roles, UserRoles             one role per user: Admin, Mentor or Student
Permissions, RolePermissions        which role may do what (edited in the database)
StudentProfiles                     university, programme, level, year, graduation month, identity verified

InternshipPolicy                    one row: seats, batches/year, months, allowance, attendance %, report days …
Departments, EvaluationCriteria, GradeBands

InternshipBatches                   code, name, year, status, capacity, duration, apply window, start date,
                                    override reason / limits / by / at
InternshipApplications              batch, student, status, applied at, statement, basis (cl.7 clause),
                                    documents, rejection reason, exam mode / date / score
  ApplicationPreferredDepartments   up to 3, ranked
  ApplicationStatusHistory          status, at, by, note
  Internships                       department, mentor, start, end, tasks, certificate number / grade / issued,
                                    outcome result / reasons
    InternshipPlacements            department and mentor history (cl.12)
    AttendanceRecords               one row per day: status, check-in, check-out, reason, approved, decided by
    AllowanceBills                  month, monthly report, submitted at; review scores (attendance, conduct, discipline,
                                    work skill) and total, absent days, note, evaluator; paid at / by, paid amount, slip number
    InternshipReports               text, submitted, status, review note, reviewed by / at
    InternshipEvaluations           total, recommendation, evaluator, at
      EvaluationScores              one score per criterion

Notifications, AuditLogs
```

Attendance %, gross amounts, the report deadline and the suggested grade are computed from these rows by `InternshipRules.cs` each time they are read. The deduction comes from the absent days the mentor enters; the net amount is stored when the month is paid.

Application statuses: `Submitted → Shortlisted → Selected → Active → Completed → Certified | Not certified`.
Side exits: `Rejected`, `Waitlisted`, `Withdrawn`, `Terminated`.

## 7. Non-functional requirements

- **Real system.** SQL Server database, ASP.NET Core 8 API, Next.js frontend. No real payments, exam software or e-signature yet.
- **Security**: JWT sign-in; passwords hashed by ASP.NET Identity; account lockout after 5 failed attempts; every endpoint checks a permission; secrets (JWT key, connection string) come from configuration or the environment, never from source control.
- **Server-side rules**: every policy check and calculation runs in the API (`InternshipRules.cs`). The frontend only shows results and hides actions a role cannot take.
- **Configurable policy**: policy numbers and lookups are database rows, so a UGC decision needs no code change.
- **Traceability**: audit entries record actor, action, entity and details; status history is kept on every application.
- **Schema changes** through EF Core migrations; the API applies them at start-up.
- **Responsive**: mobile, tablet, desktop.
- **Reuse** the existing UI kit (`@/components/ui`).
- **Accessibility**: labelled controls, keyboard-reachable dialogs, status not shown by colour alone.

## 8. Assumptions and open questions

The policy is silent or ambiguous on these. The system uses the stated default. Most defaults are database settings (`InternshipPolicy`, `Departments`, `GradeBands`), so a confirmed answer needs only a data change. **Please confirm with UGC.**

| ID | Gap in the policy | Current default |
| --- | --- | --- |
| A-1 | "One day's allowance" — which divisor? | Monthly allowance ÷ working days of that month (Sun–Thu). |
| A-2 | Grade thresholds are not defined. | 90 / 80 / 70 / 60 (see BR-13). Who sets them? |
| A-3 | Does approved leave count toward the 80% attendance? | No. Only days present count. Approved leave avoids the deduction but not the 80% check. |
| A-4 | "15 working days" — before or after the internship ends? The text reads "within 15 working days of completion". | After the end date. |
| A-5 | Test pass mark and weightings (written vs oral). | Single score out of 100, no pass mark; selection by rank and capacity. |
| A-6 | UGC departments list. | Taken from the org chart in the scan: Administration, Procurement, Research, Science & Technology, Accounts & Budget, ICT. To be confirmed. |
| A-7 | Is the application window per batch or yearly? "প্রতি বছর … আবেদন আহ্বান" says yearly. | Per batch, announced by UGC. |
| A-8 | Is the "institution nominated by UGC" (cl.8) a Nexus company? | Out of scope; placement is inside UGC. See plan Phase 4. |
| A-9 | Who signs the certificate? The scan shows the Secretary and the Chairman signing the policy. | Certificate shows both as signatories. |
| A-10 | Is a 4th-year student's graduation date checked against the 1-year limit? | Rule (a) or (b) is enough; graduation date is only used for (b). |
| A-11 | Number of certificates in a mixed-grade case (cl.15 offers one grade). | One grade per intern. |
| A-12 | Can an intern re-apply after rejection in a later batch? | Yes. After a **completed** internship, no (cl.8). |
| A-13 | Is VAT/tax deduction applied? The policy says the BDT 20,000 is excluding VAT and tax. | Not computed; a note is shown. |
| A-14 | The handwritten page marks on the scans are hard to read. Confirm no page is missing between cl.8 (page 1) and cl.9 (page 2). | Treated as contiguous. |

## 9. Out of scope (this phase)

- Real payment or bank integration, real exam software, e-signature.
- Placement of interns in outside institutions (cl.8 "nominated institution").
- Honorarium payments for staff and experts (cl.17) — noted, not built.
- An admin screen for amending the policy (cl.16). For now the Full Commission's decision is entered directly in the `InternshipPolicy` table.
- Self-registration and file uploads. The Admin creates users; documents are a fixed checklist.

## 10. Acceptance criteria

1. A student with the demo account sees an eligibility checklist and can apply to the open batch.
2. A second application, or an ineligible student's application, is blocked with a clear reason.
3. The Admin can shortlist, record a score, select (capped at capacity), and place with a mentor to activate.
4. Creating a third batch in a year, or capacity 21, is blocked until an override reason is entered; the override is visible and audited.
5. An active intern can check in and out and request leave; the intern's mentor (or an Admin) can decide leave.
6. After a month ends the intern submits a monthly report with the month's register; the mentor reviews it on the four cl.10 criteria and enters the absent days; the Admin pays it and a slip is issued. The last month shows "Withheld" until the final report is accepted.
7. Attendance below 80% shows a warning and blocks the certificate.
8. After report accepted + evaluation submitted, the Admin issues a certificate with a grade; the student can open it.
9. Every step writes an audit entry and a notification; all data is in the database and survives a restart of the API.
10. A mentor cannot open another mentor's intern, and a role without a permission gets 403 from the API.
11. The backend builds; `npm run lint` and `npm run build` pass.
