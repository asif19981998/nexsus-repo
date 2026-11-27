"use client";

import { useState } from "react";
import { ShieldAlert, ShieldCheck } from "lucide-react";
import { Badge, Button, EmptyState } from "@/components/ui";
import { formatDate } from "@/lib/formatters";

/** "today", "yesterday", "5 days" since an upload. */
function waitedFor(since, now) {
  if (!since) return null;
  const days = Math.floor((now - new Date(since).getTime()) / 86400000);
  if (days <= 0) return "today";
  if (days === 1) return "since yesterday";
  return `for ${days} days`;
}

/**
 * People with uploaded files that nobody has checked yet, the longest-waiting first. Opening one shows their
 * documents; once every file is verified or rejected they leave this list.
 */
export function ReviewQueue({ users, onReview }) {
  const [now] = useState(() => Date.now());
  if (!users) return <p className="py-8 text-center text-sm text-secondary">Loading…</p>;
  if (!users.length) return <EmptyState title="Nothing waiting" description="When someone uploads a document, they show up here until you check it." />;

  const ordered = [...users].sort((a, b) => new Date(a.waitingSince || a.createdAt) - new Date(b.waitingSince || b.createdAt));
  return (
    <ul className="divide-y divide-[#d5e3df] overflow-hidden rounded-xl border border-[#d5e3df] bg-cream dark:divide-nexus-700 dark:border-nexus-700 dark:bg-nexus-900">
      {ordered.map((u) => {
        const newcomer = now - new Date(u.createdAt).getTime() < 7 * 86400000;
        const waited = waitedFor(u.waitingSince, now);
        return (
          <li key={u.id} className="flex flex-wrap items-center gap-x-6 gap-y-3 px-4 py-4">
            <div className="min-w-[220px] flex-1">
              <p className="flex flex-wrap items-center gap-2 font-medium">
                {u.fullName}
                {newcomer ? <Badge tone="blue">New sign-up</Badge> : null}
              </p>
              <p className="text-xs text-secondary">{[u.email, u.phone].filter(Boolean).join(" · ")}</p>
              <p className="mt-1 text-xs text-secondary">
                Joined {formatDate(u.createdAt)}
                {u.university ? ` · ${u.university}` : ""}
              </p>
            </div>

            <div className="w-44 text-sm">
              <p className="font-medium">
                {u.documentsToReview} of {u.documents} file{u.documents === 1 ? "" : "s"} to check
              </p>
              {waited ? <p className="text-xs text-opportunity-dark">Waiting {waited}</p> : null}
            </div>

            <p className="flex w-40 items-center gap-1.5 text-xs">
              {u.identityVerified == null ? null : u.identityVerified ? (
                <>
                  <ShieldCheck className="h-4 w-4 text-success" /> Identity verified
                </>
              ) : (
                <>
                  <ShieldAlert className="h-4 w-4 text-opportunity-dark" /> Identity not verified
                </>
              )}
            </p>

            <Button size="sm" onClick={() => onReview(u)}>
              Review files
            </Button>
          </li>
        );
      })}
    </ul>
  );
}
