"use client";

import { useState } from "react";
import Link from "next/link";
import { Award, GraduationCap, School, UserCheck, Users } from "lucide-react";
import { Badge, Button, DataTable, ErrorState, PageHeader, Progress, Select, StatCard, StatusBadge, buttonClass } from "@/components/ui";
import { useAction, useApiQuery, useSession } from "@/hooks/useApi";
import { api, qs } from "@/lib/api";
import { SP } from "@/lib/softskillPermissions";
import { formatDate } from "@/lib/formatters";
import { cn } from "@/lib/cn";
import { period } from "../../_components/ui";
import { EDITABLE, EditBatchModal, NEXT } from "./BatchesTab";

export const BATCHES_LIST_HREF = "/softskill/manage?tab=batches";

/** Statuses that hold a seat (SoftSkillRules.SeatStatuses on the server). */
const SEAT = ["Selected", "Active", "Completed", "Certified", "Not certified"];

const FILTERS = [
  { key: "seats", label: "Seat holders", statuses: SEAT },
  { key: "pipeline", label: "In selection", statuses: ["Submitted", "Shortlisted", "Waitlisted"] },
  { key: "out", label: "Rejected / left", statuses: ["Rejected", "Withdrawn", "Terminated"] },
];

function BackLink() {
  return (
    <Link href={BATCHES_LIST_HREF} className="text-sm font-medium text-nexus-700 hover:underline dark:text-nexus-300">
      ← All batches
    </Link>
  );
}

function Fact({ label, children }) {
  return (
    <div className="min-w-0">
      <p className="text-[11px] font-semibold uppercase tracking-[0.08em] text-slate-500">{label}</p>
      <div className="mt-1 font-medium text-slate-900 dark:text-slate-50">{children}</div>
    </div>
  );
}

/** One Soft Skill batch: its settings, university groups, and every student who applied to it. */
export function BatchDetail({ id }) {
  const { can } = useSession();
  const { data: batch, error, setData, reload } = useApiQuery(`/softskill/batches/${id}`);
  const setup = useApiQuery("/softskill/setup");
  const students = useApiQuery(can(SP.applicationView) ? `/softskill/applications${qs({ batchId: id })}` : null);
  const { run, busy } = useAction();
  const [editing, setEditing] = useState(false);
  const [filter, setFilter] = useState("seats");
  const [group, setGroup] = useState("all");
  const manage = can(SP.batchManage);

  if (error) {
    return (
      <div className="space-y-4">
        <BackLink />
        <ErrorState title="Could not load this batch" description={error.message} action={<Button onClick={reload}>Try again</Button>} />
      </div>
    );
  }
  if (!batch) {
    return (
      <div className="space-y-4">
        <BackLink />
        <p className="text-sm text-secondary">Loading…</p>
      </div>
    );
  }

  const move = async (to) => {
    const updated = await run(() => api.post(`/softskill/batches/${batch.id}/status`, { status: to }), `${batch.name}: ${to}`);
    if (updated) {
      setData(updated);
      students.reload();
    }
  };

  const all = (students.data || []).filter((x) => group === "all" || String(x.universityGroupId) === group);
  const count = (list) => all.filter((x) => list.includes(x.status)).length;
  const current = FILTERS.find((f) => f.key === filter);
  const rows = (current ? all.filter((x) => current.statuses.includes(x.status)) : all)
    .slice()
    .sort((a, b) => a.university.localeCompare(b.university) || a.student.name.localeCompare(b.student.name));
  const seatPct = batch.capacity ? Math.round((batch.taken / batch.capacity) * 100) : 0;

  return (
    <div className="space-y-5">
      <PageHeader
        breadcrumbs={<BackLink />}
        title={batch.name}
        description={`${batch.code} · ${batch.year}`}
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={batch.status} />
            {manage && EDITABLE.includes(batch.status) ? (
              <Button size="sm" variant="outline" disabled={busy || !setup.data} onClick={() => setEditing(true)}>
                Edit batch
              </Button>
            ) : null}
            {manage && NEXT[batch.status] ? (
              <Button size="sm" disabled={busy} onClick={() => move(NEXT[batch.status].to)}>
                {NEXT[batch.status].label}
              </Button>
            ) : null}
          </div>
        }
      />

      <div className="card-surface grid gap-4 p-5 sm:grid-cols-2 lg:grid-cols-4">
        <Fact label="Apply window">{period(batch.applyFrom, batch.applyTo)}</Fact>
        <Fact label="Training">{period(batch.startDate, batch.endDate)}</Fact>
        <Fact label="Seats">
          <Progress value={seatPct} label={`${batch.taken} of ${batch.capacity} selected`} />
        </Fact>
        <Fact label="Note">{batch.note || "—"}</Fact>
        {batch.overrideReason ? (
          <div className="sm:col-span-2 lg:col-span-4">
            <Badge tone="amber">Override: {batch.overrideReason}</Badge>
          </div>
        ) : null}
      </div>

      <section className="space-y-3">
        <h2 className="text-lg font-semibold tracking-tight">Universities ({batch.universityGroups.length})</h2>
        <DataTable
          emptyMessage="No university added yet."
          rows={batch.universityGroups}
          columns={[
            { key: "uni", label: "University", render: (c) => <span className="font-medium">{c.university.name}</span> },
            { key: "seats", label: "Seats", render: (c) => `${c.taken}/${c.capacity}${c.overrideReason ? " *" : ""}` },
            { key: "coord", label: "Coordinator", render: (c) => (c.coordinator ? c.coordinator.name : <Badge tone="amber">Not named</Badge>) },
            { key: "venue", label: "Venue", render: (c) => c.venue || "—" },
            {
              key: "open",
              label: "",
              render: (c) => (
                <Link href={`/softskill/university-groups/${c.id}`} className={buttonClass({ size: "sm", variant: "outline" })}>
                  Open
                </Link>
              ),
            },
          ]}
        />
      </section>

      {can(SP.applicationView) ? (
        <>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <StatCard label="Applicants" value={students.data ? all.length : "…"} icon={<Users className="size-5" />} tone="blue" />
            <StatCard label="Seats filled" value={students.data ? count(SEAT) : "…"} icon={<UserCheck className="size-5" />} tone="violet" />
            <StatCard label="In training" value={students.data ? count(["Active"]) : "…"} icon={<GraduationCap className="size-5" />} tone="teal" />
            <StatCard label="Certified" value={students.data ? count(["Certified"]) : "…"} icon={<Award className="size-5" />} tone="green" />
          </div>

          <section className="space-y-3">
            <div className="flex flex-wrap items-end justify-between gap-3">
              <h2 className="text-lg font-semibold tracking-tight">Students in this batch</h2>
              <div className="flex flex-wrap items-end gap-3">
                {batch.universityGroups.length > 1 ? (
                  <Select
                    label="University"
                    value={group}
                    onChange={(e) => setGroup(e.target.value)}
                    options={[
                      { value: "all", label: "All universities" },
                      ...batch.universityGroups.map((c) => ({ value: String(c.id), label: c.university.name })),
                    ]}
                  />
                ) : null}
                <div role="tablist" aria-label="Student filter" className="flex flex-wrap gap-1.5">
                  {[...FILTERS, { key: "all", label: "All applicants" }].map((f) => {
                    const n = f.statuses ? count(f.statuses) : all.length;
                    const on = filter === f.key;
                    return (
                      <button
                        key={f.key}
                        type="button"
                        role="tab"
                        aria-selected={on}
                        onClick={() => setFilter(f.key)}
                        className={cn(
                          "inline-flex items-center gap-2 rounded-full border px-3 py-1.5 text-sm transition-colors",
                          on
                            ? "border-ocean bg-ocean font-semibold text-white dark:border-sky dark:bg-sky dark:text-nexus-950"
                            : "border-[#d5e3df] text-nexus-800 hover:border-ocean dark:border-nexus-700 dark:text-nexus-200",
                        )}
                      >
                        {f.label}
                        <span className="text-xs opacity-80">{n}</span>
                      </button>
                    );
                  })}
                </div>
              </div>
            </div>

            {students.error ? (
              <ErrorState title="Could not load students" description={students.error.message} action={<Button onClick={students.reload}>Try again</Button>} />
            ) : (
              <DataTable
                emptyMessage={students.data ? "No students here." : "Loading…"}
                rows={rows}
                columns={[
                  {
                    key: "student",
                    label: "Student",
                    render: (x) => (
                      <div>
                        <p className="font-medium">{x.student.name}</p>
                        <p className="text-xs text-secondary">{x.student.email || "—"}</p>
                      </div>
                    ),
                  },
                  {
                    key: "uni",
                    label: "University",
                    render: (x) => (
                      <span className="inline-flex items-center gap-1.5">
                        <School className="size-3.5 text-slate-400" aria-hidden />
                        {x.university}
                      </span>
                    ),
                  },
                  { key: "status", label: "Status", render: (x) => <StatusBadge status={x.status} /> },
                  { key: "applied", label: "Applied", render: (x) => formatDate(x.appliedAt) },
                  { key: "score", label: "Screening", render: (x) => (x.screeningScore != null ? `${x.screeningScore}/100` : "—") },
                  {
                    key: "open",
                    label: "",
                    render: (x) =>
                      SEAT.includes(x.status) ? (
                        <Link href={`/softskill/university-groups/${x.universityGroupId}`} className={buttonClass({ size: "sm", variant: "outline" })}>
                          Open group
                        </Link>
                      ) : null,
                  },
                ]}
              />
            )}
          </section>
        </>
      ) : (
        <p className="text-sm text-secondary">Your role cannot see the students who applied to this batch.</p>
      )}

      {editing && setup.data ? (
        <EditBatchModal
          batch={batch}
          policy={setup.data.policy}
          onClose={() => setEditing(false)}
          onSaved={(b) => {
            setData(b);
            setEditing(false);
          }}
        />
      ) : null}
    </div>
  );
}
