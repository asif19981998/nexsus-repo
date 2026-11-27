"use client";

import { useMemo, useState } from "react";
import { Check, Copy } from "lucide-react";
import { Badge, Button, SearchSelect, DataTable, EmptyState, ErrorState, MonthInput } from "@/components/ui";
import { useApiQuery } from "@/hooks/useApi";
import { DAY_TONE } from "@/lib/internshipPermissions";
import { formatDate } from "@/lib/formatters";
import { cn } from "@/lib/cn";
import { AbsentModal, ManualTag, Photo, TimesModal, duration, minutes, useAttendanceRights } from "./DailyTab";

const TONE = { ...DAY_TONE, off: "slate" };
// Same colours the intern sees in the app calendar. Dark variants keep the same hues on the dark theme.
const CELL = {
  present: "bg-emerald-50 text-emerald-800 ring-emerald-200 dark:bg-emerald-950/40 dark:text-emerald-200 dark:ring-emerald-800",
  late: "bg-amber-50 text-amber-800 ring-amber-200 dark:bg-amber-950/40 dark:text-amber-200 dark:ring-amber-800",
  absent: "bg-red-100 text-red-800 ring-red-300 dark:bg-red-950/50 dark:text-red-200 dark:ring-red-800",
  "absent-no-signin": "bg-red-100 text-red-800 ring-red-300 dark:bg-red-950/50 dark:text-red-200 dark:ring-red-800",
  "absent-approved": "bg-blue-50 text-blue-800 ring-blue-200 dark:bg-blue-950/40 dark:text-blue-200 dark:ring-blue-800",
  leave: "bg-blue-50 text-blue-800 ring-blue-200 dark:bg-blue-950/40 dark:text-blue-200 dark:ring-blue-800",
  "leave-requested": "bg-amber-50 text-amber-800 ring-amber-200 dark:bg-amber-950/40 dark:text-amber-200 dark:ring-amber-800",
  off: "bg-slate-50 text-slate-400 ring-slate-200 dark:bg-slate-900/50 dark:text-slate-500 dark:ring-slate-700",
  open: "bg-white text-slate-600 ring-slate-200 dark:bg-nexus-900 dark:text-slate-300 dark:ring-slate-700",
};

/** "lat, lng" with a copy button: pastes straight into the Google Maps search box. */
function Coords({ lat, lng }) {
  const [copied, setCopied] = useState(false);
  if (lat == null || lng == null) return null;
  const text = `${lat.toFixed(6)}, ${lng.toFixed(6)}`;
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(text);
    } catch {
      // Clipboard needs https or localhost; fall back for other origins.
      const el = document.createElement("textarea");
      el.value = text;
      document.body.appendChild(el);
      el.select();
      document.execCommand("copy");
      el.remove();
    }
    setCopied(true);
    setTimeout(() => setCopied(false), 1500);
  };
  return (
    <p className="mt-0.5 flex items-center gap-1 text-[11px] text-secondary">
      <span className="font-mono">{text}</span>
      <button
        type="button"
        onClick={copy}
        title={copied ? "Copied" : "Copy location"}
        aria-label={copied ? "Copied" : "Copy location"}
        className="inline-flex h-5 w-5 items-center justify-center rounded text-slate-400 hover:bg-slate-100 hover:text-nexus-700 dark:hover:bg-nexus-800 dark:hover:text-nexus-200"
      >
        {copied ? <Check className="h-3 w-3 text-success" /> : <Copy className="h-3 w-3" />}
      </button>
    </p>
  );
}

const thisMonth = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}`;
};

function cellClass(day) {
  if (day.kind === "present" && day.lateMinutes > 0) return CELL.late;
  return CELL[day.kind] || CELL.open;
}

const todayIso = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
};

function Calendar({ month, days }) {
  const byDate = useMemo(() => Object.fromEntries(days.map((d) => [d.date, d])), [days]);
  const [today] = useState(todayIso);
  const [y, m] = month.split("-").map(Number);
  const count = new Date(y, m, 0).getDate();
  const lead = new Date(y, m - 1, 1).getDay(); // Sunday first
  const cells = [...Array(lead).fill(null), ...Array.from({ length: count }, (_, i) => i + 1)];

  return (
    <div>
      <div className="mb-1 grid grid-cols-7 gap-1.5 text-center text-xs text-secondary">
        {["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"].map((w) => (
          <div key={w}>{w}</div>
        ))}
      </div>
      <div className="grid grid-cols-7 gap-1.5">
        {cells.map((n, i) => {
          if (n == null) return <div key={`e${i}`} />;
          const iso = `${month}-${String(n).padStart(2, "0")}`;
          const day = byDate[iso];
          return (
            <div
              key={iso}
              title={
                day
                  ? `${day.manual ? "Edited by admin · " : ""}${day.label}${day.checkIn ? ` · ${day.checkIn}–${day.checkOut || "…"}` : ""}`
                  : "Outside internship"
              }
              className={cn(
                "relative flex h-14 flex-col items-center justify-center rounded-xl text-sm ring-1",
                day ? cellClass(day) : "bg-transparent text-slate-300 ring-transparent dark:text-slate-600",
                iso === today && "ring-2 ring-nexus-600 dark:ring-nexus-300",
              )}
            >
              {day?.manual ? <span className="absolute top-1 right-1.5 h-1.5 w-1.5 rounded-full bg-amber-500" aria-label="Edited by admin" /> : null}
              <span className="font-semibold">{n}</span>
              {day?.checkIn ? <span className="text-[10px] leading-none">{day.checkIn}</span> : null}
            </div>
          );
        })}
      </div>
      <div className="mt-3 flex flex-wrap gap-3 text-xs text-secondary">
        <Legend cls={CELL.present} label="On time" />
        <Legend cls={CELL.late} label="Late" />
        <Legend cls={CELL.absent} label="Absent" />
        <Legend cls={CELL.leave} label="Leave / approved" />
        <Legend cls={CELL.off} label="Off day" />
        <span className="flex items-center gap-1.5">
          <span className="h-1.5 w-1.5 rounded-full bg-amber-500" />
          Edited by admin
        </span>
        <span className="flex items-center gap-1.5">
          <span className="h-3 w-3 rounded ring-2 ring-nexus-600 dark:ring-nexus-300" />
          Today
        </span>
      </div>
    </div>
  );
}

const Legend = ({ cls, label }) => (
  <span className="flex items-center gap-1.5">
    <span className={`h-3 w-3 rounded ring-1 ${cls}`} />
    {label}
  </span>
);

/**
 * One intern's month: calendar, then every day with times, lateness, location and photos.
 * With `applicationId` it shows that intern only (the intern's own page) and calls `onChange` after an edit.
 * With `self` it is the signed-in intern's own register: read only, from /attendance/me/month.
 * `compact` shows the calendar and totals without the day-by-day table.
 */
export function MonthlyTab({ applicationId, onChange, self = false, compact = false } = {}) {
  const fixed = applicationId != null || self;
  const interns = useApiQuery(fixed ? null : "/internship/interns?status=all");
  const staffRights = useAttendanceRights();
  const rights = self ? { editTimes: false, markAbsent: false } : staffRights;
  const [internId, setInternId] = useState("");
  const [month, setMonth] = useState(thisMonth());
  const [editing, setEditing] = useState(null);

  const options = useMemo(
    () =>
      (interns.data || [])
        .filter((i) => i.status === "Active" || i.status === "Completed" || i.status === "Certified")
        .map((i) => ({ value: String(i.id), label: i.student.name, sub: `${i.student.email || "no email"} · ${i.department}` })),
    [interns.data],
  );
  const selected = fixed ? String(applicationId) : internId || options[0]?.value || "";
  const data = useApiQuery(self ? `/attendance/me/month?month=${month}` : selected ? `/attendance/admin/interns/${selected}?month=${month}` : null);
  const d = data.data;
  const reload = () => {
    data.reload();
    onChange?.();
  };

  const rows = useMemo(() => (d?.days || []).map((r) => ({ ...r, id: r.date })), [d]);

  return (
    <div className="space-y-4">
      {fixed ? (
        <div className="max-w-xs">
          <MonthInput label="Month" max={thisMonth()} value={month} onChange={(e) => e.target.value && setMonth(e.target.value)} />
        </div>
      ) : (
        <div className="grid gap-3 sm:grid-cols-[2fr_1fr]">
          <SearchSelect
            label="Intern"
            value={selected}
            onChange={setInternId}
            options={options}
            placeholder="Search by name or email"
            emptyText="No intern matches"
          />
          <MonthInput label="Month" max={thisMonth()} value={month} onChange={(e) => e.target.value && setMonth(e.target.value)} />
        </div>
      )}

      {interns.error ? (
        <ErrorState title="Could not load interns" description={interns.error.message} action={<Button onClick={interns.reload}>Try again</Button>} />
      ) : null}
      {!fixed && !interns.error && interns.data && !options.length ? (
        <EmptyState title="No interns yet" description="Interns appear here once they are active." />
      ) : null}
      {data.error && self && data.error.code === "ATTENDANCE_NOT_INTERNING" ? (
        <EmptyState title="No attendance yet" description={data.error.message} />
      ) : data.error ? (
        <ErrorState title="Could not load this month" description={data.error.message} action={<Button onClick={data.reload}>Try again</Button>} />
      ) : null}

      {d ? (
        <>
          <div className="flex flex-wrap items-center gap-2 text-sm">
            {fixed ? null : <span className="font-medium">{d.name}</span>}
            <span className="text-secondary">
              {d.department} · shift {d.shiftStart}–{d.shiftEnd}
            </span>
          </div>
          <div className="flex flex-wrap gap-2 text-sm">
            <Badge tone="green" className="rounded-md">
              Present {d.present}
            </Badge>
            <Badge tone="amber" className="rounded-md">
              Late {d.late}
            </Badge>
            <Badge tone="red" className="rounded-md">
              Absent {d.absent}
            </Badge>
            <Badge tone="blue" className="rounded-md">
              Leave {d.leave}
            </Badge>
            <Badge tone="slate" className="rounded-md">
              Working days so far {d.workingDays}
            </Badge>
          </div>

          <div className="rounded-2xl border border-slate-200 bg-white p-4 dark:border-nexus-800 dark:bg-nexus-900">
            <Calendar month={d.month} days={d.days} />
          </div>

          {compact ? null : (
            <DataTable
              emptyMessage={self ? "Your internship has no days in this month." : "No days in this month for this intern."}
              rows={rows}
              columns={[
                {
                  key: "date",
                  label: "Date",
                  render: (r) => (
                    <div>
                      <p className="font-medium">{formatDate(r.date, "EEE, dd MMM")}</p>
                      {r.dayType !== "WORKING" ? <p className="text-xs text-secondary">{r.holidayName || "Weekly off"}</p> : null}
                    </div>
                  ),
                },
                {
                  key: "status",
                  label: "Status",
                  render: (r) => (
                    <div className="flex items-center">
                      <Badge tone={TONE[r.kind] || "slate"}>{r.label}</Badge>
                      <ManualTag row={r} />
                    </div>
                  ),
                },
                {
                  key: "in",
                  label: "Check in",
                  render: (r) => (
                    <div>
                      <p>{r.checkIn || "—"}</p>
                      {r.lateMinutes > 0 ? <p className="text-xs text-danger">{duration(r.lateMinutes)} late</p> : null}
                      <Coords lat={r.checkInLatitude} lng={r.checkInLongitude} />
                    </div>
                  ),
                },
                {
                  key: "out",
                  label: "Check out",
                  render: (r) => (
                    <div>
                      <p>{r.checkOut || "—"}</p>
                      <Coords lat={r.checkOutLatitude} lng={r.checkOutLongitude} />
                    </div>
                  ),
                },
                { key: "worked", label: "Worked", render: (r) => minutes(r.workedMinutes) },
                {
                  key: "photos",
                  label: "Photos",
                  render: (r) =>
                    r.checkInPhoto || r.checkOutPhoto ? (
                      <div className="flex gap-1.5">
                        <Photo path={r.checkInPhoto} label="Check in" />
                        <Photo path={r.checkOutPhoto} label="Check out" />
                      </div>
                    ) : (
                      "—"
                    ),
                },
                {
                  key: "act",
                  label: "",
                  render: (r) =>
                    r.kind !== "off" && r.label !== "Upcoming" && rights.markAbsent ? (
                      <div className="flex gap-2">
                        {rights.editTimes ? (
                          <Button size="sm" variant="outline" onClick={() => setEditing({ row: r, mode: "times" })}>
                            Edit times
                          </Button>
                        ) : null}
                        <Button size="sm" variant="ghost" onClick={() => setEditing({ row: r, mode: "absent" })}>
                          Mark absent
                        </Button>
                      </div>
                    ) : null,
                },
              ]}
            />
          )}
        </>
      ) : null}

      {editing
        ? (() => {
            const row = { name: d.name, applicationId: d.applicationId, checkIn: editing.row.checkIn, checkOut: editing.row.checkOut };
            const Modal = editing.mode === "times" ? TimesModal : AbsentModal;
            return <Modal row={row} date={editing.row.date} onClose={() => setEditing(null)} onDone={reload} />;
          })()
        : null}
    </div>
  );
}
