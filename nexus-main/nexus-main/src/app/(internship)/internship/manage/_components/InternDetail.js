"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { toast } from "sonner";
import { AlertTriangle, Award, CalendarCheck, CheckCircle2, ClipboardList, Receipt, Wallet, XCircle } from "lucide-react";
import {
  Badge,
  Button,
  DataTable,
  ErrorState,
  Input,
  Modal,
  PageHeader,
  Progress,
  Select,
  StatCard,
  StatusBadge,
  Tab,
  TabList,
  TabPanel,
  Tabs,
  Textarea,
} from "@/components/ui";
import { useSetup } from "@/components/layout/InternshipShell";
import {
  AttendanceRegister,
  MONTHLY_CRITERIA,
  MONTHLY_CRITERION_MAX,
  PaymentSlipModal,
  attendanceLabel,
  monthLabel,
  scoresLabel,
} from "@/components/domain/PaymentSlip";
import { useAction, useApiQuery, useSession } from "@/hooks/useApi";
import { api, qs } from "@/lib/api";
import { LEAVE_TONE, P } from "@/lib/internshipPermissions";
import { formatCurrency, formatDate } from "@/lib/formatters";
import { MonthlyTab } from "@/app/(internship)/internship/attendance/_components/MonthlyTab";

function Card({ title, aside, children, className = "" }) {
  return (
    <section className={`card-surface space-y-3 p-5 text-sm ${className}`}>
      {title || aside ? (
        <div className="flex flex-wrap items-center justify-between gap-3">
          {title ? <h3 className="text-sm font-semibold uppercase tracking-wide text-slate-600 dark:text-slate-300">{title}</h3> : <span />}
          {aside}
        </div>
      ) : null}
      {children}
    </section>
  );
}

function Fact({ label, children }) {
  return (
    <div className="min-w-0">
      <p className="text-[11px] font-semibold uppercase tracking-[0.08em] text-slate-500">{label}</p>
      <p className="mt-1 truncate font-medium text-slate-900 dark:text-slate-50">{children}</p>
    </div>
  );
}

function Check({ ok, children }) {
  return (
    <li className="flex items-center gap-2">
      {ok ? <CheckCircle2 className="h-4 w-4 shrink-0 text-success" /> : <XCircle className="h-4 w-4 shrink-0 text-danger" />}
      <span>{children}</span>
    </li>
  );
}

/** Tab label with a count bubble when something waits there. */
function TabLabel({ children, count }) {
  return (
    <span className="inline-flex items-center gap-1.5">
      {children}
      {count ? <span className="rounded-full bg-opportunity px-1.5 text-[11px] font-semibold leading-5 text-white">{count}</span> : null}
    </span>
  );
}

export const INTERNS_LIST_HREF = "/internship/manage?tab=interns";

/** The tabs of an intern's page; a link may open one with ?tab=. */
const INTERN_TABS = ["allowance", "attendance", "report", "placement", "outcome"];

function BackLink() {
  return (
    <Link href={INTERNS_LIST_HREF} className="text-sm font-medium text-nexus-700 hover:underline dark:text-nexus-300">
      ← All interns
    </Link>
  );
}

/** One intern's full record and every action on it: allowance, attendance, report, evaluation, placement, outcome. */
export function InternDetail({ id }) {
  const { policy, departments, criteria } = useSetup();
  const { can, user } = useSession();
  const isAdmin = user?.role === "Admin";
  const router = useRouter();
  const { data: app, error, setData, reload } = useApiQuery(`/internship/interns/${id}`);
  const mentors = useApiQuery(can(P.internTransfer) ? "/users/mentors" : null);
  const { run, busy } = useAction();
  // Notification links open a tab (?tab=report) and can point at one allowance month (?month=2026-10).
  const params = useSearchParams();
  const focusMonth = params.get("month");
  const [tab, setTab] = useState(() => (INTERN_TABS.includes(params.get("tab")) ? params.get("tab") : "allowance"));
  useEffect(() => {
    if (app && focusMonth) document.getElementById(`bill-${focusMonth}`)?.scrollIntoView({ block: "center", behavior: "smooth" });
  }, [app, focusMonth]);
  const [transfer, setTransfer] = useState({
    departmentId: "",
    mentorId: "",
    reason: "",
  });
  const [absence, setAbsence] = useState({
    date: "",
    approved: "no",
    reason: "",
  });
  const [times, setTimes] = useState({ date: "", checkIn: "", checkOut: "", reason: "" });
  const [reviewNote, setReviewNote] = useState("");
  // Null until edited: until then the form shows the saved evaluation.
  const [editedScores, setScores] = useState(null);
  const [editedRecommendation, setRecommendation] = useState(null);
  const [terminate, setTerminate] = useState("");
  const [month, setMonth] = useState(null);
  const [monthEval, setMonthEval] = useState({
    attendance: "",
    conduct: "",
    discipline: "",
    workSkill: "",
    absentDays: "",
    note: "",
  });
  const [slip, setSlip] = useState(null);
  const saved = app?.internship?.evaluation;
  const scores = editedScores ?? Object.fromEntries(Object.entries(saved?.scores || {}).map(([k, v]) => [k, String(v)]));
  const recommendation = editedRecommendation ?? saved?.recommendation ?? "";

  const act = async (path, body, msg) => {
    const res = await run(() => api.post(`/internship/interns/${id}/${path}`, body), msg);
    if (res) setData(res.application || res);
    return res;
  };

  // Pay a month from here too; the API returns the bill list, so reload the intern afterwards and open the new slip.
  const pay = async (b) => {
    const paid = await run(() => api.post(`/internship/bills/${id}/${b.month}/pay${qs({ status: "Paid" })}`), `Paid: ${monthLabel(b.month)}`);
    if (!paid) return;
    const fresh = await api.get(`/internship/interns/${id}`);
    setData(fresh);
    const bill = fresh.internship.bills.find((x) => x.month === b.month);
    if (bill?.slipNumber)
      setSlip({
        ...bill,
        studentName: fresh.student.name,
        batchName: fresh.batchName,
        department: fresh.internship.department.name,
        mentor: fresh.internship.mentor?.name,
      });
  };

  const openMonth = (b) => {
    setMonthEval({
      ...Object.fromEntries(MONTHLY_CRITERIA.map((c) => [c.key, b.scores ? String(b.scores[c.key]) : ""])),
      absentDays: String(b.deductDays ?? b.attendance.unapproved),
      note: b.evaluationNote || "",
    });
    setMonth(b);
  };

  if (error) {
    return (
      <div className="space-y-4">
        <BackLink />
        <ErrorState title="Could not load this intern" description={error.message} />
      </div>
    );
  }
  if (!app) {
    return (
      <div className="space-y-4">
        <BackLink />
        <p className="text-sm text-secondary">Loading…</p>
      </div>
    );
  }

  const it = app.internship;
  const sum = it.attendance;
  const outcome = it.computed;
  const live = app.status === "Active";
  const open = ["Active", "Completed"].includes(app.status);
  // cl.14: the server says when the department may evaluate (after the report, or after the end date).
  const canEvaluate = open && can(P.evaluationSubmit) && !it.evaluationBlockedReason;
  const transferMentors = (mentors.data || []).filter((m) => !transfer.departmentId || String(m.departmentId) === String(transfer.departmentId));

  const pendingLeave = it.leaveRequests.filter((l) => l.status === "Pending").length;
  const toEvaluate = it.bills.filter((b) => b.status === "Awaiting evaluation").length;
  const toPay = it.bills.filter((b) => b.status === "Ready to pay").length;
  const reportToReview = it.report?.status === "Submitted" ? 1 : 0;
  const paidBills = it.bills.filter((b) => b.status === "Paid");
  const paidTotal = paidBills.reduce((n, b) => n + (b.net || 0), 0);
  const slipFor = (b) => ({
    ...b,
    studentName: app.student.name,
    batchName: app.batchName,
    department: it.department.name,
    mentor: it.mentor?.name,
  });

  const attention = [
    toEvaluate && can(P.evaluationSubmit)
      ? {
          label: `${toEvaluate} monthly report${toEvaluate > 1 ? "s" : ""} to evaluate`,
          tab: "allowance",
        }
      : null,
    toPay && can(P.billPay)
      ? {
          label: `${toPay} month${toPay > 1 ? "s" : ""} ready to pay`,
          tab: "allowance",
        }
      : null,
    pendingLeave && can(P.attendanceManage)
      ? {
          label: `${pendingLeave} leave request${pendingLeave > 1 ? "s" : ""} to decide`,
          tab: "attendance",
        }
      : null,
    reportToReview && can(P.reportReview) ? { label: "Final report to review", tab: "report" } : null,
    sum.belowMin
      ? {
          label: `Attendance ${sum.pct}% is below ${policy.minAttendancePct}%`,
          tab: "attendance",
          danger: true,
        }
      : null,
  ].filter(Boolean);

  return (
    <div className="space-y-5">
      <PageHeader
        breadcrumbs={<BackLink />}
        title={app.student.name}
        description={`${app.batchName} · ${app.student.email || ""}`.replace(/ · $/, "")}
        actions={<StatusBadge status={app.status} />}
      />

      <div className="card-surface grid gap-4 p-5 sm:grid-cols-2 lg:grid-cols-4">
        <Fact label="Department">{it.department.name}</Fact>
        <Fact label="Mentor">{it.mentor ? `${it.mentor.name}${it.mentor.designation ? ` · ${it.mentor.designation}` : ""}` : "—"}</Fact>
        <Fact label="Period">
          {formatDate(it.startDate)} – {formatDate(it.endDate)}
        </Fact>
        <Fact label="Outcome">
          {it.certificate ? (
            <Badge tone="green">
              {it.certificate.grade} · {it.certificate.number}
            </Badge>
          ) : it.outcome ? (
            <Badge tone="red">{it.outcome.result}</Badge>
          ) : (
            "In progress"
          )}
        </Fact>
        {it.outcome?.reasons?.length ? <p className="text-sm text-danger sm:col-span-2 lg:col-span-4">{it.outcome.reasons.join(" ")}</p> : null}
      </div>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard
          label="Attendance"
          value={`${sum.pct}%`}
          hint={`${sum.present}/${sum.required} days · ${sum.unapproved} unapproved · min ${policy.minAttendancePct}%`}
          icon={<CalendarCheck className="h-5 w-5" />}
          tone={sum.belowMin ? "red" : "green"}
        />
        <StatCard
          label="Allowance paid"
          value={formatCurrency(paidTotal)}
          hint={`${paidBills.length} of ${it.bills.length} month${it.bills.length === 1 ? "" : "s"} so far`}
          icon={<Wallet className="h-5 w-5" />}
          tone="teal"
        />
        <StatCard
          label="Final report"
          value={it.report?.status || "Not submitted"}
          hint={`Due ${formatDate(it.reportDeadline)}${it.report?.late ? " · late" : ""}`}
          icon={<ClipboardList className="h-5 w-5" />}
          tone={it.report?.status === "Accepted" ? "green" : "amber"}
        />
        <StatCard
          label="Final evaluation"
          value={it.evaluation ? `${it.evaluation.total}/100` : "—"}
          hint={outcome.grade ? `Suggested grade ${outcome.grade}` : "After the final report (cl.14)"}
          icon={<Award className="h-5 w-5" />}
          tone="violet"
        />
      </div>

      {attention.length ? (
        <div className="flex flex-wrap items-center gap-2 rounded-2xl border border-opportunity/40 bg-[#f7f1e6] px-4 py-3 text-sm dark:bg-nexus-800/60">
          <AlertTriangle className="h-4 w-4 text-opportunity" />
          <span className="font-semibold">Needs attention:</span>
          {attention.map((a) => (
            <button
              key={a.label}
              type="button"
              onClick={() => setTab(a.tab)}
              className={`rounded-full px-3 py-1 text-xs font-semibold hover:underline ${a.danger ? "bg-danger text-white" : "bg-opportunity text-white"}`}
            >
              {a.label}
            </button>
          ))}
        </div>
      ) : null}

      <Tabs value={tab} onChange={setTab}>
        <TabList variant="line">
          <Tab value="allowance">
            <TabLabel count={(can(P.evaluationSubmit) ? toEvaluate : 0) + (can(P.billPay) ? toPay : 0)}>Monthly allowance</TabLabel>
          </Tab>
          <Tab value="attendance">
            <TabLabel count={can(P.attendanceManage) ? pendingLeave : 0}>Attendance</TabLabel>
          </Tab>
          <Tab value="report">
            <TabLabel count={can(P.reportReview) ? reportToReview : 0}>Final report &amp; evaluation</TabLabel>
          </Tab>
          <Tab value="placement">Placement</Tab>
          <Tab value="outcome">Completion</Tab>
        </TabList>

        <TabPanel value="allowance" className="space-y-3">
          <p className="text-sm text-secondary">
            Each month: the intern submits a report when it ends → the mentor reviews attendance, conduct, discipline and work skill and sets the absent days →
            UGC pays and issues a slip (cl.10). Allowance {formatCurrency(policy.monthlyAllowance)}/month, excl. VAT and tax.
          </p>
          <DataTable
            emptyMessage="The internship has not started yet."
            rows={it.bills.map((b) => ({ ...b, id: b.month }))}
            columns={[
              {
                key: "month",
                label: "Month",
                render: (b) => (
                  <span
                    id={`bill-${b.month}`}
                    className={
                      b.month === focusMonth
                        ? "rounded-md bg-[#f7f1e6] px-2 py-1 font-medium ring-2 ring-opportunity dark:bg-nexus-800"
                        : "font-medium"
                    }
                  >
                    {monthLabel(b.month)}
                  </span>
                ),
              },
              {
                key: "att",
                label: "Attendance",
                render: (b) => <span className="text-xs">{attendanceLabel(b.attendance)}</span>,
              },
              {
                key: "gross",
                label: "Gross",
                render: (b) => formatCurrency(b.gross),
              },
              {
                key: "ded",
                label: "Deduction",
                render: (b) =>
                  b.deductDays == null ? (
                    <span className="text-xs text-secondary">At evaluation</span>
                  ) : b.deductDays ? (
                    <Badge tone="red">
                      −{formatCurrency(b.deduction)} · {b.deductDays} d
                    </Badge>
                  ) : (
                    "—"
                  ),
              },
              {
                key: "net",
                label: "Net",
                render: (b) => (b.net == null ? "—" : <strong>{formatCurrency(b.net)}</strong>),
              },
              {
                key: "score",
                label: "Review",
                render: (b) => (b.score != null ? <Badge tone="green">{b.score}/100</Badge> : "—"),
              },
              {
                key: "status",
                label: "Status",
                render: (b) => <StatusBadge status={b.status} />,
              },
              {
                key: "act",
                label: "",
                render: (b) => (
                  <span className="flex justify-end gap-2">
                    {b.report && b.status !== "Paid" && can(P.evaluationSubmit) ? (
                      <Button size="sm" variant={b.evaluatedAt ? "outline" : "primary"} onClick={() => openMonth(b)}>
                        {b.evaluatedAt ? "Re-evaluate" : "Evaluate"}
                      </Button>
                    ) : b.report ? (
                      <Button size="sm" variant="ghost" onClick={() => setMonth(b)}>
                        View report
                      </Button>
                    ) : null}
                    {b.status === "Ready to pay" && can(P.billPay) ? (
                      <Button size="sm" disabled={busy} onClick={() => pay(b)}>
                        Pay {formatCurrency(b.net)}
                      </Button>
                    ) : null}
                    {b.status === "Paid" ? (
                      <Button size="sm" variant="outline" onClick={() => setSlip(slipFor(b))}>
                        <Receipt className="h-3.5 w-3.5" /> Slip
                      </Button>
                    ) : null}
                    {b.status === "Withheld" ? <span className="text-xs text-secondary">After final report</span> : null}
                  </span>
                ),
              },
            ]}
          />
        </TabPanel>

        <TabPanel value="attendance" className="space-y-4">
          <Card
            title="Attendance"
            aside={
              <Badge tone={sum.belowMin ? "red" : "green"}>
                {sum.pct}% · minimum {policy.minAttendancePct}%
              </Badge>
            }
          >
            <Progress value={sum.pct} />
            <div className="grid gap-3 sm:grid-cols-4">
              <Fact label="Working days">{sum.required}</Fact>
              <Fact label="Present">{sum.present}</Fact>
              <Fact label="Unapproved absent">{sum.unapproved}</Fact>
              <Fact label="Approved leave / absence">{sum.approved}</Fact>
            </div>
          </Card>

          <div>
            <Card title="Leave requests" aside={pendingLeave ? <Badge tone="amber">{pendingLeave} pending</Badge> : null}>
              {it.leaveRequests.length ? (
                <div className="space-y-2">
                  {it.leaveRequests.map((l) => (
                    <div key={l.date} className="flex flex-wrap items-center justify-between gap-2 rounded-lg bg-white/60 px-3 py-2 dark:bg-nexus-800/60">
                      <span>
                        <span className="font-medium">{formatDate(l.date, "EEE dd MMM")}</span>
                        <span className="text-secondary"> — {l.reason || "No reason given"}</span>
                      </span>
                      {l.status === "Pending" && can(P.attendanceManage) ? (
                        <span className="flex gap-2">
                          <Button size="sm" disabled={busy} onClick={() => act("leave/decide", { date: l.date, approve: true }, "Leave approved")}>
                            Approve
                          </Button>
                          <Button
                            size="sm"
                            variant="danger"
                            disabled={busy}
                            onClick={() => act("leave/decide", { date: l.date, approve: false }, "Leave rejected")}
                          >
                            Reject
                          </Button>
                        </span>
                      ) : (
                        <Badge tone={LEAVE_TONE[l.status]}>{l.status}</Badge>
                      )}
                    </div>
                  ))}
                </div>
              ) : (
                <p className="text-secondary">No leave requests.</p>
              )}
            </Card>
          </div>

          <Card title="Month by month">
            <MonthlyTab applicationId={id} onChange={reload} />
          </Card>

          {live && can(P.attendanceManage) ? (
            <Card title="Mark an absence">
              <div className="grid gap-3 sm:grid-cols-[1fr_1fr_2fr_auto] sm:items-end">
                <Input label="Date" type="date" value={absence.date} onChange={(e) => setAbsence({ ...absence, date: e.target.value })} />
                <Select
                  label="Absence"
                  value={absence.approved}
                  onChange={(e) => setAbsence({ ...absence, approved: e.target.value })}
                  options={[
                    { value: "no", label: "Unapproved (deduct)" },
                    { value: "yes", label: "Approved (no deduction)" },
                  ]}
                />
                <Input label="Reason" value={absence.reason} onChange={(e) => setAbsence({ ...absence, reason: e.target.value })} />
                <Button
                  variant="outline"
                  disabled={busy}
                  onClick={() =>
                    act(
                      "absence",
                      {
                        date: absence.date || null,
                        approved: absence.approved === "yes",
                        reason: absence.reason,
                      },
                      "Absence recorded",
                    )
                  }
                >
                  Mark
                </Button>
              </div>
            </Card>
          ) : null}

          {live && isAdmin ? (
            <Card title="Edit sign-in and sign-out times">
              <div className="grid gap-3 sm:grid-cols-[1fr_1fr_1fr_2fr_auto] sm:items-end">
                <Input label="Date" type="date" value={times.date} onChange={(e) => setTimes({ ...times, date: e.target.value })} />
                <Input label="Sign in" type="time" value={times.checkIn} onChange={(e) => setTimes({ ...times, checkIn: e.target.value })} />
                <Input label="Sign out" type="time" value={times.checkOut} onChange={(e) => setTimes({ ...times, checkOut: e.target.value })} />
                <Input label="Reason" value={times.reason} onChange={(e) => setTimes({ ...times, reason: e.target.value })} />
                <Button
                  variant="outline"
                  disabled={busy}
                  onClick={() =>
                    act(
                      "attendance/times",
                      {
                        date: times.date || null,
                        checkIn: times.checkIn,
                        checkOut: times.checkOut || null,
                        reason: times.reason,
                      },
                      "Times saved",
                    ).then((res) => res && setTimes({ date: "", checkIn: "", checkOut: "", reason: "" }))
                  }
                >
                  Save
                </Button>
              </div>
              <p className="text-xs text-secondary">
                Bangladesh time. Leave sign out empty if the intern has not signed out. The change is recorded in the audit log.
              </p>
            </Card>
          ) : null}
        </TabPanel>

        <TabPanel value="report" className="grid gap-4 lg:grid-cols-2">
          <Card
            title="Final report (cl.11 ট)"
            aside={
              <span className="text-xs text-secondary">
                Due {formatDate(it.reportDeadline)} ({policy.reportWorkingDays} working days after end)
              </span>
            }
          >
            {it.report ? (
              <>
                <div className="flex flex-wrap items-center gap-2">
                  <Badge tone={it.report.status === "Accepted" ? "green" : "amber"}>{it.report.status}</Badge>
                  <span className="text-xs text-secondary">
                    Submitted {formatDate(it.report.submittedAt)}
                    {it.report.late ? " · late" : ""}
                  </span>
                </div>
                <p className="max-h-72 overflow-y-auto whitespace-pre-wrap rounded-xl bg-white/60 p-3 dark:bg-nexus-800/60">{it.report.text}</p>
                {it.report.reviewNote ? <p className="text-xs text-secondary">Review note: {it.report.reviewNote}</p> : null}
                {it.report.status === "Submitted" && can(P.reportReview) ? (
                  <div className="space-y-2">
                    <Input
                      label="Note to intern (needed for revision)"
                      value={reviewNote}
                      onChange={(e) => setReviewNote(e.target.value)}
                      maxLength={policy.maxReviewNoteLength}
                    />
                    <div className="flex gap-2">
                      <Button disabled={busy} onClick={() => act("report/review", { accept: true, note: reviewNote }, "Report accepted")}>
                        Accept
                      </Button>
                      <Button
                        variant="secondary"
                        disabled={busy}
                        onClick={() => act("report/review", { accept: false, note: reviewNote }, "Revision requested")}
                      >
                        Ask revision
                      </Button>
                    </div>
                  </div>
                ) : null}
              </>
            ) : (
              <p className="text-secondary">No report yet.</p>
            )}
          </Card>

          <Card title="Department evaluation (cl.14)" aside={it.evaluation ? <Badge tone="green">Total {it.evaluation.total}/100</Badge> : null}>
            {it.evaluation?.evaluatorName ? (
              <p className="text-xs text-secondary">
                By {it.evaluation.evaluatorName}, {formatDate(it.evaluation.at)}
              </p>
            ) : null}
            {it.evaluationBlockedReason && can(P.evaluationSubmit) ? <p className="text-secondary">{it.evaluationBlockedReason}</p> : null}
            {open && (canEvaluate || it.evaluation) ? (
              <>
                <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
                  {criteria.map((c) => (
                    <Input
                      key={c.key}
                      label={c.label}
                      type="number"
                      value={scores[c.key] ?? ""}
                      onChange={(e) => setScores({ ...scores, [c.key]: e.target.value })}
                      hint={`0–${c.maxScore}`}
                      disabled={!canEvaluate}
                    />
                  ))}
                </div>
                <Textarea
                  label="Recommendation to the Commission"
                  value={recommendation}
                  onChange={(e) => setRecommendation(e.target.value)}
                  rows={3}
                  maxLength={policy.maxRecommendationLength}
                  hint={
                    canEvaluate
                      ? `At least ${policy.minRecommendationLength} characters · ${recommendation.trim().length}/${policy.maxRecommendationLength}`
                      : undefined
                  }
                  disabled={!canEvaluate}
                />
              </>
            ) : !it.evaluationBlockedReason ? (
              <p className="text-secondary">No evaluation yet.</p>
            ) : null}
            {canEvaluate ? (
              <Button
                variant="outline"
                disabled={busy}
                onClick={() =>
                  act(
                    "evaluation",
                    {
                      scores: Object.fromEntries(criteria.map((c) => [c.key, scores[c.key] === "" || scores[c.key] == null ? null : Number(scores[c.key])])),
                      recommendation,
                    },
                    "Evaluation saved",
                  )
                }
              >
                {it.evaluation ? "Update evaluation" : "Submit evaluation"}
              </Button>
            ) : null}
          </Card>
        </TabPanel>

        <TabPanel value="placement" className="space-y-4">
          <div className="grid gap-4 lg:grid-cols-2">
            <Card title="Current placement">
              <div className="grid gap-3 sm:grid-cols-2">
                <Fact label="Department">{it.department.name}</Fact>
                <Fact label="Mentor">{it.mentor?.name || "—"}</Fact>
              </div>
              <div>
                <p className="text-[11px] font-semibold uppercase tracking-[0.08em] text-slate-500">Assigned tasks (cl.13)</p>
                <p className="mt-1 text-secondary">{it.tasks}</p>
              </div>
            </Card>

            <Card title="Placement history (cl.12)">
              <ol className="space-y-3 border-l-2 border-nexus-600/40 pl-4">
                {it.placements.map((p, i) => (
                  <li key={`${p.from}-${i}`} className="relative">
                    <span className="absolute -left-[1.4rem] top-1 h-2.5 w-2.5 rounded-full bg-nexus-600" />
                    <p className="font-medium">
                      {p.department}
                      {p.mentor ? <span className="text-secondary"> · {p.mentor}</span> : null}
                    </p>
                    <p className="text-xs text-secondary">
                      From {formatDate(p.from)}
                      {p.note ? ` · ${p.note}` : ""}
                    </p>
                  </li>
                ))}
              </ol>
            </Card>
          </div>

          {live && can(P.internTransfer) ? (
            <Card title="Transfer to another department">
              <div className="grid gap-3 sm:grid-cols-[1fr_1fr_2fr_auto] sm:items-end">
                <Select
                  label="Transfer to"
                  value={transfer.departmentId}
                  onChange={(e) =>
                    setTransfer({
                      ...transfer,
                      departmentId: e.target.value,
                      mentorId: "",
                    })
                  }
                  options={departments.map((d) => ({
                    value: String(d.id),
                    label: d.name,
                  }))}
                  placeholder="Choose department"
                />
                <Select
                  label="New mentor"
                  value={transfer.mentorId}
                  onChange={(e) => setTransfer({ ...transfer, mentorId: e.target.value })}
                  options={(transferMentors.length ? transferMentors : mentors.data || []).map((m) => ({ value: String(m.id), label: m.name }))}
                  placeholder="Keep current"
                />
                <Input label="Reason (cl.12)" value={transfer.reason} onChange={(e) => setTransfer({ ...transfer, reason: e.target.value })} />
                <Button
                  variant="outline"
                  disabled={busy}
                  onClick={async () => {
                    const ok = await act(
                      "transfer",
                      {
                        departmentId: transfer.departmentId ? Number(transfer.departmentId) : null,
                        mentorId: transfer.mentorId ? Number(transfer.mentorId) : null,
                        reason: transfer.reason,
                      },
                      "Intern transferred",
                    );
                    if (ok)
                      setTransfer({
                        departmentId: "",
                        mentorId: "",
                        reason: "",
                      });
                  }}
                >
                  Transfer
                </Button>
              </div>
            </Card>
          ) : null}
        </TabPanel>

        <TabPanel value="outcome" className="space-y-4">
          <Card title="Completion and certificate (cl.15)">
            <ul className="space-y-2">
              <Check ok={!sum.belowMin}>
                Attendance {sum.pct}% (minimum {policy.minAttendancePct}%)
              </Check>
              <Check ok={it.report?.status === "Accepted"}>Final report: {it.report?.status || "not submitted"}</Check>
              <Check ok={Boolean(it.evaluation)}>
                Final evaluation: {it.evaluation ? `${it.evaluation.total}/100` : "missing"}
                {outcome.grade ? ` → suggested grade ${outcome.grade}` : ""}
              </Check>
            </ul>
            {outcome.reasons.length ? <p className="text-danger">{outcome.reasons.join(" ")}</p> : <p className="text-success">All conditions met.</p>}
            {open ? (
              <div className="flex flex-wrap gap-2">
                {live && can(P.internComplete) ? (
                  <Button variant="secondary" disabled={busy} onClick={() => act("complete", {}, "Marked complete")}>
                    Mark internship complete
                  </Button>
                ) : null}
                {app.status === "Completed" && can(P.certificateIssue) ? (
                  <Button
                    disabled={busy || !outcome.ready}
                    onClick={async () => {
                      const r = await act("outcome", {}, null);
                      if (r?.result) {
                        // The toast reflects which outcome the server recorded.
                        if (r.result.certified) toast.success(`Certificate issued: ${r.result.grade}`);
                        else toast.message("Recorded as not certified");
                      }
                    }}
                  >
                    Issue outcome
                  </Button>
                ) : null}
              </div>
            ) : null}
          </Card>

          {live && can(P.internTerminate) ? (
            <Card title="Terminate (cl.11 ঠ)" className="border-danger/40">
              <p className="text-secondary">Only for a conduct or discipline breach. This ends the internship and cannot be undone here.</p>
              <div className="grid gap-3 sm:grid-cols-[3fr_auto] sm:items-end">
                <Input label="Reason" value={terminate} onChange={(e) => setTerminate(e.target.value)} />
                <Button
                  variant="danger"
                  disabled={busy}
                  onClick={async () => {
                    if (await act("terminate", { reason: terminate }, "Internship terminated")) router.push(INTERNS_LIST_HREF);
                  }}
                >
                  Terminate
                </Button>
              </div>
            </Card>
          ) : null}
        </TabPanel>
      </Tabs>

      <Modal open={Boolean(month)} onClose={() => setMonth(null)} title={month ? `${app.student.name} · ${monthLabel(month.month)}` : ""} size="xl">
        {month ? (
          <div className="space-y-3 text-sm">
            <p className="whitespace-pre-wrap rounded-xl bg-white/60 p-3 dark:bg-nexus-800/60">{month.report}</p>
            <p className="text-xs text-secondary">Submitted {formatDate(month.submittedAt)}</p>
            <div className="grid gap-2 rounded-xl border border-slate-200 p-3 sm:grid-cols-4 dark:border-slate-700">
              <div>
                <p className="text-xs text-secondary">Working days</p>
                <p className="font-semibold">{month.periodDays}</p>
              </div>
              <div>
                <p className="text-xs text-secondary">Present</p>
                <p className="font-semibold">
                  {month.attendance.present}/{month.attendance.closedDays}
                </p>
              </div>
              <div>
                <p className="text-xs text-secondary">Unapproved absent</p>
                <p className="font-semibold text-danger">{month.attendance.unapproved}</p>
              </div>
              <div>
                <p className="text-xs text-secondary">Approved leave</p>
                <p className="font-semibold">{month.attendance.approved}</p>
              </div>
            </div>
            <details className="rounded-xl border border-slate-200 p-3 dark:border-slate-700">
              <summary className="cursor-pointer text-xs font-semibold uppercase tracking-wide text-secondary">Attendance register (cl.11 জ)</summary>
              <div className="pt-2">
                <AttendanceRegister days={month.register} />
              </div>
            </details>
            {month.status !== "Paid" && can(P.evaluationSubmit) ? (
              <>
                <p className="text-xs font-semibold uppercase tracking-wide text-secondary">
                  Monthly review (cl.10), 0–{MONTHLY_CRITERION_MAX} each · total {MONTHLY_CRITERIA.reduce((n, c) => n + (Number(monthEval[c.key]) || 0), 0)}
                  /100
                </p>
                <div className="grid gap-3 sm:grid-cols-4">
                  {MONTHLY_CRITERIA.map((c) => (
                    <Input
                      key={c.key}
                      label={c.label}
                      type="number"
                      min={0}
                      max={MONTHLY_CRITERION_MAX}
                      value={monthEval[c.key]}
                      onChange={(e) => setMonthEval({ ...monthEval, [c.key]: e.target.value })}
                      hint={`0–${MONTHLY_CRITERION_MAX}`}
                    />
                  ))}
                </div>
                <div className="grid gap-3 sm:grid-cols-[1fr_3fr]">
                  <Input
                    label="Absent days to deduct"
                    type="number"
                    min={0}
                    max={month.periodDays}
                    value={monthEval.absentDays}
                    onChange={(e) => setMonthEval({ ...monthEval, absentDays: e.target.value })}
                    hint={`Register: ${month.attendance.unapproved} unapproved`}
                  />
                  <Textarea
                    label="Note on the month's work"
                    value={monthEval.note}
                    onChange={(e) => setMonthEval({ ...monthEval, note: e.target.value })}
                    rows={3}
                    maxLength={policy.maxMonthlyNoteLength}
                  />
                </div>
                {(() => {
                  const days = Number(monthEval.absentDays) || 0;
                  const deduction = Math.round(days * month.dailyRate);
                  return (
                    <p className="text-secondary">
                      Gross {formatCurrency(month.gross)} − {days} day
                      {days === 1 ? "" : "s"} × {formatCurrency(month.dailyRate)} = <strong>{formatCurrency(Math.max(0, month.gross - deduction))}</strong>{" "}
                      (approx.; the server rounds the exact daily rate)
                    </p>
                  );
                })()}
                <div className="flex justify-end">
                  <Button
                    disabled={busy}
                    onClick={async () => {
                      const ok = await act(
                        `bills/${month.month}/evaluate`,
                        {
                          ...Object.fromEntries(MONTHLY_CRITERIA.map((c) => [c.key, monthEval[c.key] === "" ? null : Number(monthEval[c.key])])),
                          absentDays: monthEval.absentDays === "" ? null : Number(monthEval.absentDays),
                          note: monthEval.note,
                        },
                        `${monthLabel(month.month)} evaluated`,
                      );
                      if (ok) setMonth(null);
                    }}
                  >
                    {month.evaluatedAt ? "Update evaluation" : "Submit evaluation"}
                  </Button>
                </div>
              </>
            ) : month.evaluatedAt ? (
              <div className="rounded-xl border border-slate-200 p-3 dark:border-slate-700">
                <p className="font-semibold">
                  {month.score}/100
                  {month.evaluatedBy ? ` · ${month.evaluatedBy}` : ""} · {month.deductDays} absent day
                  {month.deductDays === 1 ? "" : "s"} · net {formatCurrency(month.net)}
                </p>
                {month.scores ? <p className="text-xs text-secondary">{scoresLabel(month.scores)} (out of 25 each)</p> : null}
                <p className="text-secondary">{month.evaluationNote}</p>
              </div>
            ) : (
              <p className="text-secondary">Not evaluated yet.</p>
            )}
          </div>
        ) : null}
      </Modal>

      <PaymentSlipModal slip={slip} onClose={() => setSlip(null)} />
    </div>
  );
}
