"use client";

import { useState } from "react";
import { Button, ErrorState, PageHeader, Tab, TabList, TabPanel, Tabs } from "@/components/ui";
import { LuxuryLoader } from "@/components/brand/LuxuryLoader";
import { NoAccess } from "@/components/layout/InternshipShell";
import { useApiQuery, useSession } from "@/hooks/useApi";
import { P } from "@/lib/internshipPermissions";
import { ProgrammeTab } from "./_components/ProgrammeTab";
import { ApplicationTab } from "./_components/ApplicationTab";
import { InternshipTab } from "./_components/InternshipTab";

export default function MyInternshipPage() {
  const { can } = useSession();
  const canSelf = can(P.selfView);
  const canProgramme = can(P.programmeView);
  const mine = useApiQuery(canSelf ? "/internship/me/application" : null);
  const [tab, setTab] = useState(null);

  if (!canSelf && !canProgramme) return <NoAccess what="the student programme page" />;
  if (canSelf && mine.loading && mine.data === undefined) return <LuxuryLoader variant="compact" label="Loading your application" />;
  if (mine.error) return <ErrorState title="Could not load your application" description={mine.error.message} action={<Button onClick={mine.reload}>Try again</Button>} />;

  const app = mine.data || null;
  const hasInternship = Boolean(app?.internship);
  const current = tab || (hasInternship ? "internship" : app ? "application" : "programme");

  return (
    <div className="space-y-2">
      <PageHeader
        title="UGC Internship"
        description="Intern inside the University Grants Commission for 3–6 months, with a monthly allowance and a graded certificate."
      />
      <Tabs value={current} onChange={setTab}>
        <TabList>
          {canProgramme ? <Tab value="programme">Programme</Tab> : null}
          {app ? <Tab value="application">My application</Tab> : null}
          {hasInternship ? <Tab value="internship">My internship</Tab> : null}
        </TabList>
        {canProgramme ? (
          <TabPanel value="programme">
            <ProgrammeTab
              myApp={app}
              onApplied={(next) => {
                mine.setData(next);
                setTab("application");
              }}
            />
          </TabPanel>
        ) : null}
        {app ? (
          <TabPanel value="application">
            <ApplicationTab app={app} onChange={mine.setData} />
          </TabPanel>
        ) : null}
        {hasInternship ? (
          <TabPanel value="internship">
            <InternshipTab app={app} onChange={mine.setData} onRefresh={mine.reload} />
          </TabPanel>
        ) : null}
      </Tabs>
    </div>
  );
}
