"use client";

import { useState } from "react";
import Link from "next/link";
import { Button, EmptyState, ErrorState, PageHeader, buttonClass } from "@/components/ui";
import { LuxuryLoader } from "@/components/brand/LuxuryLoader";
import { NoAccess } from "@/components/layout/InternshipShell";
import { useApiQuery, useSession } from "@/hooks/useApi";
import { SP } from "@/lib/softskillPermissions";
import { formatCurrency } from "@/lib/formatters";
import { Card } from "../_components/ui";
import { HonorariumTable, VisitCard, VisitReportModal, honorariumTotals } from "../_components/oversight";

/**
 * An expert's (or Commission officer's) monitoring work, cl.14 (ঘ): the visits they are sent on, the report for each,
 * and the honorarium per visit (cl.17).
 */
export default function MonitoringPage() {
  const { can } = useSession();
  const visits = useApiQuery(can(SP.monitorVisit) ? "/softskill/monitoring/visits/mine" : null);
  const honoraria = useApiQuery(can(SP.monitorVisit) ? "/softskill/honoraria/me" : null);
  const [reporting, setReporting] = useState(null);

  if (!can(SP.monitorVisit)) return <NoAccess what="monitoring visits" />;
  if (visits.error) return <ErrorState title="Could not load your visits" description={visits.error.message} action={<Button onClick={visits.reload}>Try again</Button>} />;
  if (!visits.data) return <LuxuryLoader variant="compact" label="Loading your visits" />;

  const planned = visits.data.filter((v) => v.status === "Planned");
  const done = visits.data.filter((v) => v.status === "Submitted");
  const totals = honorariumTotals(honoraria.data);

  return (
    <div className="space-y-4">
      <PageHeader
        title="Monitoring visits"
        description="The Commission sends you to Soft Skill university groups (cl.14 ঘ). Observe, rate and report; the coordinator reads your recommendations."
      />
      <div className="grid gap-5 lg:grid-cols-[3fr_2fr]">
        <div className="space-y-5">
          <Card title="To visit" description="File the report after the visit.">
            {planned.length ? (
              <div className="space-y-3">
                {planned.map((v) => (
                  <VisitCard key={v.id} v={v}>
                    <div className="flex flex-wrap gap-2">
                      <Button size="sm" onClick={() => setReporting(v)}>
                        File report
                      </Button>
                      <Link href={`/softskill/university-groups/${v.universityGroupId}`} className={buttonClass({ size: "sm", variant: "outline" })}>
                        Open university group
                      </Link>
                    </div>
                  </VisitCard>
                ))}
              </div>
            ) : (
              <EmptyState title="No visit planned for you" description="The Commission plans visits and lets you know." />
            )}
          </Card>
          <Card title="Reported">
            {done.length ? (
              <div className="space-y-3">
                {done.map((v) => (
                  <VisitCard key={v.id} v={v} />
                ))}
              </div>
            ) : (
              <p className="text-secondary">No report filed yet.</p>
            )}
          </Card>
        </div>
        <Card title="My honorarium" description={`One per visit reported (cl.17). Paid ${formatCurrency(totals.paid)}${totals.due ? ` · due ${formatCurrency(totals.due)}` : ""}.`}>
          <HonorariumTable rows={honoraria.data} showPerson={false} emptyMessage="Appears once a visit is reported." />
        </Card>
      </div>
      {reporting ? (
        <VisitReportModal
          visit={reporting}
          onClose={() => setReporting(null)}
          onSaved={(saved) => {
            visits.setData(visits.data.map((v) => (v.id === saved.id ? saved : v)));
            setReporting(null);
            honoraria.reload();
          }}
        />
      ) : null}
    </div>
  );
}
