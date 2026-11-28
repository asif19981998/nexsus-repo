"use client";

import { useParams } from "next/navigation";
import { NoAccess } from "@/components/layout/InternshipShell";
import { useSession } from "@/hooks/useApi";
import { SP } from "@/lib/softskillPermissions";
import { BatchDetail } from "../../_components/BatchDetail";

export default function SoftSkillBatchPage() {
  const { id } = useParams();
  const { can } = useSession();

  if (!can(SP.batchView)) return <NoAccess what="this batch" />;
  return <BatchDetail key={id} id={id} />;
}
