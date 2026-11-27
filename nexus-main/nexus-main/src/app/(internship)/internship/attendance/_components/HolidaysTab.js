"use client";

import { useState } from "react";
import { Button, DataTable, ErrorState, Input } from "@/components/ui";
import { useAction, useApiQuery } from "@/hooks/useApi";
import { api } from "@/lib/api";
import { formatDate } from "@/lib/formatters";

export function HolidaysTab() {
  const list = useApiQuery("/attendance/admin/holidays");
  const { run, busy } = useAction();
  const [f, setF] = useState({ date: "", name: "" });

  if (list.error) return <ErrorState title="Could not load holidays" description={list.error.message} action={<Button onClick={list.reload}>Try again</Button>} />;

  const add = async () => {
    const ok = await run(() => api.post("/attendance/admin/holidays", { date: f.date || null, name: f.name }), "Holiday added");
    if (ok) {
      setF({ date: "", name: "" });
      list.reload();
    }
  };
  const remove = async (id) => {
    const ok = await run(() => api.delete(`/attendance/admin/holidays/${id}`), "Holiday removed");
    if (ok) list.reload();
  };

  return (
    <div className="space-y-4">
      <p className="text-sm text-secondary">On a holiday nobody needs to sign in, and the mobile app shows &quot;Holiday&quot;. Friday and Saturday are weekly off already. Holidays are not working days: they do not count against attendance or the monthly allowance.</p>
      <div className="grid gap-3 sm:grid-cols-[1fr_2fr_auto] sm:items-end">
        <Input label="Date" type="date" value={f.date} onChange={(e) => setF({ ...f, date: e.target.value })} />
        <Input label="Name" placeholder="e.g. Victory Day" value={f.name} onChange={(e) => setF({ ...f, name: e.target.value })} />
        <Button disabled={busy || !f.date || !f.name.trim()} onClick={add}>
          Add holiday
        </Button>
      </div>
      <DataTable
        emptyMessage={list.data ? "No holidays yet." : "Loading…"}
        rows={list.data || []}
        columns={[
          { key: "date", label: "Date", render: (h) => formatDate(h.date, "EEE, dd MMM yyyy") },
          { key: "name", label: "Holiday", render: (h) => h.name },
          {
            key: "act",
            label: "",
            render: (h) => (
              <Button size="sm" variant="ghost" disabled={busy} onClick={() => remove(h.id)}>
                Remove
              </Button>
            ),
          },
        ]}
      />
    </div>
  );
}
