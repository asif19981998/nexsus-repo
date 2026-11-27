"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { FileText, UserRound } from "lucide-react";
import { cn } from "@/lib/cn";
import { PageHeader } from "@/components/ui";
import { useSession } from "@/hooks/useApi";
import { isApplicant, isTrainer } from "@/lib/internshipPermissions";

const TABS = [
  { href: "/account/profile", label: "Details", icon: UserRound },
  { href: "/account/documents", label: "Documents", icon: FileText },
];

/**
 * "My profile" title and tab bar. Tabs are links, so each one can be opened or shared directly. Staff do not apply to
 * programmes, so they get the title alone: no documents tab.
 */
export function ProfileHeader() {
  const pathname = usePathname();
  const { can } = useSession();
  if (!isApplicant(can)) return <PageHeader title="My profile" description="Your account details." />;
  return (
    <>
      <PageHeader
        title="My profile"
        description={isTrainer(can) ? "Your workplace and the documents your Soft Skill trainer registration uses." : "Used by every programme you apply to."}
      />
      <nav aria-label="My profile" className="flex flex-wrap gap-1 border-b border-slate-200 dark:border-slate-700">
        {TABS.map((t) => {
          const Icon = t.icon;
          const active = pathname === t.href || pathname?.startsWith(t.href + "/");
          return (
            <Link
              key={t.href}
              href={t.href}
              aria-current={active ? "page" : undefined}
              className={cn(
                "-mb-px inline-flex items-center gap-2 border-b-2 px-3 py-2 text-sm font-medium transition-colors",
                active
                  ? "border-nexus-600 text-nexus-700 dark:text-nexus-300"
                  : "border-transparent text-slate-500 hover:text-slate-800 dark:hover:text-slate-200",
              )}
            >
              <Icon className="h-4 w-4" />
              {t.label}
            </Link>
          );
        })}
      </nav>
    </>
  );
}
