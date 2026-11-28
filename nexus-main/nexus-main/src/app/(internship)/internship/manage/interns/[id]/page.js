"use client";

import { Suspense } from "react";
import { useParams } from "next/navigation";
import { NoAccess } from "@/components/layout/InternshipShell";
import { useSession } from "@/hooks/useApi";
import { P } from "@/lib/internshipPermissions";
import { InternDetail } from "../../_components/InternDetail";

export default function InternPage() {
  const { id } = useParams();
  const { can } = useSession();

  // The API also limits a mentor to their own interns; this only avoids a page that cannot load.
  if (!can(P.internViewAll) && !can(P.internViewAssigned)) return <NoAccess what="this intern" />;
  // InternDetail reads ?tab= and ?month=; useSearchParams needs a Suspense boundary in this Next.js version.
  return (
    <Suspense fallback={null}>
      <InternDetail key={id} id={id} />
    </Suspense>
  );
}
