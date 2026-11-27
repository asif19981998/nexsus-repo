"use client";

import Link from "next/link";
import { PageHeader } from "@/components/ui";
import { NoAccess } from "@/components/layout/InternshipShell";
import { useSession } from "@/hooks/useApi";
import { P } from "@/lib/internshipPermissions";
import { MonthlyTab } from "../attendance/_components/MonthlyTab";

/** The intern's own register, month by month: every day with times, lateness, location and photos. */
export default function MyAttendancePage() {
  const { can } = useSession();
  if (!can(P.selfAttendance)) return <NoAccess what="My attendance" />;
  return (
    <div className="space-y-4">
      <PageHeader
        title="My attendance"
        description="Every day of your internship: when you signed in and out, how late, and the photo and place of each punch."
        actions={
          <Link href="/internship/programme" className="text-sm font-medium text-nexus-700 hover:underline dark:text-nexus-300">
            Request leave
          </Link>
        }
      />
      <MonthlyTab self />
    </div>
  );
}
