"use client";

import { useState } from "react";
import Link from "next/link";
import { CheckCircle2, Circle, GraduationCap, Lock, ShieldAlert, ShieldCheck, Star } from "lucide-react";
import { Badge, Button, ConfirmDialog, EmptyState, ErrorState, Input, Modal, RadioGroup, SearchSelect, Select } from "@/components/ui";
import { LuxuryLoader } from "@/components/brand/LuxuryLoader";
import { useAction, useApiQuery, useSession } from "@/hooks/useApi";
import { api } from "@/lib/api";
import { P, isApplicant, isTrainer, roleLabel } from "@/lib/internshipPermissions";
import { SP, TRAINER_TONE } from "@/lib/softskillPermissions";
import { formatDate } from "@/lib/formatters";
import { cn } from "@/lib/cn";

const LEVELS = [
  { value: "Ssc", label: "SSC or equivalent" },
  { value: "Hsc", label: "HSC or equivalent" },
  { value: "Diploma", label: "Diploma" },
  { value: "Bachelor", label: "Bachelor's degree" },
  { value: "Master", label: "Master's degree" },
  { value: "Doctorate", label: "Doctorate" },
];
const LEVEL_LABEL = Object.fromEntries(LEVELS.map((l) => [l.value, l.label]));
const TERTIARY = ["Bachelor", "Master", "Doctorate"];
const STATUSES = [
  { value: "Studying", label: "Still studying" },
  { value: "Completed", label: "Completed (result published)" },
];
const YEARS = [1, 2, 3, 4, 5, 6].map((y) => ({ value: String(y), label: `Year ${y}` }));
const OTHER = "other";
const EMPTY = { level: "Bachelor", universityId: "", institution: "", programme: "", status: "Studying", currentYear: "", resultMonth: "", result: "", isMain: false };

function Section({ title, description, aside, children }) {
  return (
    <section className="card-surface space-y-4 p-5 text-sm">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <h3 className="text-sm font-semibold uppercase tracking-wide text-slate-600 dark:text-slate-300">{title}</h3>
          {description ? <p className="mt-1 text-xs text-secondary">{description}</p> : null}
        </div>
        {aside}
      </div>
      {children}
    </section>
  );
}

function Fact({ label, children }) {
  return (
    <div>
      <dt className="text-xs text-secondary">{label}</dt>
      <dd className="font-medium break-words">{children || "—"}</dd>
    </div>
  );
}

/** "2026-05" → "May 2026". */
function monthLabel(value) {
  if (!value) return null;
  const [y, m] = value.split("-").map(Number);
  return new Date(y, m - 1, 1).toLocaleDateString("en-GB", { month: "short", year: "numeric" });
}

function thisMonth() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}`;
}

/** The person's profile. It belongs to the account, so every programme (internship now, training and jobs later) reads it. */
export default function MyProfilePage() {
  const { can } = useSession();
  const profile = useApiQuery("/me/profile");
  const { run, busy } = useAction();
  const [editing, setEditing] = useState(null); // { id?, form }
  const [removing, setRemoving] = useState(null);

  if (profile.error)
    return <ErrorState title="Could not load your profile" description={profile.error.message} action={<Button onClick={profile.reload}>Try again</Button>} />;
  if (!profile.data) return <LuxuryLoader variant="compact" label="Loading your profile" />;

  const p = profile.data;
  if (!isApplicant(can)) return <StaffProfile p={p} />;
  if (isTrainer(can)) return <TrainerProfile p={p} onSaved={profile.setData} />;
  const student = p.identityVerified !== null && p.identityVerified !== undefined;
  const main = p.educations.find((e) => e.isMain);

  const setMain = async (e) => {
    const saved = await run(() => api.post(`/me/educations/${e.id}/main`), "Main education changed");
    if (saved) profile.setData(saved);
  };
  const remove = async () => {
    const saved = await run(() => api.delete(`/me/educations/${removing.id}`), "Education removed");
    setRemoving(null);
    if (saved) profile.setData(saved);
  };

  return (
    <div className="space-y-4">
      <div className="grid gap-4 lg:grid-cols-3">
        <div className="lg:col-span-2">
          <Section
            title="Education"
            description="Add each level you studied. Mark your bachelor's degree or higher as the main line: the UGC Internship's cl.7 check reads it."
            aside={
              <Button size="sm" onClick={() => setEditing({ form: { ...EMPTY, isMain: !main } })}>
                Add education
              </Button>
            }
          >
            {p.mainLocked ? (
              <p className="flex items-start gap-2 rounded-lg bg-[#f7f1e6] p-3 text-xs text-opportunity-dark">
                <Lock className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                An application in progress relies on your main education, so that line is locked. Other lines can still change.
              </p>
            ) : null}
            {!main && p.educations.some((e) => TERTIARY.includes(e.level)) ? (
              <p className="rounded-lg bg-[#f7f1e6] p-3 text-xs text-opportunity-dark">Choose which degree is your main line.</p>
            ) : null}

            {p.educations.length ? (
              <ul className="space-y-2">
                {p.educations.map((e) => {
                  const locked = e.isMain && p.mainLocked;
                  return (
                    <li
                      key={e.id}
                      className={cn(
                        "flex flex-wrap items-start gap-3 rounded-lg border p-3",
                        e.isMain ? "border-ocean/60" : "border-[#d5e3df] dark:border-nexus-800",
                      )}
                    >
                      <GraduationCap className="mt-0.5 h-5 w-5 shrink-0 text-nexus-600" />
                      <div className="min-w-0 flex-1">
                        <p className="flex flex-wrap items-center gap-2 font-semibold">
                          {LEVEL_LABEL[e.level] || e.level}
                          {e.isMain ? <Badge tone="teal">Main</Badge> : null}
                          <Badge tone={e.status === "Completed" ? "green" : "blue"}>{e.status === "Completed" ? "Completed" : "Studying"}</Badge>
                        </p>
                        <p className="text-sm">{[e.institution, e.programme].filter(Boolean).join(" · ")}</p>
                        <p className="text-xs text-secondary">
                          {[
                            e.status === "Studying" && e.currentYear ? `Year ${e.currentYear}` : null,
                            e.resultMonth ? `${e.status === "Completed" ? "Result" : "Expected"} ${monthLabel(e.resultMonth)}` : null,
                            e.result,
                          ]
                            .filter(Boolean)
                            .join(" · ") || "—"}
                        </p>
                      </div>
                      <div className="flex flex-wrap gap-1.5">
                        {!e.isMain && TERTIARY.includes(e.level) ? (
                          <Button size="sm" variant="ghost" disabled={busy || p.mainLocked} onClick={() => setMain(e)}>
                            <Star className="h-3.5 w-3.5" /> Make main
                          </Button>
                        ) : null}
                        <Button size="sm" variant="outline" disabled={busy || locked} onClick={() => setEditing({ id: e.id, form: toForm(e) })}>
                          Edit
                        </Button>
                        <Button size="sm" variant="ghost" disabled={busy || locked} onClick={() => setRemoving(e)}>
                          Remove
                        </Button>
                      </div>
                    </li>
                  );
                })}
              </ul>
            ) : (
              <EmptyState title="No education added" description="Add your current or latest degree first; programmes check it before you apply." />
            )}
          </Section>
        </div>

        <div className="space-y-4">
          <Section title="Account" description="Set when the account was created.">
            <dl className="space-y-3">
              <Fact label="Full name">{p.fullName}</Fact>
              <Fact label="Email">{p.email}</Fact>
              <Fact label="Mobile number">{p.phone}</Fact>
              <Fact label="Role">{p.role ? roleLabel(p.role) : null}</Fact>
            </dl>
          </Section>

          {student ? (
            <Section title="Identity" aside={<Badge tone={p.identityVerified ? "green" : "amber"}>{p.identityVerified ? "Verified" : "Not verified"}</Badge>}>
              <p className="flex items-start gap-2 text-xs text-secondary">
                {p.identityVerified ? (
                  <ShieldCheck className="mt-0.5 h-4 w-4 shrink-0 text-success" />
                ) : (
                  <ShieldAlert className="mt-0.5 h-4 w-4 shrink-0 text-opportunity-dark" />
                )}
                <span>
                  {p.identityVerified ? (
                    "The administrator has verified your identity. Every programme accepts it."
                  ) : (
                    <>
                      Upload your National ID, birth registration or passport in{" "}
                      <Link href="/account/documents" className="font-medium text-nexus-700 hover:underline dark:text-nexus-300">
                        My documents
                      </Link>
                      . The administrator verifies it once, for every programme.
                    </>
                  )}
                </span>
              </p>
            </Section>
          ) : null}

          {student && can([P.programmeView, P.selfView, SP.programmeView, SP.selfView]) ? (
            <Link href="/programmes" className="block text-center text-sm font-medium text-nexus-700 hover:underline dark:text-nexus-300">
              Check which programmes you qualify for →
            </Link>
          ) : null}
        </div>
      </div>

      {editing ? (
        <EducationModal
          id={editing.id}
          initial={editing.form}
          canBeMain={!p.mainLocked || !main}
          onClose={() => setEditing(null)}
          onSaved={(saved) => {
            profile.setData(saved);
            setEditing(null);
          }}
        />
      ) : null}

      <ConfirmDialog
        open={Boolean(removing)}
        onClose={() => setRemoving(null)}
        onConfirm={remove}
        loading={busy}
        danger
        title="Remove this education?"
        description={removing ? `${LEVEL_LABEL[removing.level] || removing.level}, ${removing.institution}.` : ""}
        confirmLabel="Remove"
      />
    </div>
  );
}

/** Staff do not apply to programmes: their profile is the account alone, with no education, identity or documents. */
function StaffProfile({ p }) {
  const { user } = useSession();
  return (
    <div className="max-w-xl">
      <Section title="Account" description="Set by the administrator. Ask them if something here is wrong.">
        <dl className="grid gap-3 sm:grid-cols-2">
          <Fact label="Full name">{p.fullName}</Fact>
          <Fact label="Role">{p.role ? roleLabel(p.role) : null}</Fact>
          <Fact label="Email">{p.email}</Fact>
          <Fact label="Mobile number">{p.phone}</Fact>
          <Fact label="Designation">{user?.designation}</Fact>
          <Fact label="Department">{user?.department}</Fact>
        </dl>
      </Section>
    </div>
  );
}

/**
 * A university teacher's profile: the account and the workplace the Soft Skill trainer registration reads (cl.12 ক).
 * The university locks once a registration is waiting or approved, because the Commission reviewed it against that university.
 */
function TrainerProfile({ p, onSaved }) {
  const universities = useApiQuery("/universities");
  const { run, busy } = useAction();
  const t = p.trainer || {};
  const [form, setForm] = useState({
    universityId: t.universityId ? String(t.universityId) : "",
    designation: t.designation || "",
    academicDepartment: t.academicDepartment || "",
    employeeId: t.employeeId || "",
  });
  const [error, setError] = useState("");
  const set = (k) => (e) => setForm((f) => ({ ...f, [k]: e.target.value }));
  const options = (universities.data || []).map((u) => ({ value: String(u.id), label: u.name, sub: u.shortName, search: u.nameBn || "" }));

  const save = async (e) => {
    e.preventDefault();
    setError("");
    try {
      const saved = await run(
        () =>
          api.put("/me/trainer-profile", {
            universityId: form.universityId ? Number(form.universityId) : null,
            designation: form.designation.trim(),
            academicDepartment: form.academicDepartment.trim() || null,
            employeeId: form.employeeId.trim() || null,
          }),
        "Workplace saved",
        { silentError: true },
      );
      if (saved) onSaved(saved);
    } catch (err) {
      setError(err.message);
    }
  };

  return (
    <div className="grid gap-4 lg:grid-cols-3">
      <div className="lg:col-span-2">
        <Section
          title="Workplace"
          description="Soft Skill trainers are the university's own teachers (cl.12 ক). Your trainer registration and university groups follow this university."
          aside={t.university ? <Badge tone={t.universityIsPublic ? "green" : "amber"}>{t.universityIsPublic ? "Public university" : "Not public"}</Badge> : null}
        >
          {p.trainerLocked ? (
            <p className="flex items-start gap-2 rounded-lg bg-[#f7f1e6] p-3 text-xs text-opportunity-dark">
              <Lock className="mt-0.5 h-3.5 w-3.5 shrink-0" />
              Your trainer registration names this university, so it cannot change. Ask the administrator if you moved.
            </p>
          ) : null}
          <form onSubmit={save} className="space-y-4" noValidate>
            {p.trainerLocked ? (
              <Fact label="University">{t.university}</Fact>
            ) : (
              <div className="space-y-1">
                <p className="text-sm font-medium">University</p>
                <SearchSelect
                  value={form.universityId}
                  onChange={(v) => setForm((f) => ({ ...f, universityId: v }))}
                  options={options}
                  placeholder={universities.data ? "Search your university…" : "Loading…"}
                />
              </div>
            )}
            <div className="grid gap-4 sm:grid-cols-2">
              <Input label="Designation" value={form.designation} onChange={set("designation")} placeholder="e.g. Assistant Professor" maxLength={150} required />
              <Input label="Department" value={form.academicDepartment} onChange={set("academicDepartment")} placeholder="e.g. Department of English" maxLength={150} />
              <Input label="Staff id (optional)" value={form.employeeId} onChange={set("employeeId")} maxLength={50} />
            </div>
            {error ? (
              <p role="alert" className="rounded-lg bg-red-50 p-2 text-sm text-danger">
                {error}
              </p>
            ) : null}
            <div className="flex justify-end">
              <Button type="submit" disabled={busy}>
                {busy ? "Saving…" : "Save workplace"}
              </Button>
            </div>
          </form>
        </Section>
      </div>
      <div className="space-y-4">
        <Section title="Account" description="Set when the account was created.">
          <dl className="space-y-3">
            <Fact label="Full name">{p.fullName}</Fact>
            <Fact label="Email">{p.email}</Fact>
            <Fact label="Mobile number">{p.phone}</Fact>
            <Fact label="Role">{p.role ? roleLabel(p.role) : null}</Fact>
          </dl>
        </Section>
        <TrainerRegistration />
        <TrainerDocuments />
      </div>
    </div>
  );
}

/** Where the trainer registration stands (cl.12 খ): the Commission approves it and records the Training of Trainers. */
function TrainerRegistration() {
  const mine = useApiQuery("/softskill/trainers/me");
  const reg = mine.data?.registration;
  return (
    <Section
      title="Trainer registration"
      aside={mine.data ? <Badge tone={reg ? TRAINER_TONE[reg.status] || "slate" : "amber"}>{reg ? reg.status : "Not registered"}</Badge> : null}
    >
      {mine.error ? (
        <p className="text-xs text-danger">{mine.error.message}</p>
      ) : !mine.data ? (
        <p className="text-xs text-secondary">Loading…</p>
      ) : reg ? (
        <dl className="space-y-3">
          <Fact label="Applied">{formatDate(reg.appliedAt)}</Fact>
          <Fact label="Training of Trainers">{reg.totCompleted ? `Completed ${formatDate(reg.totCompletedOn)}` : "Not recorded yet"}</Fact>
          <Fact label="Can take sessions">{reg.canTrain ? "Yes" : "Not yet"}</Fact>
        </dl>
      ) : (
        <p className="text-xs text-secondary">Save your workplace and upload the documents below, then send your registration to the Commission.</p>
      )}
      <Link href="/softskill/trainer" className="block text-sm font-medium text-nexus-700 hover:underline dark:text-nexus-300">
        {reg ? "Open my training work →" : "Register as a trainer →"}
      </Link>
    </Section>
  );
}

/** The trainer registration's document checklist, against the person's library. */
function TrainerDocuments() {
  const reqs = useApiQuery("/me/requirements");
  const list = reqs.data?.find((m) => m.module === "softskill-trainer");
  if (reqs.error || (reqs.data && !list)) return null;
  return (
    <Section
      title="Registration documents"
      aside={list ? <Badge tone={list.ready ? "green" : "amber"}>{list.ready ? "Complete" : `${list.missing.length} missing`}</Badge> : null}
    >
      {!list ? (
        <p className="text-xs text-secondary">Loading…</p>
      ) : (
        <ul className="space-y-2">
          {list.rows.map((r) => (
            <li key={r.groupKey} className="flex items-start gap-2 text-xs">
              {r.met ? (
                <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-success" />
              ) : (
                <Circle className={cn("mt-0.5 h-4 w-4 shrink-0", r.required ? "text-opportunity-dark" : "text-slate-400")} />
              )}
              <span>
                <span className="font-medium">{r.types.map((t) => t.name).join(" or ")}</span>
                {r.required ? null : <span className="text-secondary"> (optional)</span>}
                {r.note ? <span className="block text-secondary">{r.note}</span> : null}
              </span>
            </li>
          ))}
        </ul>
      )}
      <Link href="/account/documents" className="block text-sm font-medium text-nexus-700 hover:underline dark:text-nexus-300">
        Upload in My documents →
      </Link>
    </Section>
  );
}

function toForm(e) {
  return {
    level: e.level,
    universityId: e.universityId ? String(e.universityId) : TERTIARY.includes(e.level) ? OTHER : "",
    institution: e.institution || "",
    programme: e.programme || "",
    status: e.status,
    currentYear: e.currentYear != null ? String(e.currentYear) : "",
    resultMonth: e.resultMonth || "",
    result: e.result || "",
    isMain: e.isMain,
  };
}

/** Same rules as the API, so most mistakes show next to the field before saving. */
function validate(f) {
  const e = {};
  const picked = TERTIARY.includes(f.level) && f.universityId && f.universityId !== OTHER;
  if (TERTIARY.includes(f.level) && !f.universityId) e.institution = "Choose your university, or “Not on the list”.";
  else if (!picked && !f.institution.trim()) e.institution = "Institution is required.";
  if (f.level !== "Ssc" && f.level !== "Hsc" && !f.programme.trim()) e.programme = "Programme or department is required.";
  const now = thisMonth();
  if (f.status === "Studying") {
    if (f.level === "Bachelor" && !f.currentYear) e.currentYear = "Choose your year of study.";
    if (f.resultMonth && f.resultMonth < now) e.resultMonth = "This month has passed. If the result is out, choose “Completed”.";
  } else {
    if (!f.resultMonth) e.resultMonth = "Enter the month the result was published.";
    else if (f.resultMonth > now) e.resultMonth = "This month is still ahead. If you are still studying, choose “Still studying”.";
  }
  return e;
}

function EducationModal({ id, initial, canBeMain, onClose, onSaved }) {
  const { run, busy } = useAction();
  const [form, setForm] = useState(initial);
  const [errors, setErrors] = useState({});
  const [error, setError] = useState("");
  const universities = useApiQuery("/universities");

  const school = form.level === "Ssc" || form.level === "Hsc";
  const studying = form.status === "Studying";
  const tertiary = TERTIARY.includes(form.level);
  const picked = tertiary && form.universityId && form.universityId !== OTHER;
  const universityOptions = [
    ...(universities.data || []).map((u) => ({ value: String(u.id), label: u.name, sub: u.shortName, search: u.nameBn || "" })),
    { value: OTHER, label: "Not on the list", sub: "Type the name yourself" },
  ];

  const set = (key) => (value) => {
    setForm((f) => ({ ...f, [key]: value }));
    if (errors[key]) setErrors((x) => ({ ...x, [key]: undefined }));
  };
  const onInput = (key) => (e) => set(key)(e.target.value);

  const save = async (e) => {
    e.preventDefault();
    const found = validate(form);
    setErrors(found);
    setError("");
    if (Object.keys(found).length) return;
    const body = {
      level: form.level,
      universityId: picked ? Number(form.universityId) : null,
      institution: picked ? null : form.institution.trim(),
      programme: form.programme.trim() || null,
      status: form.status,
      currentYear: studying && form.currentYear ? Number(form.currentYear) : null,
      resultMonth: form.resultMonth || null,
      result: form.result.trim() || null,
      isMain: tertiary && form.isMain,
    };
    try {
      const saved = await run(
        () => (id ? api.put(`/me/educations/${id}`, body) : api.post("/me/educations", body)),
        id ? "Education updated" : "Education added",
        {
          silentError: true,
        },
      );
      if (saved) onSaved(saved);
    } catch (err) {
      setError(err.message);
    }
  };

  return (
    <Modal open onClose={onClose} title={id ? "Edit education" : "Add education"} size="lg">
      <form onSubmit={save} className="space-y-4" noValidate>
        <div className="grid gap-4 sm:grid-cols-2">
          <Select label="Level" value={form.level} onChange={onInput("level")} options={LEVELS} />
          {tertiary ? (
            <div className="space-y-1">
              <p className="text-sm font-medium">University</p>
              <SearchSelect
                value={form.universityId}
                onChange={set("universityId")}
                options={universityOptions}
                placeholder={universities.data ? "Search your university…" : "Loading…"}
              />
              {errors.institution && !picked && form.universityId !== OTHER ? <p className="text-xs text-danger">{errors.institution}</p> : null}
              <p className="text-xs text-secondary">The Soft Skill Training is for public universities on this list (cl.7).</p>
            </div>
          ) : null}
          {!tertiary || form.universityId === OTHER ? (
            <Input
              label={school ? "School or college" : "University or institute"}
              value={form.institution}
              onChange={onInput("institution")}
              error={errors.institution}
              maxLength={200}
              required
            />
          ) : null}
          <Input
            label={school ? "Group" : "Programme / department"}
            value={form.programme}
            onChange={onInput("programme")}
            error={errors.programme}
            placeholder={school ? "e.g. Science" : "e.g. BSc in Computer Science and Engineering"}
            maxLength={200}
            required={!school}
          />
          <Input
            label="Result"
            value={form.result}
            onChange={onInput("result")}
            placeholder={school ? "e.g. GPA 5.00" : "e.g. CGPA 3.62 / 4.00"}
            maxLength={50}
          />
        </div>
        <RadioGroup label="Status" name="status" options={STATUSES} value={form.status} onChange={set("status")} />
        <div className="grid gap-4 sm:grid-cols-2">
          {studying && !school ? (
            <Select
              label="Year of study"
              value={form.currentYear}
              onChange={onInput("currentYear")}
              options={YEARS}
              placeholder="Choose a year"
              error={errors.currentYear}
              hint={form.level === "Bachelor" ? "Year 1–2: Soft Skill Training. Year 4: UGC Internship." : undefined}
            />
          ) : null}
          <Input
            label={studying ? "Expected result month" : "Result published (month)"}
            type="month"
            value={form.resultMonth}
            onChange={onInput("resultMonth")}
            error={errors.resultMonth}
            required={!studying}
            hint={studying ? "Optional." : tertiary ? "The UGC Internship accepts you for 12 months after this (cl.7 খ, গ)." : undefined}
          />
        </div>
        {tertiary && !initial.isMain ? (
          <label className={cn("flex items-start gap-2 text-sm", !canBeMain && "opacity-60")}>
            <input type="checkbox" className="mt-0.5" checked={form.isMain} disabled={!canBeMain} onChange={(e) => set("isMain")(e.target.checked)} />
            <span>
              Make this my main education
              <span className="block text-xs text-secondary">
                {canBeMain ? "The UGC Internship's eligibility check reads the main line." : "Locked while an application is in progress."}
              </span>
            </span>
          </label>
        ) : null}
        {error ? (
          <p role="alert" className="rounded-lg bg-red-50 p-2 text-sm text-danger">
            {error}
          </p>
        ) : null}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={onClose} disabled={busy}>
            Cancel
          </Button>
          <Button type="submit" disabled={busy}>
            {busy ? "Saving…" : "Save"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
