"use client";

import { useState } from "react";
import { PageHeader, Tab, TabList, TabPanel, Tabs } from "@/components/ui";
import { NoAccess } from "@/components/layout/InternshipShell";
import { useSession } from "@/hooks/useApi";
import { P } from "@/lib/internshipPermissions";
import { DailyTab } from "./_components/DailyTab";
import { HolidaysTab } from "./_components/HolidaysTab";
import { MonthlyTab } from "./_components/MonthlyTab";
import { LocationsTab } from "./_components/LocationsTab";

// Mentors see the register for the interns they mentor; holidays and office locations are programme settings.
const TABS = [
  { value: "daily", label: "Daily attendance", perm: [P.internViewAll, P.internViewAssigned] },
  { value: "monthly", label: "Monthly view", perm: [P.internViewAll, P.internViewAssigned] },
  { value: "holidays", label: "Weekends & holidays", perm: P.batchManage },
  { value: "locations", label: "Office locations", perm: P.batchManage },
];

export default function AttendancePage() {
  const { can } = useSession();
  const [tab, setTab] = useState("daily");
  if (!can([P.internViewAll, P.internViewAssigned])) return <NoAccess what="Attendance" />;
  const tabs = TABS.filter((t) => can(t.perm));
  const settings = can(P.batchManage);

  return (
    <div className="space-y-2">
      <PageHeader
        title="Attendance"
        description={
          settings
            ? "See who signed in and out each day, correct times, mark absences, and set holidays and the office location that a punch must be within."
            : "See when your interns signed in and out each day, with their photo and location, and mark absences."
        }
      />
      <Tabs value={tab} onChange={setTab}>
        <TabList>
          {tabs.map((t) => (
            <Tab key={t.value} value={t.value}>
              {t.label}
            </Tab>
          ))}
        </TabList>
        <TabPanel value="daily">
          <DailyTab />
        </TabPanel>
        <TabPanel value="monthly">
          <MonthlyTab />
        </TabPanel>
        {settings ? (
          <>
            <TabPanel value="holidays">
              <HolidaysTab />
            </TabPanel>
            <TabPanel value="locations">
              <LocationsTab />
            </TabPanel>
          </>
        ) : null}
      </Tabs>
    </div>
  );
}
