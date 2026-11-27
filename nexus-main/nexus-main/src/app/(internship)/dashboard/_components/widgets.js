"use client";

import Link from "next/link";
import { useState } from "react";
import { differenceInCalendarDays, parseISO } from "date-fns";
import {
  Armchair,
  ArrowRight,
  Award,
  Bell,
  ChevronDown,
  ChevronUp,
  ClipboardList,
  FileCheck2,
  School,
  TriangleAlert,
  UserCheck,
  Users,
  Wallet,
} from "lucide-react";
import { Badge, Progress, SectionHeader, StatCard, buttonClass } from "@/components/ui";
import { cn } from "@/lib/cn";
import { formatCurrency, formatDate, formatRelative, truncate } from "@/lib/formatters";
import { HONORARIUM_TONE, TRAINER_TONE, VISIT_TONE } from "@/lib/softskillPermissions";

// ---------- building blocks ----------

/** A titled card with an optional "Open" link in the corner. */
export function Panel({ title, description, href, linkLabel = "Open", children, className }) {
  return (
    <section className={cn("card-surface p-5 text-sm", className)}>
      <SectionHeader
        title={title}
        description={description}
        className="mb-3"
        actions={
          href ? (
            <Link href={href} className={buttonClass({ size: "sm", variant: "ghost" })}>
              {linkLabel} <ArrowRight className="h-3.5 w-3.5" />
            </Link>
          ) : null
        }
      />
      {children}
    </section>
  );
}

/**
 * Keeps a long list from stretching its card: past `limit` rows the list gets a fixed height and scrolls inside,
 * and a footer button opens it to full length (and back).
 */
export function ScrollList({ count, limit = 5, height = "max-h-72", children }) {
  const [open, setOpen] = useState(false);
  const long = count > limit;
  return (
    <>
      <div
        className={cn(
          long && !open && cn(height, "overflow-y-auto overscroll-contain pr-2 [scrollbar-width:thin]"),
        )}
      >
        {children}
      </div>
      {long ? (
        <button
          type="button"
          onClick={() => setOpen((o) => !o)}
          aria-expanded={open}
          className="mt-3 inline-flex w-full items-center justify-center gap-1.5 rounded-lg border border-slate-200 py-1.5 text-xs font-semibold text-nexus-700 hover:bg-nexus-50 dark:border-nexus-700 dark:text-nexus-200 dark:hover:bg-nexus-800"
        >
          {open ? (
            <>
              Show less <ChevronUp className="h-3.5 w-3.5" />
            </>
          ) : (
            <>
              Show all ({count}) <ChevronDown className="h-3.5 w-3.5" />
            </>
          )}
        </button>
      ) : null}
    </>
  );
}

/**
 * Counts by category as thin horizontal bars, kept in the order the API returns (the pipeline order).
 * One series, so one hue and no legend; the number sits beside each bar in text ink, which doubles as the table view.
 */
export function BarList({ rows: all, empty = "Nothing to show yet." }) {
  // Statuses with nothing in them are noise here; the footnote says how many were left out.
  const rows = (all || []).filter((r) => r.count > 0);
  const hidden = (all?.length || 0) - rows.length;
  if (!rows.length) return <p className="text-secondary">{empty}</p>;
  const max = Math.max(1, ...rows.map((r) => r.count));
  return (
    <>
    <ScrollList count={rows.length} limit={6} height="max-h-56">
    <ul className="space-y-2.5">
      {rows.map((r) => (
        <li key={r.status} className="grid grid-cols-[minmax(0,9rem)_1fr_2.5rem] items-center gap-3" title={`${r.status}: ${r.count}`}>
          <span className="truncate text-secondary">{r.status}</span>
          <span className="h-2 rounded-full bg-slate-100 dark:bg-nexus-800" aria-hidden>
            <span className="block h-full rounded-full bg-ocean dark:bg-sky" style={{ width: `${(r.count / max) * 100}%`, minWidth: r.count ? 4 : 0 }} />
          </span>
          <span className="text-right font-semibold tabular-nums">{r.count}</span>
        </li>
      ))}
    </ul>
    </ScrollList>
    {hidden ? <p className="mt-3 text-xs text-secondary">{hidden} status{hidden === 1 ? "" : "es"} with no applications hidden.</p> : null}
    </>
  );
}

function Fact({ label, children }) {
  return (
    <div className="rounded-lg bg-slate-50 px-3 py-2 dark:bg-nexus-900/40">
      <dt className="text-[11px] font-medium tracking-wide text-secondary uppercase">{label}</dt>
      <dd className="font-medium">{children}</dd>
    </div>
  );
}

const daysUntil = (date, today) => differenceInCalendarDays(parseISO(date), parseISO(today));

// ---------- needs attention ----------

/** One list of everything waiting on this person, gathered from every module they work in. */
export function ActionList({ items, className }) {
  return (
    <Panel
      title="Needs your attention"
      description={items.length ? `${items.length} item${items.length === 1 ? "" : "s"} waiting` : null}
      className={className}
    >
      {items.length ? (
        <ScrollList count={items.length} limit={6} height="max-h-80">
        <ul className="divide-y divide-slate-200 dark:divide-slate-700">
          {items.map((a) => (
            <li key={a.key} className="flex items-center justify-between gap-3 py-2.5">
              <span className="flex min-w-0 items-center gap-2.5">
                <span className={cn("h-2 w-2 shrink-0 rounded-full", a.urgent ? "bg-danger" : "bg-opportunity")} aria-hidden />
                <span className="min-w-0">
                  {a.count != null ? <strong className="mr-1.5 text-base tabular-nums">{a.count}</strong> : null}
                  {a.label}
                  {a.module ? <span className="ml-2 text-xs text-secondary">· {a.module}</span> : null}
                </span>
              </span>
              {a.href ? (
                <Link href={a.href} className={buttonClass({ size: "sm", variant: "outline" })}>
                  Open
                </Link>
              ) : null}
            </li>
          ))}
        </ul>
        </ScrollList>
      ) : (
        <p className="text-secondary">Nothing waiting on you. Everything is up to date.</p>
      )}
    </Panel>
  );
}

// ---------- staff ----------

export function InternshipStaff({ data, policy, manageHref }) {
  return (
    <div className="space-y-4">
      <h2 className="text-xs font-semibold tracking-[0.14em] text-nexus-500 uppercase">UGC Internship</h2>
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
        <StatCard label="Open batches" value={data.openBatches} hint="Accepting applications" icon={<ClipboardList className="h-5 w-5" />} tone="blue" />
        <StatCard label="In selection" value={data.inSelection} hint="Not yet decided" icon={<Users className="h-5 w-5" />} tone="violet" />
        <StatCard label="Seats left" value={data.seatsLeft} hint="Open, selection and running" icon={<Armchair className="h-5 w-5" />} tone="teal" />
        <StatCard label="Active interns" value={data.activeInterns} hint="Currently placed" icon={<UserCheck className="h-5 w-5" />} tone="green" />
        <StatCard
          label="Attendance risk"
          value={data.atRisk.length}
          hint={`Below ${policy.minAttendancePct}%`}
          icon={<TriangleAlert className="h-5 w-5" />}
          tone={data.atRisk.length ? "red" : "amber"}
        />
      </div>
      {/* The at-risk list only appears when someone is at risk; the stat card above already says "0". */}
      <div className={cn("grid gap-4", data.atRisk.length && "lg:grid-cols-2")}>
        <Panel title="Intake funnel" description="Applications by status, all batches" href={manageHref("applications")}>
          <BarList rows={data.funnel} empty="No applications yet." />
        </Panel>
        {data.atRisk.length ? (
          <Panel title="Attendance below minimum" description={`Interns under ${policy.minAttendancePct}% (cl.11 ঞ)`} href="/internship/attendance" linkLabel="Attendance">
            <ScrollList count={data.atRisk.length} limit={5} height="max-h-56">
            <ul className="space-y-2.5">
              {data.atRisk.map((r) => (
                <li key={r.name}>
                  <div className="mb-1 flex justify-between gap-2">
                    <span className="truncate">{r.name}</span>
                    <span className="font-semibold text-danger tabular-nums">{r.pct}%</span>
                  </div>
                  <Progress value={r.pct} />
                </li>
              ))}
            </ul>
            </ScrollList>
          </Panel>
        ) : null}
      </div>
    </div>
  );
}

export function SoftSkillStaff({ data, manageHref }) {
  return (
    <div className="space-y-4">
      <h2 className="text-xs font-semibold tracking-[0.14em] text-nexus-500 uppercase">Soft Skill Training</h2>
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
        <StatCard label="Open batches" value={data.openBatches} hint={`${data.seatsLeft} seats left`} icon={<ClipboardList className="h-5 w-5" />} tone="teal" />
        <StatCard label="University groups" value={data.universityGroups} hint="Open, selection or running" icon={<School className="h-5 w-5" />} tone="blue" />
        <StatCard label="In training" value={data.activeTrainees} hint="Trainees" icon={<Users className="h-5 w-5" />} tone="violet" />
        <StatCard label="Stipends ready" value={data.stipendsReady} hint="Not yet paid" icon={<Wallet className="h-5 w-5" />} tone="green" />
        <StatCard label="Certificates" value={data.certified} hint="Issued" icon={<Award className="h-5 w-5" />} tone="amber" />
      </div>
      {data.funnel.some((f) => f.count > 0) ? (
        <Panel title="Applications by status" description="All Soft Skill batches" href={manageHref("applications")}>
          <BarList rows={data.funnel} />
        </Panel>
      ) : null}
    </div>
  );
}

/** Mentor's own interns (cl.14): attendance and what each one is waiting for. */
export function MyInterns({ rows }) {
  return (
    <Panel title="My interns" description="Active interns under your supervision" href="/internship/manage?tab=interns">
      {rows.length ? (
        <ScrollList count={rows.length}>
        <ul className="divide-y divide-slate-200 dark:divide-slate-700">
          {rows.map((x) => (
            <li key={x.id} className="flex flex-wrap items-center justify-between gap-3 py-2.5">
              <div className="min-w-0">
                <Link href={`/internship/manage/interns/${x.id}`} className="font-medium hover:underline">
                  {x.student.name}
                </Link>
                <p className="text-xs text-secondary">
                  {x.department} · ends {formatDate(x.endDate)}
                </p>
              </div>
              <div className="flex flex-wrap items-center gap-1.5">
                <Badge tone={x.belowMin ? "red" : "green"}>{x.attendancePct}% attendance</Badge>
                {x.pendingLeave ? <Badge tone="amber">{x.pendingLeave} leave to decide</Badge> : null}
                {x.monthlyToEvaluate ? <Badge tone="blue">{x.monthlyToEvaluate} month to evaluate</Badge> : null}
                {x.reportSubmitted ? <Badge tone="teal">Final report in</Badge> : null}
              </div>
            </li>
          ))}
        </ul>
        </ScrollList>
      ) : (
        <p className="text-secondary">No active interns are assigned to you.</p>
      )}
    </Panel>
  );
}

export function ReviewQueue({ rows }) {
  return (
    <Panel title="Documents to verify" description="People waiting for a document review" href="/internship/users">
      {rows.length ? (
        <ScrollList count={rows.length}>
        <ul className="divide-y divide-slate-200 dark:divide-slate-700">
          {rows.map((u) => (
            <li key={u.id} className="flex items-center justify-between gap-3 py-2.5">
              <div className="min-w-0">
                <p className="truncate font-medium">{u.fullName}</p>
                <p className="truncate text-xs text-secondary">{[u.role, u.university].filter(Boolean).join(" · ")}</p>
              </div>
              <div className="shrink-0 text-right">
                <Badge tone="amber">{u.documentsToReview} to review</Badge>
                {u.waitingSince ? <p className="mt-0.5 text-[11px] text-secondary">waiting {formatRelative(u.waitingSince)}</p> : null}
              </div>
            </li>
          ))}
        </ul>
        </ScrollList>
      ) : (
        <p className="text-secondary">No documents waiting for review.</p>
      )}
    </Panel>
  );
}

// ---------- trainer / monitor / documents ----------

export function TrainerCard({ me, today }) {
  const reg = me.registration;
  const groupName = (id) => me.universityGroups.find((g) => g.id === id)?.university.shortName || me.universityGroups.find((g) => g.id === id)?.university.name;
  const upcoming = me.upcomingSessions.filter((s) => s.date >= today).slice(0, 4);
  return (
    <Panel title="My training work" description="Soft Skill trainer (university teacher)" href="/softskill/trainer">
      <div className="mb-3 flex flex-wrap items-center gap-2">
        {reg ? <Badge tone={TRAINER_TONE[reg.status] || "slate"}>Registration: {reg.status}</Badge> : <Badge tone="amber">Not registered</Badge>}
        {reg ? <Badge tone={reg.totCompleted ? "green" : "amber"}>{reg.totCompleted ? "ToT completed" : "ToT pending"}</Badge> : null}
        <Badge tone="blue">
          {me.universityGroups.length} university group{me.universityGroups.length === 1 ? "" : "s"}
        </Badge>
      </div>
      <p className="mb-2 text-xs font-semibold tracking-wide text-secondary uppercase">Upcoming sessions</p>
      {upcoming.length ? (
        <ul className="divide-y divide-slate-200 dark:divide-slate-700">
          {upcoming.map((s) => (
            <li key={s.id} className="flex items-center justify-between gap-3 py-2">
              <div className="min-w-0">
                <p className="truncate font-medium">{s.topic}</p>
                <p className="text-xs text-secondary">{groupName(s.universityGroupId)}</p>
              </div>
              <Link href={`/softskill/university-groups/${s.universityGroupId}`} className="shrink-0 text-right text-xs hover:underline">
                <span className="block font-medium">{formatDate(s.date, "EEE, dd MMM")}</span>
                <span className="text-secondary">
                  {s.startTime}–{s.endTime}
                </span>
              </Link>
            </li>
          ))}
        </ul>
      ) : (
        <p className="text-secondary">No sessions scheduled.</p>
      )}
    </Panel>
  );
}

export function MonitorVisits({ visits, today }) {
  const planned = visits.filter((v) => v.status === "Planned").sort((a, b) => a.plannedOn.localeCompare(b.plannedOn));
  const done = visits.filter((v) => v.status === "Submitted").length;
  return (
    <Panel title="Monitoring visits" description={`${planned.length} planned · ${done} reported`} href="/softskill/monitoring">
      {planned.length ? (
        <ScrollList count={planned.length} limit={4}>
        <ul className="divide-y divide-slate-200 dark:divide-slate-700">
          {planned.map((v) => {
            const d = daysUntil(v.plannedOn, today);
            return (
              <li key={v.id} className="flex items-center justify-between gap-3 py-2.5">
                <div className="min-w-0">
                  <p className="truncate font-medium">{v.university}</p>
                  <p className="truncate text-xs text-secondary">
                    {v.batchCode}
                    {v.session ? ` · ${v.session}` : ""}
                  </p>
                </div>
                <div className="shrink-0 text-right">
                  <p className="text-xs font-medium">{formatDate(v.plannedOn)}</p>
                  <Badge tone={d < 0 ? "red" : d <= 3 ? "amber" : VISIT_TONE[v.status]}>{d < 0 ? "Report due" : d === 0 ? "Today" : `In ${d} day${d === 1 ? "" : "s"}`}</Badge>
                </div>
              </li>
            );
          })}
        </ul>
        </ScrollList>
      ) : (
        <p className="text-secondary">No visits planned for you.</p>
      )}
    </Panel>
  );
}

export function HonorariumSummary({ rows }) {
  const sum = (status) => rows.filter((r) => r.status === status).reduce((n, r) => n + r.amount, 0);
  const rateMissing = rows.filter((r) => r.status === "Rate not set").length;
  return (
    <Panel title="My honorarium" description="Sessions, coordination and visits (cl.17)">
      <dl className="grid grid-cols-2 gap-2">
        <Fact label="Due">{formatCurrency(sum("Due"))}</Fact>
        <Fact label="Paid">{formatCurrency(sum("Paid"))}</Fact>
      </dl>
      {rateMissing ? (
        <p className="mt-3 text-xs">
          <Badge tone={HONORARIUM_TONE["Rate not set"]}>{rateMissing} waiting for a rate</Badge>
        </p>
      ) : null}
      {!rows.length ? <p className="mt-3 text-secondary">No honorarium recorded yet.</p> : null}
    </Panel>
  );
}

/** Each programme's document checklist: required lines met, and what is still missing. */
export function DocumentReadiness({ modules }) {
  return (
    <Panel title="Documents" description="Shared by every programme" href="/account/documents" linkLabel="My documents">
      {modules.length ? (
        <ul className="space-y-4">
          {modules.map((m) => {
            const required = m.rows.filter((r) => r.required);
            const met = required.filter((r) => r.met).length;
            const pct = required.length ? Math.round((met / required.length) * 100) : 100;
            return (
              <li key={m.module}>
                <div className="mb-1 flex items-center justify-between gap-2">
                  <span className="font-medium">{m.moduleName}</span>
                  {m.ready ? (
                    <Badge tone="green">
                      <FileCheck2 className="mr-1 h-3 w-3" /> Ready
                    </Badge>
                  ) : (
                    <span className="text-xs text-secondary tabular-nums">
                      {met} of {required.length} required
                    </span>
                  )}
                </div>
                <Progress value={pct} />
                {m.missing.length ? <p className="mt-1 text-xs text-opportunity-dark">Missing: {m.missing.join(", ")}</p> : null}
              </li>
            );
          })}
        </ul>
      ) : (
        <p className="text-secondary">No programme asks you for documents.</p>
      )}
    </Panel>
  );
}

export function RecentNotifications({ data }) {
  const items = data?.items || [];
  return (
    <Panel title="Notifications" description={data?.unread ? `${data.unread} unread` : "All read"} href="/account/notifications" linkLabel="All">
      {items.length ? (
        <ScrollList count={items.length} limit={5} height="max-h-80">
        <ul className="space-y-3">
          {items.map((n) => {
            const body = (
              <>
                <span className="flex items-center gap-2">
                  {!n.isRead ? <span className="h-1.5 w-1.5 shrink-0 rounded-full bg-danger" aria-label="Unread" /> : null}
                  <span className={cn("truncate", !n.isRead && "font-semibold")}>{n.title}</span>
                </span>
                {n.body ? <span className="block text-xs text-secondary">{truncate(n.body, 90)}</span> : null}
                <span className="block text-[11px] text-secondary">{formatRelative(n.createdAt)}</span>
              </>
            );
            return (
              <li key={n.id}>
                {n.link ? (
                  <Link href={n.link} className="block rounded-lg hover:bg-slate-50 dark:hover:bg-nexus-900/40">
                    {body}
                  </Link>
                ) : (
                  body
                )}
              </li>
            );
          })}
        </ul>
        </ScrollList>
      ) : (
        <p className="flex items-center gap-2 text-secondary">
          <Bell className="h-4 w-4" /> No notifications yet.
        </p>
      )}
    </Panel>
  );
}
