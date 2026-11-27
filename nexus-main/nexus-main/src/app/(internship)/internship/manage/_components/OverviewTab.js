"use client";

import { ClipboardList, Users, Armchair, UserCheck, TriangleAlert } from "lucide-react";
import { Badge, Button, ErrorState, SectionHeader, StatCard } from "@/components/ui";
import { useSetup } from "@/components/layout/InternshipShell";
import { useApiQuery, useSession } from "@/hooks/useApi";
import { MANAGE_TABS, allowed } from "@/lib/internshipPermissions";

export function OverviewTab({ onGo }) {
  const { policy, departments } = useSetup();
  const { can } = useSession();
  const { data, error, reload } = useApiQuery("/internship/overview");

  if (error) return <ErrorState title="Could not load the overview" description={error.message} action={<Button onClick={reload}>Try again</Button>} />;
  if (!data) return <p className="text-sm text-secondary">Loading…</p>;

  const tabAllowed = (value) => allowed(can, MANAGE_TABS.find((t) => t.value === value)?.perm);

  return (
    <div className="space-y-6">
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
        <StatCard label="Open batches" value={data.openBatches} hint="Accepting applications" icon={<ClipboardList className="h-5 w-5" />} tone="blue" />
        <StatCard label="In selection" value={data.inSelection} hint="Applicants not yet decided" icon={<Users className="h-5 w-5" />} tone="violet" />
        <StatCard label="Seats left" value={data.seatsLeft} hint="Open, selection and running batches" icon={<Armchair className="h-5 w-5" />} tone="teal" />
        <StatCard label="Active interns" value={data.activeInterns} hint="Currently placed" icon={<UserCheck className="h-5 w-5" />} tone="green" />
        <StatCard
          label="Attendance risk"
          value={data.atRisk.length}
          hint={`Below ${policy.minAttendancePct}%`}
          icon={<TriangleAlert className="h-5 w-5" />}
          tone={data.atRisk.length ? "red" : "amber"}
        />
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="card-surface p-5 lg:col-span-2">
          <SectionHeader title="Needs action" description="Open items across all batches" />
          {data.actions.length ? (
            <ul className="divide-y divide-slate-200 dark:divide-slate-700">
              {data.actions.map((a) => (
                <li key={a.label} className="flex items-center justify-between gap-3 py-3 text-sm">
                  <span>
                    <strong className="mr-1.5 text-base">{a.count}</strong>
                    {a.label}
                  </span>
                  {tabAllowed(a.tab) ? (
                    <Button size="sm" variant="outline" onClick={() => onGo(a.tab)}>
                      Open
                    </Button>
                  ) : null}
                </li>
              ))}
            </ul>
          ) : (
            <p className="text-sm text-secondary">Nothing waiting. Every application and intern is up to date.</p>
          )}

          {data.atRisk.length ? (
            <div className="mt-4 rounded-xl border border-danger/30 bg-red-50 p-3 text-sm text-danger">
              Attendance below {policy.minAttendancePct}%: {data.atRisk.map((r) => `${r.name} (${r.pct}%)`).join(", ")}
            </div>
          ) : null}
        </div>

        <div className="card-surface p-5">
          <SectionHeader title="Policy rules in force" description="Values from the InternshipPolicy table" />
          <dl className="space-y-2 text-sm">
            {[
              ["Seats per batch", `${policy.batchSize} (cl.6 ক)`],
              ["Batches per year", `${policy.maxBatchesPerYear} (cl.6 খ)`],
              ["Duration", `${policy.minMonths}–${policy.maxMonths} months, no renewal (cl.8)`],
              ["Allowance", `BDT ${Number(policy.monthlyAllowance).toLocaleString("en-BD")}/month, excl. VAT and tax (cl.10)`],
              ["Attendance", `At least ${policy.minAttendancePct}% (cl.11 ঞ)`],
              ["Final report", `${policy.reportWorkingDays} working days after end (cl.11 ট)`],
            ].map(([k, v]) => (
              <div key={k} className="flex justify-between gap-3">
                <dt className="text-secondary">{k}</dt>
                <dd className="text-right font-medium">{v}</dd>
              </div>
            ))}
          </dl>
          <p className="mt-3 text-xs text-secondary">Departments: {departments.map((d) => d.name).join(", ")}.</p>
        </div>
      </div>

      <div className="card-surface p-5">
        <SectionHeader title="Intake funnel" description="Applications by status, all batches" />
        <div className="flex flex-wrap gap-2">
          {data.funnel.map((f) => (
            <Badge
              key={f.status}
              tone={["Certified", "Active"].includes(f.status) ? "green" : ["Rejected", "Terminated", "Not certified"].includes(f.status) ? "red" : "slate"}
              className="px-3 py-1 text-xs"
            >
              {f.status}: {f.count}
            </Badge>
          ))}
        </div>
      </div>
    </div>
  );
}
