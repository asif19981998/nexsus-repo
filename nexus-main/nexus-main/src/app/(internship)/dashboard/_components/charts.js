"use client";

import { format, parseISO } from "date-fns";
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { cn } from "@/lib/cn";
import { formatCurrency } from "@/lib/formatters";
import { BarList, Panel, ScrollList } from "./widgets";

// Chart colours come from CSS variables set on the wrapper, so light and dark mode each get their own step
// (ocean on white, sky on the dark card) instead of one colour flipped.
const CHART_VARS =
  "[--bar:var(--color-ocean)] [--tick:#64748b] [--grid:#e2e8f0] dark:[--bar:var(--color-sky)] dark:[--tick:#94a3b8] dark:[--grid:#22456b]";

/** Batches still taking up seats; draft, completed and cancelled ones are left out. */
export const LIVE_BATCH =["Applications open", "Selection", "Running"];

const compact = new Intl.NumberFormat("en", { notation: "compact", maximumFractionDigits: 1 });

/** Counts per value of `key`, largest first. */
function countBy(rows, key) {
  const m = new Map();
  rows.forEach((r) => m.set(key(r), (m.get(key(r)) || 0) + 1));
  return [...m].map(([status, count]) => ({ status, count })).sort((a, b) => b.count - a.count);
}

// ---------- interns by department ----------

export function InternsByDepartment({ interns }) {
  return (
    <Panel title="Active interns by department" description={`${interns.length} interns placed`} href="/internship/manage?tab=interns">
      <BarList rows={countBy(interns, (x) => x.department)} empty="No active interns." />
    </Panel>
  );
}

// ---------- attendance spread ----------

/**
 * One whole (every active intern) split into three attendance bands: a tall segmented bar with a 2px gap between
 * segments, and a legend that names each band with its count, so the colour is never the only cue.
 */
export function AttendanceSpread({ interns, minPct }) {
  const bands = [
    { key: "good", label: "90% and above", tone: "bg-success", n: interns.filter((x) => x.attendancePct >= 90).length },
    { key: "ok", label: `${minPct}–89%`, tone: "bg-opportunity", n: interns.filter((x) => x.attendancePct >= minPct && x.attendancePct < 90).length },
    { key: "low", label: `Below ${minPct}%`, tone: "bg-danger", n: interns.filter((x) => x.attendancePct < minPct).length },
  ];
  const total = interns.length || 1;
  const avg = interns.length ? Math.round(interns.reduce((n, x) => n + x.attendancePct, 0) / interns.length) : 0;

  return (
    <Panel title="Attendance spread" description={`Active interns · average ${avg}%`} href="/internship/attendance" linkLabel="Attendance">
      <div className="flex h-8 gap-[2px] overflow-hidden rounded-md" role="img" aria-label={bands.map((b) => `${b.label}: ${b.n}`).join(", ")}>
        {bands
          .filter((b) => b.n)
          .map((b) => (
            <div key={b.key} className={cn(b.tone, "h-full")} style={{ width: `${(b.n / total) * 100}%` }} title={`${b.label}: ${b.n}`} />
          ))}
      </div>
      <ul className="mt-4 space-y-2">
        {bands.map((b) => (
          <li key={b.key} className="flex items-center justify-between gap-3">
            <span className="flex items-center gap-2">
              <span className={cn("h-2.5 w-2.5 rounded-sm", b.tone)} aria-hidden />
              {b.label}
            </span>
            <span className="tabular-nums">
              <strong>{b.n}</strong>
              <span className="ml-1.5 text-xs text-secondary">{Math.round((b.n / total) * 100)}%</span>
            </span>
          </li>
        ))}
      </ul>
    </Panel>
  );
}

// ---------- allowance by month ----------

function AllowanceTooltip({ active, payload }) {
  if (!active || !payload?.length) return null;
  const m = payload[0].payload;
  return (
    <div className="rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs shadow-lg dark:border-nexus-700 dark:bg-nexus-900">
      <p className="mb-1 font-semibold">{m.label}</p>
      <p>
        Total <strong className="ml-1 tabular-nums">{formatCurrency(m.total)}</strong>
      </p>
      <p className="text-secondary">
        Paid {formatCurrency(m.paid)} · Unpaid {formatCurrency(m.total - m.paid)}
      </p>
      <p className="text-secondary">{m.interns} interns</p>
    </div>
  );
}

/**
 * Allowance per month (cl.10) over the last six months, one column each. The figure is the net amount once the
 * mentor has evaluated the month and the gross before that; the tooltip splits it into paid and unpaid.
 */
export function AllowanceByMonth({ bills }) {
  const byMonth = new Map();
  bills.forEach((b) => {
    const m = byMonth.get(b.month) || { month: b.month, total: 0, paid: 0, interns: 0 };
    const amount = b.net ?? b.gross ?? 0;
    m.total += amount;
    m.interns += 1;
    if (b.status === "Paid") m.paid += amount;
    byMonth.set(b.month, m);
  });
  const data = [...byMonth.values()]
    .sort((a, b) => a.month.localeCompare(b.month))
    .slice(-6)
    .map((m) => ({ ...m, label: format(parseISO(`${m.month}-01`), "MMM yyyy") }));
  const unpaid = data.reduce((n, m) => n + m.total - m.paid, 0);

  return (
    <Panel
      title="Allowance by month"
      description={`Last ${data.length} months · ${formatCurrency(unpaid)} not yet paid`}
      href="/internship/manage?tab=stipend"
      linkLabel="Allowance"
    >
      <div className={cn("h-56", CHART_VARS)}>
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={data} margin={{ top: 8, right: 4, bottom: 0, left: 0 }}>
            <CartesianGrid vertical={false} stroke="var(--grid)" />
            <XAxis dataKey="label" tickLine={false} axisLine={false} tick={{ fill: "var(--tick)", fontSize: 12 }} />
            <YAxis tickLine={false} axisLine={false} width={44} tick={{ fill: "var(--tick)", fontSize: 12 }} tickFormatter={(v) => compact.format(v)} />
            <Tooltip content={<AllowanceTooltip />} cursor={{ fill: "var(--grid)", opacity: 0.5 }} />
            <Bar dataKey="total" fill="var(--bar)" radius={[4, 4, 0, 0]} maxBarSize={24} />
          </BarChart>
        </ResponsiveContainer>
      </div>
    </Panel>
  );
}

// ---------- seats filled ----------

/** A meter per row: name, how full it is, and taken of capacity. */
function MeterList({ rows }) {
  return (
    <ScrollList count={rows.length} limit={5} height="max-h-56">
      <ul className="space-y-3">
        {rows.map((r) => {
          const pct = r.capacity ? Math.round((r.taken / r.capacity) * 100) : 0;
          return (
            <li key={r.key} title={`${r.name}: ${r.taken} of ${r.capacity} seats`}>
              <div className="mb-1 flex items-baseline justify-between gap-2">
                <span className="min-w-0 truncate">
                  {r.name}
                  {r.sub ? <span className="ml-1.5 text-xs text-secondary">{r.sub}</span> : null}
                </span>
                <span className="shrink-0 text-xs tabular-nums">
                  <strong className="text-sm">{r.taken}</strong>
                  <span className="text-secondary"> / {r.capacity}</span>
                </span>
              </div>
              <div className="h-2 rounded-full bg-slate-100 dark:bg-nexus-800">
                <div className="h-full rounded-full bg-ocean dark:bg-sky" style={{ width: `${Math.min(100, pct)}%`, minWidth: r.taken ? 4 : 0 }} />
              </div>
            </li>
          );
        })}
      </ul>
    </ScrollList>
  );
}

export function SeatsByBatch({ batches }) {
  const rows = batches
    .filter((b) => LIVE_BATCH.includes(b.status))
    .map((b) => ({ key: b.id, name: b.name, sub: b.status, taken: b.seats.taken, capacity: b.seats.capacity }));
  if (!rows.length) return null;
  return (
    <Panel title="Seats filled" description="Internship batches in progress" href="/internship/manage?tab=batches">
      <MeterList rows={rows} />
    </Panel>
  );
}

export function SeatsByUniversity({ batches }) {
  const rows = batches
    .filter((b) => LIVE_BATCH.includes(b.status))
    .flatMap((b) => b.universityGroups)
    .map((g) => ({ key: g.id, name: g.university.shortName || g.university.name, sub: g.batchCode, taken: g.taken, capacity: g.capacity }));
  if (!rows.length) return null;
  return (
    <Panel title="Seats by university" description="Soft Skill university groups in progress" href="/softskill/manage?tab=batches">
      <MeterList rows={rows} />
    </Panel>
  );
}
