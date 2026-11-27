"use client";

import { useMemo, useState } from "react";
import { Check, Copy } from "lucide-react";
import { Badge, Button, SearchSelect, DataTable, EmptyState, ErrorState, MonthInput } from "@/components/ui";
import { useApiQuery } from "@/hooks/useApi";
import { DAY_TONE } from "@/lib/internshipPermissions";
import { formatDate } from "@/lib/formatters";
import { cn } from "@/lib/cn";
import { AbsentModal, ManualTag, Photo, TimesModal, minutes, useAttendanceRights } from "./DailyTab";

const TONE = { ...DAY_TONE, off: "slate" };
/**
 * Day colours from the site palette (success teal, opportunity gold, danger red, sky blue), so the calendar reads as
 * part of the page in both themes. Each tone is a soft fill, a matching bar along the bottom, and text in the same hue.
 */
const tone = (fill, text, bar) => ({ cell: `${fill} ${text}`, bar });
const PRESENT = tone("bg-success/10 dark:bg-success/25", "text-success dark:text-[#a8dcc9]", "bg-success dark:bg-[#6fbfa5]");
const LATE = tone("bg-opportunity/12 dark:bg-opportunity/25", "text-opportunity-dark dark:text-[#ecd09d]", "bg-opportunity dark:bg-[#d9b06a]");
const ABSENT = tone("bg-danger/10 dark:bg-danger/30", "text-danger dark:text-[#f8b4b4]", "bg-danger dark:bg-[#f07a7a]");
const LEAVE = tone("bg-sky/15 dark:bg-sky/20", "text-ocean dark:text-[#bfe0ee]", "bg-sky");
const CELL = {
  present: PRESENT,
  late: LATE,
  absent: ABSENT,
  "absent-no-signin": ABSENT,
  "absent-approved": LEAVE,
  leave: LEAVE,
  "leave-requested": LATE,
  off: tone("bg-chrome dark:bg-nexus-950/50", "text-nexus-700/40 dark:text-nexus-200/35", null),
  open: tone("bg-nexus-50 dark:bg-nexus-800/50", "text-nexus-900 dark:text-nexus-50", null),
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

function toneOf(day) {
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
      <div className="mb-2 grid grid-cols-7 gap-1.5 text-center text-[11px] font-semibold tracking-[0.08em] text-nexus-700/60 uppercase dark:text-nexus-200/60">
        {["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"].map((w) => (
          <div key={w}>{w}</div>
        ))}
      </div>
      <div className="grid grid-cols-7 gap-1.5">
        {cells.map((n, i) => {
          if (n == null) return <div key={`e${i}`} />;
          const iso = `${month}-${String(n).padStart(2, "0")}`;
          const day = byDate[iso];
          const t = day ? toneOf(day) : null;
          return (
            <div
              key={iso}
              title={
                day
                  ? `${day.manual ? "Edited by admin · " : ""}${day.label}${day.checkIn ? ` · ${day.checkIn}–${day.checkOut || "…"}` : ""}`
                  : "Outside internship"
              }
              className={cn(
                "relative flex h-14 flex-col items-center justify-center overflow-hidden rounded-xl text-sm",
                t ? t.cell : "text-nexus-700/25 dark:text-nexus-200/20",
                iso === today && "ring-2 ring-ocean dark:ring-sky",
              )}
            >
              {day?.manual ? <span className="absolute top-1.5 right-1.5 h-1.5 w-1.5 rounded-full bg-opportunity" aria-label="Edited by admin" /> : null}
              <span className="font-semibold tabular-nums">{n}</span>
              {day?.checkIn ? <span className="text-[10px] leading-none tabular-nums opacity-80">{day.checkIn}</span> : null}
              {t?.bar ? <span className={cn("absolute inset-x-0 bottom-0 h-[3px]", t.bar)} aria-hidden /> : null}
            </div>
          );
        })}
      </div>
      <div className="mt-4 flex flex-wrap gap-x-4 gap-y-2 border-t border-[#d5e3df] pt-3 text-xs text-secondary dark:border-nexus-800">
        <Legend t={CELL.present} label="On time" />
        <Legend t={CELL.late} label="Late" />
        <Legend t={CELL.absent} label="Absent" />
        <Legend t={CELL.leave} label="Leave / approved" />
        <Legend t={CELL.off} label="Off day" />
        <span className="flex items-center gap-1.5">
          <span className="h-1.5 w-1.5 rounded-full bg-opportunity" />
          Edited by admin
        </span>
        <span className="flex items-center gap-1.5">
          <span className="h-3 w-3 rounded ring-2 ring-ocean dark:ring-sky" />
          Today
        </span>
      </div>
    </div>
  );
}

const Legend = ({ t, label }) => (
  <span className="flex items-center gap-1.5">
    <span className={cn("relative h-3 w-3 overflow-hidden rounded", t.cell)}>
      {t.bar ? <span className={cn("absolute inset-x-0 bottom-0 h-[2px]", t.bar)} /> : null}
    </span>
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

          <div className="card-surface p-4">
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
                      {r.lateMinutes > 0 ? <p className="text-xs text-danger">{r.lateMinutes}m late</p> : null}
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
