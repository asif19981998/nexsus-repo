"use client";

import { FileText } from "lucide-react";
import { Badge } from "@/components/ui";
import { useOpenDocument } from "@/hooks/useOpenDocument";

const TONE = { Verified: "green", Rejected: "red", Submitted: "amber" };

/**
 * The document versions an application was sent with; each opens in a new tab.
 * Applications from before the document library have names only, shown as plain badges.
 */
export function AttachedDocuments({ attached, names }) {
  const viewer = useOpenDocument();
  if (attached?.length) {
    return (
      <span className="flex flex-wrap gap-1.5">
        {attached.map((d) => (
          <button
            key={d.id}
            type="button"
            disabled={viewer.busy}
            onClick={() => viewer.open(d)}
            title={`${d.fileName} · version ${d.version} · ${d.status === "Submitted" ? "not reviewed yet" : d.status.toLowerCase()}`}
            className="inline-flex items-center gap-1.5 rounded-full border border-[#d5e3df] bg-white/70 px-2.5 py-1 text-xs font-medium text-nexus-800 hover:border-nexus-400 disabled:opacity-60 dark:border-nexus-700 dark:bg-nexus-900 dark:text-nexus-100"
          >
            <FileText className="h-3.5 w-3.5" />
            {d.typeName}
            <Badge tone={TONE[d.status] || "slate"}>{d.status === "Submitted" ? "To review" : d.status}</Badge>
          </button>
        ))}
      </span>
    );
  }
  if (!names?.length) return "—";
  return (
    <span className="flex flex-wrap gap-1.5">
      {names.map((n) => (
        <Badge key={n} tone="blue">
          {n}
        </Badge>
      ))}
    </span>
  );
}
