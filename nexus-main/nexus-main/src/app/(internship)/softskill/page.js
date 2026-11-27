"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useSession } from "@/hooks/useApi";
import { SP, SS_MANAGE_PERMS } from "@/lib/softskillPermissions";

/** /softskill opens the screen that fits the role. */
export default function SoftSkillHome() {
  const router = useRouter();
  const { can } = useSession();
  useEffect(() => {
    router.replace(
      can(SS_MANAGE_PERMS) ? "/softskill/manage" : can(SP.trainerApply) ? "/softskill/trainer" : can(SP.monitorVisit) ? "/softskill/monitoring" : "/softskill/programme",
    );
  }, [can, router]);
  return null;
}
