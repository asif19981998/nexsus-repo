"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useSession } from "@/hooks/useApi";
import { homeFor } from "@/lib/internshipPermissions";

/** Sends the user to the first screen their permissions open. */
export default function InternshipHome() {
  const router = useRouter();
  const { can } = useSession();
  useEffect(() => {
    router.replace(homeFor(can));
  }, [router, can]);
  return null;
}
