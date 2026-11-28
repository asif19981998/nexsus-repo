"use client";

import { useParams } from "next/navigation";
import { NoAccess } from "@/components/layout/InternshipShell";
import { useSession } from "@/hooks/useApi";
import { P } from "@/lib/internshipPermissions";
import { BatchDetail } from "../../_components/BatchDetail";

export default function BatchPage() {
  const { id } = useParams();
  const { can } = useSession();

  if (!can(P.batchView)) return <NoAccess what="this batch" />;
  return <BatchDetail key={id} id={id} />;
}
