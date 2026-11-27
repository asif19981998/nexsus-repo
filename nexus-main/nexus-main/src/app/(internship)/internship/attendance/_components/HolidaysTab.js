"use client";

import { useState } from "react";
import { Button, DataTable, ErrorState, Input } from "@/components/ui";
import { useAction, useApiQuery } from "@/hooks/useApi";
import { api } from "@/lib/api";
import { formatDate } from "@/lib/formatters";
import { cn } from "@/lib/cn";

const WEEK = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

/** The weekly off days. Nobody signs in on them, and they are not working days for attendance or the allowance. */
function WeekendPicker() {
  const saved = useApiQuery("/attendance/admin/weekend");
  const { run, busy } = useAction();
  // Unsaved choice; null shows what is saved.
  const [draft, setDraft] = useState(null);
  const days = draft ?? saved.data?.days ?? null;

  if (saved.error) return <ErrorState title="Could not load the weekly off days" description={saved.error.message} action={<Button onClick={saved.reload}>Try again</Button>} />;
  if (!days) return <p className="text-sm text-secondary">Loading weekly off days…</p>;

  const toggle = (d) => setDraft(days.includes(d) ? days.filter((x) => x !== d) : [...days, d]);
  const changed = [...days].sort().join() !== [...saved.data.days].sort().join();
  const save = async () => {
    const ok = await run(() => api.put("/attendance/admin/weekend", { days }), "Weekly off days saved");
    if (ok) {
      setDraft(null);
      saved.reload();
    }
  };

  return (
    <div className="space-y-3 rounded-2xl border border-slate-200 bg-white p-4 dark:border-nexus-800 dark:bg-nexus-900">
      <div>
        <p className="font-medium">Weekly off days</p>
        <p className="text-sm text-secondary">Choose the days that are off every week. At least one day must stay a working day. A change applies to every day, past and future: attendance, the monthly allowance and the report deadline are counted again with the new week.</p>
      </div>
      <div className="flex flex-wrap gap-2">
        {WEEK.map((d) => {
          const on = days.includes(d);
          return (
            <button
              key={d}
              type="button"
              aria-pressed={on}
              onClick={() => toggle(d)}
              className={cn(
                "rounded-xl px-3 py-1.5 text-sm ring-1 transition-colors",
                on
                  ? "bg-slate-100 font-medium text-slate-800 ring-slate-300 dark:bg-slate-800 dark:text-slate-100 dark:ring-slate-600"
                  : "bg-white text-slate-500 ring-slate-200 hover:bg-slate-50 dark:bg-nexus-900 dark:text-slate-400 dark:ring-slate-700",
              )}
            >
              {d}
            </button>
          );
        })}
      </div>
      <div className="flex items-center gap-2">
        <Button size="sm" disabled={busy || !changed || days.length === 0 || days.length === 7} onClick={save}>
          Save
        </Button>
        {changed ? (
          <Button size="sm" variant="ghost" disabled={busy} onClick={() => setDraft(null)}>
            Cancel
          </Button>
        ) : null}
        {days.length === 0 ? <span className="text-xs text-danger">Choose at least one day.</span> : null}
        {days.length === 7 ? <span className="text-xs text-danger">At least one day must be a working day.</span> : null}
      </div>
    </div>
  );
}

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
      <WeekendPicker />
      <p className="text-sm text-secondary">On a holiday nobody needs to sign in, and the mobile app shows &quot;Holiday&quot;. Weekly off days are set above. Holidays are not working days: they do not count against attendance or the monthly allowance.</p>
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
