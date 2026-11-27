"use client";

import { Suspense } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { PageHeader, Tab, TabList, TabPanel, Tabs } from "@/components/ui";
import { NoAccess } from "@/components/layout/InternshipShell";
import { useSession } from "@/hooks/useApi";
import { MANAGE_TABS, P, allowed } from "@/lib/internshipPermissions";
import { OverviewTab } from "./_components/OverviewTab";
import { BatchesTab } from "./_components/BatchesTab";
import { ApplicationsTab } from "./_components/ApplicationsTab";
import { InternsTab } from "./_components/InternsTab";
import { StipendTab } from "./_components/StipendTab";

const PANELS = {
  overview: OverviewTab,
  batches: BatchesTab,
  applications: ApplicationsTab,
  interns: InternsTab,
  stipend: StipendTab,
};

export default function ManageInternshipPage() {
  // useSearchParams needs a Suspense boundary in this Next.js version.
  return (
    <Suspense fallback={null}>
      <ManageInternship />
    </Suspense>
  );
}

function ManageInternship() {
  const { can } = useSession();
  const router = useRouter();
  // The open tab lives in the URL (?tab=interns), so "back" from an intern page returns to the same tab.
  const tab = useSearchParams().get("tab");
  const setTab = (value) => router.replace(`/internship/manage?tab=${value}`, { scroll: false });
  const tabs = MANAGE_TABS.filter((t) => allowed(can, t.perm));

  if (!tabs.length) return <NoAccess what="UGC Internship administration" />;
  const current = tabs.some((t) => t.value === tab) ? tab : tabs[0].value;
  const mentorOnly = !can(P.internViewAll) && can(P.internViewAssigned);

  return (
    <div className="space-y-2">
      <PageHeader
        title={mentorOnly ? "My interns" : "UGC Internship Programme"}
        description={
          mentorOnly
            ? "Interns placed under your supervision: attendance, leave, final report and evaluation (cl.14)."
            : "Interns hosted inside UGC under the UGC Internship Policy: batches, selection, placement, attendance, allowance and certificates."
        }
      />
      <Tabs value={current} onChange={setTab}>
        {tabs.length > 1 ? (
          <TabList>
            {tabs.map((t) => (
              <Tab key={t.value} value={t.value}>
                {t.label}
              </Tab>
            ))}
          </TabList>
        ) : null}
        {tabs.map((t) => {
          const Panel = PANELS[t.value];
          return (
            <TabPanel key={t.value} value={t.value}>
              <Panel onGo={setTab} />
            </TabPanel>
          );
        })}
      </Tabs>
    </div>
  );
}
