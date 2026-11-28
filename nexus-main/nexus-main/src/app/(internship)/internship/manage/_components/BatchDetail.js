"use client";

import { useState } from "react";
import Link from "next/link";
import { Award, GraduationCap, UserCheck, Users } from "lucide-react";
import { Badge, Button, DataTable, ErrorState, PageHeader, Progress, StatCard, StatusBadge, buttonClass } from "@/components/ui";
import { useAction, useApiQuery, useSession } from "@/hooks/useApi";
import { api, qs } from "@/lib/api";
import { P } from "@/lib/internshipPermissions";
import { formatDate } from "@/lib/formatters";
import { cn } from "@/lib/cn";
import { BatchFormModal, EDITABLE, NEXT } from "./BatchesTab";
import { STATUS } from "./ApplicationsTab";

export const BATCHES_LIST_HREF = "/internship/manage?tab=batches";

/** Statuses that hold a seat (InternshipRules.SeatStatuses on the server). */
const SEAT = ["Selected", "Active", "Completed", "Certified", "Not certified"];
/** Statuses that have an intern record to open. */
const INTERN = ["Active", "Completed", "Certified", "Not certified"];

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

/** One batch: its settings, seat use, and every student who applied to it. */
export function BatchDetail({ id }) {
  const { can } = useSession();
  const { data: batch, error, setData, reload } = useApiQuery(`/internship/batches/${id}`);
  const students = useApiQuery(can(P.applicationView) ? `/internship/applications${qs({ batchId: id })}` : null);
  const { run, busy } = useAction();
  const [editing, setEditing] = useState(false);
  const [filter, setFilter] = useState("seats");
  const manage = can(P.batchManage);

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
    const updated = await run(() => api.post(`/internship/batches/${batch.id}/status`, { status: to }), `${batch.name}: ${to}`);
    if (updated) setData(updated);
  };

  const all = students.data || [];
  const count = (list) => all.filter((x) => list.includes(x.status)).length;
  const current = FILTERS.find((f) => f.key === filter);
  const rows = (current ? all.filter((x) => current.statuses.includes(x.status)) : all)
    .slice()
    .sort((a, b) => a.student.name.localeCompare(b.student.name));
  const seatPct = batch.seats.capacity ? Math.round((batch.seats.taken / batch.seats.capacity) * 100) : 0;

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
              <Button size="sm" variant="outline" disabled={busy} onClick={() => setEditing(true)}>
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
        <Fact label="Apply window">
          {formatDate(batch.applyFrom)} – {formatDate(batch.applyTo)}
        </Fact>
        <Fact label="Internship">
          {formatDate(batch.startDate)} · {batch.durationMonths} months
        </Fact>
        <Fact label="Seats">
          <Progress value={seatPct} label={`${batch.seats.taken} of ${batch.seats.capacity} filled · ${batch.seats.left} left`} />
        </Fact>
        <Fact label="Note">{batch.note || "—"}</Fact>
        {batch.override ? (
          <div className="sm:col-span-2 lg:col-span-4">
            <Badge tone="amber">Override: {batch.override.reason}</Badge>
            {batch.override.limits?.length ? <p className="mt-1 text-xs text-secondary">{batch.override.limits.join(" · ")}</p> : null}
          </div>
        ) : null}
      </div>

      {can(P.applicationView) ? (
        <>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <StatCard label="Applicants" value={students.data ? all.length : "…"} icon={<Users className="size-5" />} tone="blue" />
            <StatCard label="Seats filled" value={students.data ? count(SEAT) : "…"} icon={<UserCheck className="size-5" />} tone="violet" />
            <StatCard label="Interning now" value={students.data ? count(["Active"]) : "…"} icon={<GraduationCap className="size-5" />} tone="teal" />
            <StatCard label="Certified" value={students.data ? count(["Certified"]) : "…"} icon={<Award className="size-5" />} tone="green" />
          </div>

          <section className="space-y-3">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <h2 className="text-lg font-semibold tracking-tight">Students in this batch</h2>
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
                  { key: "university", label: "University", render: (x) => x.student.university || "—" },
                  {
                    key: "status",
                    label: "Status",
                    render: (x) => {
                      const st = STATUS[x.status];
                      return <Badge tone={st?.tone || "slate"}>{st?.label || x.status}</Badge>;
                    },
                  },
                  { key: "applied", label: "Applied", render: (x) => formatDate(x.appliedAt) },
                  { key: "exam", label: "Test", render: (x) => (x.exam ? `${x.exam.score} · ${x.exam.mode}` : "—") },
                  {
                    key: "open",
                    label: "",
                    render: (x) =>
                      INTERN.includes(x.status) && (can(P.internViewAll) || can(P.internViewAssigned)) ? (
                        <Link href={`/internship/manage/interns/${x.id}`} className={buttonClass({ size: "sm", variant: "outline" })}>
                          Open intern
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

      {editing ? (
        <BatchFormModal
          open
          batch={batch}
          onClose={() => setEditing(false)}
          onSaved={(b) => {
            if (!b) return;
            setData(b);
            setEditing(false);
          }}
        />
      ) : null}
    </div>
  );
}
