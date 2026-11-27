"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { Suspense, useMemo } from "react";
import { differenceInCalendarDays, parseISO } from "date-fns";
import { Eye } from "lucide-react";
import { Button, ErrorState, PageHeader, buttonClass } from "@/components/ui";
import { useSetup } from "@/components/layout/InternshipShell";
import { useApiQuery, useSession } from "@/hooks/useApi";
import { cn } from "@/lib/cn";
import { formatDate } from "@/lib/formatters";
import { MANAGE_TABS, P, allowed, isApplicant, roleLabel } from "@/lib/internshipPermissions";
import { SP, SS_MANAGE_TABS } from "@/lib/softskillPermissions";
import {
  ActionList,
  DocumentReadiness,
  HonorariumSummary,
  InternshipStaff,
  MonitorVisits,
  MyInterns,
  RecentNotifications,
  ReviewQueue,
  SoftSkillStaff,
  TrainerCard,
} from "./_components/widgets";
import { PREVIEW_ENABLED, sampleData } from "./_components/sampleData";
import {
  AllowanceByMonth,
  AttendanceSpread,
  InternsByDepartment,
  LIVE_BATCH,
  SeatsByBatch,
  SeatsByUniversity,
} from "./_components/charts";

/** Days before a report deadline when it starts showing under "Needs your attention". */
const DEADLINE_WARN_DAYS = 14;

const greeting = (hour) => (hour < 12 ? "Good morning" : hour < 17 ? "Good afternoon" : "Good evening");

/**
 * The landing page after sign-in, for every role. Each block loads only when the person's permissions open its API,
 * so a student sees their deadlines and documents, a mentor their interns, a trainer their sessions and Commission staff the totals.
 * "Needs your attention" gathers the open items from all of them in one list.
 */
export default function DashboardPage() {
  // useSearchParams needs a Suspense boundary in this Next.js version.
  return (
    <Suspense fallback={null}>
      <Dashboard />
    </Suspense>
  );
}

function Dashboard() {
  const { user, can } = useSession();
  const setup = useSetup();
  const today = setup.today;
  const params = useSearchParams();
  const preview = PREVIEW_ENABLED && params.get("preview") === "1";

  const applicant = isApplicant(can);
  const mentorOnly = can(P.internViewAssigned) && !can(P.internViewAll);
  const isTrainer = can(SP.trainerApply);
  const isMonitor = can(SP.monitorVisit);

  const live = {
    notes: useApiQuery("/notifications"),
    requirements: useApiQuery(applicant ? "/me/requirements" : null),
    internship: useApiQuery(can(P.selfView) ? "/internship/me/application" : null),
    softskill: useApiQuery(can(SP.selfView) ? "/softskill/me" : null),
    trainer: useApiQuery(isTrainer ? "/softskill/trainers/me" : null),
    visits: useApiQuery(isMonitor ? "/softskill/monitoring/visits/mine" : null),
    honoraria: useApiQuery(isTrainer || isMonitor ? "/softskill/honoraria/me" : null),
    internOverview: useApiQuery(can(P.overviewView) ? "/internship/overview" : null),
    ssOverview: useApiQuery(can(SP.overviewView) ? "/softskill/overview" : null),
    myInterns: useApiQuery(mentorOnly ? "/internship/interns?status=Active" : null),
    reviewQueue: useApiQuery(can(P.usersView) ? "/users?toReview=true" : null),
    // Chart sources for Commission staff.
    internsAll: useApiQuery(can(P.internViewAll) ? "/internship/interns?status=Active" : null),
    bills: useApiQuery(can(P.billView) ? "/internship/bills" : null),
    batches: useApiQuery(can(P.batchView) ? "/internship/batches" : null),
    ssBatches: useApiQuery(can(SP.batchView) ? "/softskill/batches" : null),
  };

  // Design preview: every block filled with sample data and shown whatever the role; the API is still only
  // called for what the person may open, and the sample replaces its answers.
  const sample = useMemo(() => (preview ? sampleData(today) : null), [preview, today]);
  const q = preview ? Object.fromEntries(Object.keys(live).map((k) => [k, { data: sample[k], error: null, reload: () => {} }])) : live;
  const show = preview ? () => true : can;

  const failed = Object.entries(q).filter(([, v]) => v.error);
  const retry = () => failed.forEach(([, v]) => v.reload());

  // Links into the admin tabs, only when the person may open that tab.
  const internTab = (tab) => (allowed(show, MANAGE_TABS.find((t) => t.value === tab)?.perm) ? `/internship/manage?tab=${tab}` : null);
  const ssTab = (tab) => (allowed(show, SS_MANAGE_TABS.find((t) => t.value === tab)?.perm) ? `/softskill/manage?tab=${tab}` : null);

  const actions = buildActions({ q, today, can: show, internTab, ssTab });

  const internsAll = q.internsAll.data || [];
  const bills = (q.bills.data || []).filter((b) => b.status !== "Accruing");
  const liveBatches = (q.batches.data || []).filter((b) => LIVE_BATCH.includes(b.status));
  const ssLive = (q.ssBatches.data || []).filter((b) => LIVE_BATCH.includes(b.status) && b.universityGroups?.length);
  const myInterns = q.myInterns.data || [];
  const toReview = q.reviewQueue.data || [];
  const trainer = show(SP.trainerApply) ? q.trainer.data : null;

  const hour = new Date().getHours();

  return (
    <div className="space-y-6">
      <PageHeader
        title={`${greeting(hour)}, ${user.fullName.split(" ")[0]}`}
        description={`${formatDate(today, "EEEE, dd MMMM yyyy")} · ${[roleLabel(user.role), user.designation, user.department].filter(Boolean).join(" · ")}`}
        actions={
          PREVIEW_ENABLED ? (
            <Link href={preview ? "/dashboard" : "/dashboard?preview=1"} className={buttonClass({ size: "sm", variant: "outline" })}>
              <Eye className="h-4 w-4" /> {preview ? "Exit preview" : "Preview with sample data"}
            </Link>
          ) : null
        }
      />

      {preview ? (
        <div className="rounded-xl border border-opportunity/40 bg-[#f7f1e6] px-4 py-3 text-sm text-opportunity-dark dark:bg-opportunity/10">
          <strong>Preview: sample data.</strong> Every card is filled with made-up records to show the design. Nothing here comes from or goes to
          the database, and this mode is off in production.
        </div>
      ) : null}

      {failed.length ? (
        <ErrorState
          title="Part of the dashboard could not load"
          description={failed.map(([, v]) => v.error.message).filter((m, i, all) => all.indexOf(m) === i).join(" ")}
          action={<Button onClick={retry}>Try again</Button>}
        />
      ) : null}

      <div className="grid gap-6 lg:grid-cols-3">
        {/* The action list stretches to the notifications' height, so the row has no gap. */}
        <div className="lg:col-span-2">
          <ActionList items={actions} className="h-full" />
        </div>
        <RecentNotifications data={q.notes.data} />
      </div>

      {q.internOverview.data ? <InternshipStaff data={q.internOverview.data} policy={setup.policy} manageHref={internTab} /> : null}

      {/* Internship charts: each shows only when its data has something in it. */}
      {bills.length || liveBatches.length ? (
        <div className={cn("grid gap-6", bills.length && liveBatches.length && "lg:grid-cols-3")}>
          {bills.length ? (
            <div className={cn(liveBatches.length && "lg:col-span-2")}>
              <AllowanceByMonth bills={bills} />
            </div>
          ) : null}
          {liveBatches.length ? <SeatsByBatch batches={liveBatches} /> : null}
        </div>
      ) : null}
      {internsAll.length ? (
        <div className="grid gap-6 lg:grid-cols-2">
          <InternsByDepartment interns={internsAll} />
          <AttendanceSpread interns={internsAll} minPct={setup.policy.minAttendancePct} />
        </div>
      ) : null}

      {q.ssOverview.data ? <SoftSkillStaff data={q.ssOverview.data} manageHref={ssTab} /> : null}
      {ssLive.length ? <SeatsByUniversity batches={ssLive} /> : null}

      {/* Lists show only when they have rows; an empty one is already "nothing waiting" in the action list. */}
      {myInterns.length || toReview.length ? (
        <div className={cn("grid gap-6", myInterns.length && toReview.length && "lg:grid-cols-2")}>
          {myInterns.length ? <MyInterns rows={myInterns} /> : null}
          {toReview.length ? <ReviewQueue rows={toReview} /> : null}
        </div>
      ) : null}

      {/* A student's programmes have their own pages; their deadlines reach the action list above. */}
      {trainer ? <TrainerCard me={trainer} today={today} /> : null}

      {q.visits.data || q.honoraria.data || q.requirements.data ? (
        <div className="grid gap-6 lg:grid-cols-3">
          {q.visits.data ? <MonitorVisits visits={q.visits.data} today={today} /> : null}
          {q.honoraria.data ? <HonorariumSummary rows={q.honoraria.data} /> : null}
          {q.requirements.data ? <DocumentReadiness modules={q.requirements.data} /> : null}
        </div>
      ) : null}
    </div>
  );
}

/** Everything waiting on this person, most urgent first. */
function buildActions({ q, today, can, internTab, ssTab }) {
  const items = [];
  const add = (item) => items.push(item);
  const daysTo = (date) => differenceInCalendarDays(parseISO(date), parseISO(today));

  // Commission staff
  q.internOverview.data?.actions.forEach((a) =>
    add({ key: `int-${a.tab}-${a.label}`, count: a.count, label: a.label, module: "Internship", href: internTab(a.tab) })
  );
  if (q.internOverview.data?.atRisk.length)
    add({ key: "int-risk", count: q.internOverview.data.atRisk.length, label: "interns below minimum attendance", module: "Internship", href: "/internship/attendance", urgent: true });

  const ss = q.ssOverview.data;
  if (ss) {
    [
      [ss.applicationsToScreen, "applications to screen", "applications"],
      [ss.trainersPending, "trainer registrations waiting", "trainers"],
      [ss.stipendsReady, "stipends ready to pay", "stipends"],
    ]
      .filter(([n]) => n > 0)
      .forEach(([n, label, tab]) => add({ key: `ss-${tab}`, count: n, label, module: "Soft Skill", href: ssTab(tab) }));
  }

  const review = q.reviewQueue.data;
  if (review?.length) add({ key: "users-review", count: review.length, label: "people with documents to verify", module: "Users", href: "/internship/users" });

  // Mentor
  const interns = q.myInterns.data || [];
  const leave = interns.reduce((n, x) => n + x.pendingLeave, 0);
  const months = interns.reduce((n, x) => n + x.monthlyToEvaluate, 0);
  const low = interns.filter((x) => x.belowMin).length;
  if (leave) add({ key: "m-leave", count: leave, label: "leave requests to decide", module: "My interns", href: "/internship/manage?tab=interns" });
  if (months) add({ key: "m-months", count: months, label: "monthly evaluations due", module: "My interns", href: "/internship/manage?tab=interns" });
  if (low) add({ key: "m-low", count: low, label: "of your interns below minimum attendance", module: "My interns", href: "/internship/attendance", urgent: true });

  // Intern
  const i = q.internship.data?.internship;
  if (i && can(P.selfAttendance) && i.today.inPeriod && !i.today.record)
    add({ key: "i-today", label: "Sign in for today's attendance", module: "Internship", href: "/internship/my-attendance", urgent: true });
  if (i && !i.report && !i.outcome && daysTo(i.reportDeadline) <= DEADLINE_WARN_DAYS) {
    const d = daysTo(i.reportDeadline);
    add({ key: "i-report", label: d < 0 ? "Final report is overdue" : `Final report due in ${d} day${d === 1 ? "" : "s"}`, module: "Internship", href: "/internship/programme", urgent: d <= 3 });
  }

  // Trainee
  const t = q.softskill.data?.trainee;
  if (t && !t.report && !t.certificate && daysTo(t.reportDeadline) <= DEADLINE_WARN_DAYS) {
    const d = daysTo(t.reportDeadline);
    add({ key: "t-report", label: d < 0 ? "Learning report is overdue" : `Learning report due in ${d} day${d === 1 ? "" : "s"}`, module: "Soft Skill", href: "/softskill/programme", urgent: d <= 3 });
  }

  // Trainer
  const tr = q.trainer.data;
  if (tr && !tr.registration && tr.eligibility.eligible)
    add({ key: "tr-register", label: "Register as a Soft Skill trainer", module: "Trainer", href: "/softskill/trainer" });
  const sessionsToday = tr?.upcomingSessions.filter((s) => s.date === today).length;
  if (sessionsToday) add({ key: "tr-today", count: sessionsToday, label: `session${sessionsToday === 1 ? "" : "s"} to run today`, module: "Trainer", href: "/softskill/trainer" });

  // Monitor
  const overdue = (q.visits.data || []).filter((v) => v.status === "Planned" && v.plannedOn < today).length;
  if (overdue) add({ key: "mon-report", count: overdue, label: `visit report${overdue === 1 ? "" : "s"} to file`, module: "Monitoring", href: "/softskill/monitoring", urgent: true });

  // Applicant documents
  (q.requirements.data || [])
    .filter((m) => !m.ready && m.missing.length)
    .forEach((m) => add({ key: `doc-${m.module}`, count: m.missing.length, label: `document${m.missing.length === 1 ? "" : "s"} missing`, module: m.moduleName, href: "/account/documents" }));

  return items.sort((a, b) => Number(!!b.urgent) - Number(!!a.urgent));
}
