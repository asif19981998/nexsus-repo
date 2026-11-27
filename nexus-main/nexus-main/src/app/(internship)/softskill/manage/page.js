"use client";

import { Suspense } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { Button, ErrorState, PageHeader, Tab, TabList, TabPanel, Tabs } from "@/components/ui";
import { LuxuryLoader } from "@/components/brand/LuxuryLoader";
import { NoAccess } from "@/components/layout/InternshipShell";
import { useApiQuery, useSession } from "@/hooks/useApi";
import { allowed } from "@/lib/internshipPermissions";
import { SS_MANAGE_TABS } from "@/lib/softskillPermissions";
import { OverviewTab } from "./_components/OverviewTab";
import { BatchesTab } from "./_components/BatchesTab";
import { ApplicationsTab } from "./_components/ApplicationsTab";
import { TrainersTab } from "./_components/TrainersTab";
import { TraineesTab } from "./_components/TraineesTab";
import { StipendsTab } from "./_components/StipendsTab";
import { MonitoringTab } from "./_components/MonitoringTab";
import { HonorariumTab } from "./_components/HonorariumTab";

const PANELS = {
  overview: OverviewTab,
  batches: BatchesTab,
  applications: ApplicationsTab,
  trainers: TrainersTab,
  trainees: TraineesTab,
  stipends: StipendsTab,
  monitoring: MonitoringTab,
  honorarium: HonorariumTab,
};

export default function ManageSoftSkillPage() {
  // useSearchParams needs a Suspense boundary in this Next.js version.
  return (
    <Suspense fallback={null}>
      <ManageSoftSkill />
    </Suspense>
  );
}

function ManageSoftSkill() {
  const { can } = useSession();
  const router = useRouter();
  const tab = useSearchParams().get("tab");
  const setTab = (value) => router.replace(`/softskill/manage?tab=${value}`, { scroll: false });
  const tabs = SS_MANAGE_TABS.filter((t) => allowed(can, t.perm));
  const setup = useApiQuery(tabs.length ? "/softskill/setup" : null);

  if (!tabs.length) return <NoAccess what="Soft Skill administration" />;
  if (setup.error) return <ErrorState title="Could not load the policy" description={setup.error.message} action={<Button onClick={setup.reload}>Try again</Button>} />;
  if (!setup.data) return <LuxuryLoader variant="compact" label="Loading Soft Skill administration" />;
  const current = tabs.some((t) => t.value === tab) ? tab : tabs[0].value;

  return (
    <div className="space-y-2">
      <PageHeader
        title="UGC Soft Skill Training"
        description="Batches at public universities under the UGC Soft Skill Training Policy: selection, trainers and ToT, university groups, stipends and certificates."
      />
      <Tabs value={current} onChange={setTab}>
        <TabList>
          {tabs.map((t) => (
            <Tab key={t.value} value={t.value}>
              {t.label}
            </Tab>
          ))}
        </TabList>
        {tabs.map((t) => {
          const Panel = PANELS[t.value];
          return (
            <TabPanel key={t.value} value={t.value}>
              <Panel setup={setup.data} onGo={setTab} />
            </TabPanel>
          );
        })}
      </Tabs>
    </div>
  );
}
