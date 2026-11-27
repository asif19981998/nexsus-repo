# Nexus — UGC Internship API

ASP.NET Core 8 backend for the UGC Internship module of the Nexus frontend (`../nexus-main - Copy`).
The layers follow the SVP reference project: Domain → Application → Infrastructure → API, plus BuildingBlocks.

```
Nexus.BuildingBlocks   exceptions (BadRequest/NotFound/Forbidden/…) and the ProblemDetails handler
Nexus.Domain           entities (Identity, Setup, Internships, Common), enums, status labels
Nexus.Application      repository interfaces, DTOs, services, permission catalogue, policy rules (Rules/InternshipRules.cs)
Nexus.Infrastructure   EF Core DbContext + configurations, repositories, UnitOfWork, JWT, migrations, seeder
Nexus.API              controllers, permission authorization, Program.cs
```

## Run

Requirements: .NET 8 runtime (SDK 8 or later), SQL Server LocalDB (`(localdb)\MSSQLLocalDB`).

```powershell
cd backend
dotnet run --project Nexus.API --launch-profile http
```

- API: http://localhost:5080 — Swagger: http://localhost:5080/swagger
- On start-up the API applies migrations, creates the `NexusInternship` database if needed and seeds it.
- Then start the frontend (`npm run dev` in `nexus-main - Copy`) and open http://localhost:3000/login.
  The frontend reads the API address from `NEXT_PUBLIC_API_URL` (default `http://localhost:5080`, see `.env.example`).

### Demo accounts (password `demo123`)

| Role | Email |
| --- | --- |
| Admin | admin@nexus.demo |
| Mentor | mentor@nexus.demo (Dr. Mahfuza Khatun, Research) — plus five more mentors, one per department |
| Student | student@nexus.demo (Ayesha Rahman, eligible, has not applied) — plus ten students with applications |

Demo data is seeded only into an empty database and only while `Seed:DemoData` is `true` in `appsettings.json`.
Turn it off for a real deployment; an admin account (`Seed:AdminEmail`) is still created.

## Configuration

| Key | Purpose |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | SQL Server connection |
| `Jwt:Key` | Token signing secret, 32+ characters. A random development key is in `appsettings.Development.json`; in production set the `Jwt__Key` environment variable. |
| `Jwt:ExpiryHours` | Token lifetime (default 8) |
| `Cors:Origins` | Frontend origins allowed to call the API |
| `Seed:*` | Demo data switch, demo password, admin email |

## Roles and permissions

Each user has one role: **Admin**, **Student** or **Mentor**. The role is chosen when the admin creates the user (Users page).

What a role may do is the `RolePermissions` table (role ↔ `Permissions.Code`). Edit it directly in the database;
the change applies on the user's next request (the API reads permissions from the database, not from the token),
and the frontend picks it up the next time a page in the portal loads.

```sql
-- Let mentors also mark internships complete
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id FROM Roles r, Permissions p
WHERE r.Name = 'Mentor' AND p.Code = 'internship.intern.complete';

-- Take user management away from admins
DELETE rp FROM RolePermissions rp
JOIN Roles r ON r.Id = rp.RoleId JOIN Permissions p ON p.Id = rp.PermissionId
WHERE r.Name = 'Admin' AND p.Code = 'users.manage';

-- See what each role holds
SELECT r.Name, p.Code FROM RolePermissions rp
JOIN Roles r ON r.Id = rp.RoleId JOIN Permissions p ON p.Id = rp.PermissionId ORDER BY r.Name, p.Code;
```

The seeder grants the defaults in `Nexus.Application/Security/Permissions.cs` only to a role that has **no** permissions,
so your edits are never overwritten. Permission codes are listed in the same file (and in `Permissions` table).

Default grants:

- **Student**: programme view, apply/withdraw, own application and internship, today's sign-in status (signing in and out is in the mobile app: `api/v1/attendance/me/*`) and leave, submit monthly reports (each is that month's allowance bill), submit final report.
- **Mentor**: interns assigned to them only (`internship.intern.view_assigned`), their attendance register with punch photos and locations, decide leave and mark absences, evaluate monthly reports (score + note), review the final report, submit the final evaluation.
- **Admin**: everything else — overview, batches (and cl.6 overrides), holidays and office punch locations, editing punch times, screening, tests, selection, placement, all interns, transfer, completion, certificates, termination, allowance payment with slips, users.

## Policy settings

The policy numbers (20 seats, 2 batches a year, 3–6 months, BDT 20,000, 80 % attendance, 15 working days…) are the single
row in `InternshipPolicy`. Departments, evaluation criteria and grade bands are in `Departments`, `EvaluationCriteria`
and `GradeBands`. Change them in the database; screens read them from `GET /api/v1/internship/setup`.

## Migrations

```powershell
dotnet tool restore
dotnet ef migrations add <Name> --project Nexus.Infrastructure --startup-project Nexus.Infrastructure
```

`dotnet ef database update` uses LocalDB by default, or the `NEXUS_CONNECTION` environment variable.
The API also migrates on start-up.
