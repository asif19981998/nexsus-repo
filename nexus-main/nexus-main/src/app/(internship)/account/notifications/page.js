"use client";

import Link from "next/link";
import { Button, EmptyState, ErrorState, PageHeader } from "@/components/ui";
import { useNotificationBadge } from "@/components/layout/InternshipShell";
import { useAction, useApiQuery } from "@/hooks/useApi";
import { api } from "@/lib/api";
import { formatDate } from "@/lib/formatters";
import { cn } from "@/lib/cn";

export default function NotificationsPage() {
  const { data, error, reload } = useApiQuery("/notifications");
  const badge = useNotificationBadge();
  const { run, busy } = useAction();

  const refresh = () => {
    reload();
    badge.refresh();
  };

  const markRead = async (n) => {
    if (n.isRead) return;
    if (await run(() => api.post(`/notifications/${n.id}/read`))) refresh();
  };

  if (error) return <ErrorState title="Could not load notifications" description={error.message} action={<Button onClick={reload}>Try again</Button>} />;

  return (
    <div className="space-y-4">
      <PageHeader
        title="Notifications"
        description={data ? `${data.unread} unread` : "Loading…"}
        actions={
          data?.unread ? (
            <Button
              variant="outline"
              disabled={busy}
              onClick={async () => (await run(() => api.post("/notifications/read-all"), "All marked read")) && refresh()}
            >
              Mark all read
            </Button>
          ) : null
        }
      />
      {data && !data.items.length ? (
        <EmptyState title="No notifications yet" description="Status changes on applications and internships appear here." />
      ) : null}
      <ul className="space-y-2">
        {(data?.items || []).map((n) => (
          <li key={n.id} className={cn("card-surface flex flex-wrap items-start justify-between gap-3 p-4", !n.isRead && "border-l-4 border-l-ocean")}>
            <div className="min-w-0">
              <p className={cn("text-sm", !n.isRead && "font-semibold")}>{n.title}</p>
              {n.body ? <p className="text-sm text-secondary">{n.body}</p> : null}
              <p className="mt-1 text-xs text-secondary">{formatDate(n.createdAt, "dd MMM yyyy, HH:mm")}</p>
            </div>
            <div className="flex gap-2">
              {n.link ? (
                <Link href={n.link} onClick={() => markRead(n)}>
                  <Button size="sm" variant="outline">
                    Open
                  </Button>
                </Link>
              ) : null}
              {!n.isRead ? (
                <Button size="sm" variant="ghost" disabled={busy} onClick={() => markRead(n)}>
                  Mark read
                </Button>
              ) : null}
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
}
