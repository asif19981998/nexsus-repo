"use client";

import { Award, ClipboardList, Presentation, School, Users, Wallet } from "lucide-react";
import { Button, ErrorState, StatCard } from "@/components/ui";
import { useApiQuery } from "@/hooks/useApi";
import { Card } from "../../_components/ui";

export function OverviewTab({ onGo }) {
  const { data: o, error, reload } = useApiQuery("/softskill/overview");
  if (error) return <ErrorState title="Could not load the overview" description={error.message} action={<Button onClick={reload}>Try again</Button>} />;
  if (!o) return <p className="text-secondary">Loading…</p>;

  const actions = [
    [o.applicationsToScreen, "applications to screen", "applications"],
    [o.trainersPending, "trainer registrations waiting", "trainers"],
    [o.stipendsReady, "stipends ready to pay", "stipends"],
  ].filter(([n]) => n > 0);

  return (
    <div className="space-y-5">
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        <StatCard label="Open batches" value={o.openBatches} hint={`${o.seatsLeft} seats left`} icon={<ClipboardList className="h-5 w-5" />} tone="teal" />
        <StatCard label="University groups" value={o.universityGroups} hint="In open, selection or running batches" icon={<School className="h-5 w-5" />} tone="blue" />
        <StatCard label="Trainees in training" value={o.activeTrainees} icon={<Users className="h-5 w-5" />} tone="violet" />
        <StatCard label="Trainers waiting" value={o.trainersPending} icon={<Presentation className="h-5 w-5" />} tone="amber" />
        <StatCard label="Stipends ready" value={o.stipendsReady} hint="Months passed, not yet paid" icon={<Wallet className="h-5 w-5" />} tone="green" />
        <StatCard label="Certificates issued" value={o.certified} icon={<Award className="h-5 w-5" />} tone="teal" />
      </div>
      <div className="grid gap-5 lg:grid-cols-2">
        <Card title="Needs your attention">
          {actions.length ? (
            <ul className="space-y-2">
              {actions.map(([n, label, tab]) => (
                <li key={tab} className="flex items-center justify-between gap-2">
                  <span>
                    <strong>{n}</strong> {label}
                  </span>
                  <Button size="sm" variant="outline" onClick={() => onGo(tab)}>
                    Open
                  </Button>
                </li>
              ))}
            </ul>
          ) : (
            <p className="text-secondary">Nothing waiting.</p>
          )}
        </Card>
        <Card title="Applications by status">
          <ul className="space-y-1">
            {o.funnel.map((f) => (
              <li key={f.status} className="flex justify-between">
                <span>{f.status}</span>
                <strong>{f.count}</strong>
              </li>
            ))}
            {!o.funnel.length ? <li className="text-secondary">No applications yet.</li> : null}
          </ul>
        </Card>
      </div>
    </div>
  );
}
