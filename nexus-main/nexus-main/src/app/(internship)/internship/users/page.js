"use client";

import { useState } from "react";
import { MoreHorizontal } from "lucide-react";
import {
  Badge,
  Button,
  Checkbox,
  DataTable,
  DropdownMenu,
  ErrorState,
  IconButton,
  Input,
  Modal,
  PageHeader,
  Select,
  Tab,
  TabList,
  Tabs,
} from "@/components/ui";
import { NoAccess, useSetup } from "@/components/layout/InternshipShell";
import { useAction, useApiQuery, useSession } from "@/hooks/useApi";
import { api, qs } from "@/lib/api";
import { P, roleLabel } from "@/lib/internshipPermissions";
import { formatDate } from "@/lib/formatters";
import { UserDocumentsModal } from "./_components/UserDocumentsModal";
import { ReviewQueue } from "./_components/ReviewQueue";

const ROLE_TONE = { Admin: "violet", Mentor: "blue", Student: "teal", Trainer: "cyan", Monitor: "amber" };

const EMPTY = {
  fullName: "",
  email: "",
  phone: "",
  password: "",
  role: "Student",
  designation: "",
  departmentId: "",
  university: "",
  programme: "",
  level: "Undergraduate",
  currentYear: "4",
  graduationMonth: "",
  identityVerified: true,
};

function toForm(u) {
  return {
    ...EMPTY,
    fullName: u.fullName,
    email: u.email,
    phone: u.phone || "",
    role: u.role,
    designation: u.designation || "",
    departmentId: u.departmentId ? String(u.departmentId) : "",
    university: u.student?.university || "",
    programme: u.student?.programme || "",
    level: u.student?.level || "Undergraduate",
    currentYear: u.student?.currentYear != null ? String(u.student.currentYear) : "",
    graduationMonth: u.student?.graduationMonth || "",
    identityVerified: Boolean(u.student?.identityVerified),
  };
}

function toBody(f, creating) {
  const body = {
    fullName: f.fullName,
    email: f.email,
    phone: f.phone,
    designation: f.designation,
    departmentId: f.role === "Mentor" && f.departmentId ? Number(f.departmentId) : null,
    student:
      f.role === "Student"
        ? {
            university: f.university,
            programme: f.programme,
            level: f.level,
            currentYear: f.currentYear === "" ? null : Number(f.currentYear),
            graduationMonth: f.graduationMonth || null,
            identityVerified: f.identityVerified,
          }
        : null,
  };
  if (creating) {
    body.password = f.password;
    body.role = f.role;
  }
  return body;
}

export default function UsersPage() {
  const { can } = useSession();
  const { departments } = useSetup();
  const [role, setRole] = useState("all");
  const [search, setSearch] = useState("");
  const [tab, setTab] = useState("all");
  const list = useApiQuery(can(P.usersView) ? `/users${qs({ role, search: search.trim() })}` : null);
  const queue = useApiQuery(can(P.usersView) ? "/users?toReview=true" : null);
  const roles = useApiQuery(can(P.usersView) ? "/users/roles" : null);
  const { run, busy } = useAction();
  const [modal, setModal] = useState(null); // { mode: "create" | "edit" | "password", user }
  const [docsFor, setDocsFor] = useState(null); // user whose documents are open
  const [form, setForm] = useState(EMPTY);
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const manage = can(P.usersManage);

  if (!can(P.usersView)) return <NoAccess what="user management" />;

  const set = (k) => (e) => setForm({ ...form, [k]: e.target.value });
  const roleNames = (roles.data || []).map((r) => r.name);

  const open = async (mode, user) => {
    setError("");
    setPassword("");
    if (mode === "create") {
      setForm(EMPTY);
      setModal({ mode });
      return;
    }
    if (mode === "edit") {
      try {
        const full = await api.get(`/users/${user.id}`);
        setForm(toForm(full));
        setModal({ mode, user: full });
      } catch (e) {
        setError(e.message);
      }
      return;
    }
    setModal({ mode, user });
  };

  const save = async () => {
    setError("");
    try {
      if (modal.mode === "create") await run(() => api.post("/users", toBody(form, true)), "User created", { silentError: true });
      else await run(() => api.put(`/users/${modal.user.id}`, toBody(form, false)), "User updated", { silentError: true });
      setModal(null);
      list.reload();
    } catch (e) {
      setError(e.message);
    }
  };

  const resetPassword = async () => {
    setError("");
    try {
      await run(() => api.post(`/users/${modal.user.id}/reset-password`, { newPassword: password }), "Password reset", { silentError: true });
      setModal(null);
    } catch (e) {
      setError(e.message);
    }
  };

  const toggleActive = async (u) => {
    if (await run(() => api.post(`/users/${u.id}/active`, { isActive: !u.isActive }), u.isActive ? "User deactivated" : "User activated")) list.reload();
  };
  const reloadAll = () => {
    list.reload();
    queue.reload();
  };
  const roleInfo = Object.fromEntries((roles.data || []).map((r) => [r.name, r.description]));
  const waiting = queue.data?.length || 0;

  const creating = modal?.mode === "create";
  const effectiveRole = creating ? form.role : modal?.user?.role;

  return (
    <div className="space-y-4">
      <PageHeader
        title="Users"
        description="Everyone who can sign in. People who upload documents wait under “Waiting for review” until you check their files."
        actions={manage ? <Button onClick={() => open("create")}>Create user</Button> : null}
      />

      <Tabs value={tab} onChange={setTab}>
        <TabList>
          <Tab value="all">All users</Tab>
          <Tab value="review">
            <span className="inline-flex items-center gap-2">
              Waiting for review
              {waiting ? <span className="rounded-full bg-opportunity px-1.5 py-0.5 text-[11px] leading-none font-semibold text-white">{waiting}</span> : null}
            </span>
          </Tab>
        </TabList>
      </Tabs>

      {tab === "review" ? (
        queue.error ? (
          <ErrorState title="Could not load the review list" description={queue.error.message} action={<Button onClick={queue.reload}>Try again</Button>} />
        ) : (
          <ReviewQueue users={queue.data} onReview={setDocsFor} />
        )
      ) : (
        <>
          <div className="flex flex-wrap items-end gap-3">
            <Select
              label="Role"
              value={role}
              onChange={(e) => setRole(e.target.value)}
              options={[{ value: "all", label: "All roles" }, ...roleNames.map((r) => ({ value: r, label: roleLabel(r) }))]}
            />
            <Input label="Search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Name or email" />
            {list.data ? (
              <p className="pb-2.5 text-sm text-secondary">
                {list.data.length} {list.data.length === 1 ? "person" : "people"}
              </p>
            ) : null}
          </div>

          {list.error ? (
            <ErrorState title="Could not load users" description={list.error.message} action={<Button onClick={list.reload}>Try again</Button>} />
          ) : (
            <DataTable
              className="lg:overflow-visible"
              emptyMessage={list.data ? "No users match." : "Loading…"}
              rows={list.data || []}
              columns={[
                {
                  key: "name",
                  label: "User",
                  render: (u) => (
                    <div className={u.isActive ? "" : "opacity-60"}>
                      <p className="font-medium">{u.fullName}</p>
                      <p className="text-xs text-secondary">{u.email}</p>
                    </div>
                  ),
                },
                {
                  key: "role",
                  label: "Role",
                  render: (u) =>
                    u.role ? (
                      <span title={roleInfo[u.role]}>
                        <Badge tone={ROLE_TONE[u.role] || "slate"}>{roleLabel(u.role)}</Badge>
                      </span>
                    ) : (
                      <span className="text-secondary">—</span>
                    ),
                },
                {
                  key: "detail",
                  label: "University / office",
                  render: (u) => [u.designation, u.department, u.university].filter(Boolean).join(" · ") || <span className="text-secondary">—</span>,
                },
                {
                  key: "documents",
                  label: "Documents",
                  render: (u) =>
                    u.documents ? (
                      <button type="button" onClick={() => setDocsFor(u)} className="inline-flex items-center gap-2 text-left hover:underline">
                        {u.documents} file{u.documents === 1 ? "" : "s"}
                        {u.documentsToReview ? <Badge tone="amber">{u.documentsToReview} to check</Badge> : null}
                      </button>
                    ) : (
                      <span className="text-secondary">None yet</span>
                    ),
                },
                {
                  key: "status",
                  label: "Status",
                  render: (u) =>
                    u.isActive ? (
                      <span className="inline-flex items-center gap-1.5 text-xs">
                        <span className="h-1.5 w-1.5 rounded-full bg-success" /> Active
                      </span>
                    ) : (
                      <span className="inline-flex items-center gap-1.5 text-xs text-secondary">
                        <span className="h-1.5 w-1.5 rounded-full bg-slate-400" /> Deactivated
                      </span>
                    ),
                },
                { key: "created", label: "Joined", render: (u) => <span className="text-secondary">{formatDate(u.createdAt)}</span> },
                {
                  key: "act",
                  label: <span className="sr-only">Actions</span>,
                  render: (u) =>
                    manage ? (
                      <div className="flex items-center justify-end gap-1">
                        <Button size="sm" variant="outline" onClick={() => open("edit", u)}>
                          Edit
                        </Button>
                        <DropdownMenu
                          trigger={
                            <IconButton label={`More for ${u.fullName}`}>
                              <MoreHorizontal className="h-4 w-4" />
                            </IconButton>
                          }
                          items={[
                            { label: "Documents", onClick: () => setDocsFor(u) },
                            { label: "Reset password", onClick: () => open("password", u) },
                            { divider: true },
                            u.isActive
                              ? { label: "Deactivate account", danger: true, disabled: busy, onClick: () => toggleActive(u) }
                              : { label: "Activate account", disabled: busy, onClick: () => toggleActive(u) },
                          ]}
                        />
                      </div>
                    ) : (
                      <Button size="sm" variant="ghost" onClick={() => setDocsFor(u)}>
                        Documents
                      </Button>
                    ),
                },
              ]}
            />
          )}
        </>
      )}

      {docsFor ? <UserDocumentsModal user={docsFor} manage={manage} onClose={() => setDocsFor(null)} onChanged={reloadAll} /> : null}

      <Modal
        open={modal?.mode === "create" || modal?.mode === "edit"}
        onClose={() => setModal(null)}
        title={creating ? "Create user" : `Edit ${modal?.user?.fullName || ""}`}
        description={creating ? "The role is fixed once the user is created." : `Role: ${modal?.user?.role ? roleLabel(modal.user.role) : "—"}`}
        size="lg"
      >
        <div className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <Input label="Full name" value={form.fullName} onChange={set("fullName")} required />
            <Input label="Email" type="email" value={form.email} onChange={set("email")} required />
            <Input label="Phone" value={form.phone} onChange={set("phone")} />
            {creating ? (
              <>
                <Input
                  label="Initial password"
                  type="password"
                  value={form.password}
                  onChange={set("password")}
                  hint="At least 6 characters with a digit."
                  required
                />
                <Select label="Role" value={form.role} onChange={set("role")} options={roleNames.map((r) => ({ value: r, label: roleLabel(r) }))} />
              </>
            ) : null}
            {effectiveRole !== "Student" ? (
              <Input label="Designation" value={form.designation} onChange={set("designation")} placeholder="e.g. Deputy Director" />
            ) : null}
            {effectiveRole === "Mentor" ? (
              <Select
                label="Department"
                value={form.departmentId}
                onChange={set("departmentId")}
                options={departments.map((d) => ({ value: String(d.id), label: d.name }))}
                placeholder="Choose department"
              />
            ) : null}
          </div>

          {effectiveRole === "Student" ? (
            <fieldset className="space-y-3 rounded-xl border border-slate-200 p-4 dark:border-slate-700">
              <legend className="px-1 text-sm font-medium">Academic details (used for the cl.7 eligibility check)</legend>
              <div className="grid gap-4 sm:grid-cols-2">
                <Input label="University" value={form.university} onChange={set("university")} />
                <Input label="Programme" value={form.programme} onChange={set("programme")} placeholder="e.g. BSc in CSE" />
                <Select
                  label="Level"
                  value={form.level}
                  onChange={set("level")}
                  options={[
                    { value: "Undergraduate", label: "Undergraduate" },
                    { value: "Postgraduate", label: "Postgraduate" },
                  ]}
                />
                <Input label="Year of study" type="number" value={form.currentYear} onChange={set("currentYear")} hint="4th year qualifies under cl.7 (ka)" />
                <Input
                  label="Graduation month"
                  type="month"
                  value={form.graduationMonth}
                  onChange={set("graduationMonth")}
                  hint="Within 12 months qualifies under cl.7 (kha)"
                />
              </div>
              <Checkbox label="Identity verified" checked={form.identityVerified} onChange={(e) => setForm({ ...form, identityVerified: e.target.checked })} />
            </fieldset>
          ) : null}

          {error ? (
            <p role="alert" className="text-sm text-danger">
              {error}
            </p>
          ) : null}
          <div className="flex justify-end gap-2">
            <Button variant="ghost" onClick={() => setModal(null)}>
              Cancel
            </Button>
            <Button onClick={save} disabled={busy}>
              {creating ? "Create user" : "Save changes"}
            </Button>
          </div>
        </div>
      </Modal>

      <Modal open={modal?.mode === "password"} onClose={() => setModal(null)} title={`Reset password for ${modal?.user?.fullName || ""}`} size="sm">
        <div className="space-y-4">
          <Input
            label="New password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            hint="At least 6 characters with a digit."
          />
          {error ? (
            <p role="alert" className="text-sm text-danger">
              {error}
            </p>
          ) : null}
          <Button onClick={resetPassword} disabled={busy}>
            Reset password
          </Button>
        </div>
      </Modal>
    </div>
  );
}
