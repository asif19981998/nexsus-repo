"use client";

import Link from "next/link";
import { Award, CalendarClock, Presentation, Sparkles } from "lucide-react";
import { Badge, Button, EmptyState, ErrorState, PageHeader, buttonClass } from "@/components/ui";
import { LuxuryLoader } from "@/components/brand/LuxuryLoader";
import { useApiQuery } from "@/hooks/useApi";
import { formatDate } from "@/lib/formatters";

const ICON = { internship: Award, softskill: Sparkles, "softskill-trainer": Presentation };

/**
 * Every programme the signed-in person can join, one card per module, each checked against its own eligibility rules.
 * The profile and documents are shared, so filling them in once counts for every programme.
 */
export default function ProgrammesPage() {
  const cards = useApiQuery("/programmes");

  if (cards.error) return <ErrorState title="Could not load programmes" description={cards.error.message} action={<Button onClick={cards.reload}>Try again</Button>} />;
  if (!cards.data) return <LuxuryLoader variant="compact" label="Checking your programmes" />;

  return (
    <div className="space-y-4">
      <PageHeader
        title="Programmes"
        description="Your profile and documents are shared by every programme. Each one checks its own conditions against them."
      />
      {cards.data.length ? (
        <div className="grid gap-4 md:grid-cols-2">
          {cards.data.map((c) => (
            <ProgrammeCard key={c.module} c={c} />
          ))}
        </div>
      ) : (
        <EmptyState title="No programmes for your role" description="Programmes are for students and university teachers." />
      )}
    </div>
  );
}

function ProgrammeCard({ c }) {
  const Icon = ICON[c.module] || Award;
  const trainer = c.module === "softskill-trainer";

  return (
    <section className="card-surface flex flex-col gap-4 p-5 text-sm">
      <div className="flex items-start gap-3">
        <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-ocean text-white">
          <Icon className="h-5 w-5" />
        </span>
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h2 className="text-base font-semibold">{c.name}</h2>
            {c.myStatus ? (
              <Badge tone="blue">{trainer ? "Registration" : "Application"}: {c.myStatus}</Badge>
            ) : (
              <Badge tone={c.eligible ? "green" : "amber"}>{c.eligible ? "You qualify" : "Not yet eligible"}</Badge>
            )}
          </div>
          <p className="mt-0.5 text-xs text-secondary">{c.summary}</p>
        </div>
      </div>

      {c.facts?.length ? (
        <dl className="grid grid-cols-2 gap-2">
          {c.facts.map((f) => (
            <div key={f.label} className="rounded-lg bg-slate-50 px-3 py-2 dark:bg-nexus-900/40">
              <dt className="text-[11px] font-medium tracking-wide text-secondary uppercase">{f.label}</dt>
              <dd className="font-medium">{f.value}</dd>
            </div>
          ))}
        </dl>
      ) : null}

      {!trainer ? (
        <p className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs">
          <CalendarClock className="h-4 w-4 text-nexus-600" />
          {c.openBatches ? (
            <>
              <span className="font-medium">
                {c.openBatches} open batch{c.openBatches === 1 ? "" : "es"}
              </span>
              {c.applyBy ? <span className="text-secondary">· Apply by {formatDate(c.applyBy)}</span> : null}
              {c.startsOn ? <span className="text-secondary">· Starts {formatDate(c.startsOn)}</span> : null}
            </>
          ) : (
            <span className="text-secondary">No batch open right now</span>
          )}
        </p>
      ) : null}

      <div className="mt-auto">
        <Link href={c.link} className={buttonClass({ variant: c.eligible || c.myStatus ? "primary" : "outline" })}>
          Open {c.name}
        </Link>
      </div>
    </section>
  );
}
