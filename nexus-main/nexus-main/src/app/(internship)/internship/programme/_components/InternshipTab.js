"use client";

import Link from "next/link";
import { useState } from "react";
import { AlertTriangle, Award, CalendarCheck, ClipboardList, Receipt, RefreshCw, Smartphone, Wallet } from "lucide-react";
import { Badge, Button, DataTable, Input, Modal, Progress, StatCard, StatusBadge, Tab, TabList, TabPanel, Tabs, Textarea } from "@/components/ui";
import { useSetup } from "@/components/layout/InternshipShell";
import { AttendanceRegister, PaymentSlipModal, attendanceLabel, deductionLabel, monthLabel, scoresLabel } from "@/components/domain/PaymentSlip";
import { useAction, useSession } from "@/hooks/useApi";
import { api } from "@/lib/api";
import { LEAVE_TONE, P } from "@/lib/internshipPermissions";
import { formatCurrency, formatDate } from "@/lib/formatters";
import { MonthlyTab } from "../../attendance/_components/MonthlyTab";

function Card({ title, aside, children }) {
  return (
    <section className="card-surface space-y-3 p-5 text-sm">
      {title || aside ? (
        <div className="flex flex-wrap items-center justify-between gap-2">
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

/** "(12 min late)" after a sign-in time. Lateness is shown only; it is not deducted. */
function LateNote({ minutes }) {
  return minutes > 0 ? <span className="ml-1 text-danger">({minutes} min late)</span> : null;
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

/** The student's own internship: today's sign-in (made in the mobile app), monthly reports and allowance, attendance, final report, placement, certificate. */
export function InternshipTab({ app, onChange, onRefresh }) {
  const { policy } = useSetup();
  const { can } = useSession();
  const { run, busy } = useAction();
  const it = app.internship;
  const sum = it.attendance;
  const today = it.today;
  const rec = today.record;
  const live = app.status === "Active";

  const [tab, setTab] = useState("allowance");
  const [leave, setLeave] = useState({ date: "", reason: "" });
  const [report, setReport] = useState(it.report?.text || "");
  const [cert, setCert] = useState(false);
  const [month, setMonth] = useState(null);
  const [monthReport, setMonthReport] = useState("");
  const [slip, setSlip] = useState(null);
  const slipFor = (b) => ({
    ...b,
    studentName: app.student.name,
    batchName: app.batchName,
    department: it.department.name,
    mentor: it.mentor?.name,
  });

  const act = async (path, body, msg) => {
    const next = await run(() => api.post(path, body), msg);
    if (next) onChange(next);
    return next;
  };

  const canAttend = can(P.selfAttendance);
  const reportsDue = it.bills.filter((b) => b.status === "Report due").length;
  const paidBills = it.bills.filter((b) => b.status === "Paid");
  const paidTotal = paidBills.reduce((n, b) => n + (b.net || 0), 0);
  const pendingLeave = it.leaveRequests.filter((l) => l.status === "Pending").length;
  const canWriteReport = can(P.selfReport) && ["Active", "Completed"].includes(app.status) && it.report?.status !== "Accepted";
  const reportNeeded = canWriteReport && (app.status === "Completed" || it.report?.status === "Revision requested");

  const todo = [
    reportsDue && can(P.selfBill) ? { label: `Submit ${reportsDue} monthly report${reportsDue > 1 ? "s" : ""}`, tab: "allowance" } : null,
    reportNeeded ? { label: it.report ? "Revise your final report" : `Submit your final report by ${formatDate(it.reportDeadline)}`, tab: "report" } : null,
    sum.belowMin ? { label: `Attendance ${sum.pct}% is below ${policy.minAttendancePct}%`, tab: "attendance", danger: true } : null,
  ].filter(Boolean);

  return (
    <div className="space-y-5">
      <div className="card-surface grid gap-4 p-5 sm:grid-cols-2 lg:grid-cols-4">
        <Fact label="Department">{it.department.name}</Fact>
        <Fact label="Mentor">{it.mentor ? `${it.mentor.name}${it.mentor.designation ? ` · ${it.mentor.designation}` : ""}` : "—"}</Fact>
        <Fact label="Period">
          {formatDate(it.startDate)} – {formatDate(it.endDate)}
        </Fact>
        <Fact label="Status">
          <StatusBadge status={app.status} />
        </Fact>
      </div>

      {live && canAttend ? (
        <div className="card-surface flex flex-wrap items-center justify-between gap-3 p-5">
          <div className="text-sm">
            <p className="text-[11px] font-semibold uppercase tracking-[0.08em] text-slate-500">Today</p>
            <p className="mt-1 text-lg font-semibold">{formatDate(today.date, "EEEE, dd MMM yyyy")}</p>
            <p className="text-secondary">
              {!today.inPeriod ? (
                "No sign-in needed today (weekend, holiday or outside your period)."
              ) : rec?.status === "Present" ? (
                <>
                  Signed in {rec.checkIn}
                  <LateNote minutes={rec.lateMinutes} />
                  {rec.checkOut ? ` · signed out ${rec.checkOut}` : " · not signed out yet"}
                </>
              ) : rec?.status === "Leave requested" ? (
                "Leave requested"
              ) : (
                "Not signed in yet"
              )}
            </p>
            <p className="mt-2 flex items-center gap-1.5 text-xs text-secondary">
              <Smartphone className="h-3.5 w-3.5" /> Sign in and out with the Nexus mobile app, at your department, with a selfie.
            </p>
          </div>
          {onRefresh ? (
            <Button variant="outline" onClick={onRefresh}>
              <RefreshCw className="h-4 w-4" /> Refresh
            </Button>
          ) : null}
        </div>
      ) : null}

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard
          label="Attendance"
          value={`${sum.pct}%`}
          hint={`${sum.present}/${sum.required} days present · need ${policy.minAttendancePct}%`}
          icon={<CalendarCheck className="h-5 w-5" />}
          tone={sum.belowMin ? "red" : "green"}
        />
        <StatCard
          label="Allowance received"
          value={formatCurrency(paidTotal)}
          hint={`${paidBills.length} of ${it.bills.length} month${it.bills.length === 1 ? "" : "s"} paid so far`}
          icon={<Wallet className="h-5 w-5" />}
          tone="teal"
        />
        <StatCard
          label="Final report"
          value={it.report?.status || "Not submitted"}
          hint={`Due ${formatDate(it.reportDeadline)}`}
          icon={<ClipboardList className="h-5 w-5" />}
          tone={it.report?.status === "Accepted" ? "green" : "amber"}
        />
        <StatCard
          label="Certificate"
          value={it.certificate ? it.certificate.grade : it.outcome ? it.outcome.result : "Not yet"}
          hint={it.certificate ? `No. ${it.certificate.number}` : "Issued after completion (cl.15)"}
          icon={<Award className="h-5 w-5" />}
          tone="violet"
        />
      </div>

      {todo.length ? (
        <div className="flex flex-wrap items-center gap-2 rounded-2xl border border-opportunity/40 bg-[#f7f1e6] px-4 py-3 text-sm dark:bg-nexus-800/60">
          <AlertTriangle className="h-4 w-4 text-opportunity" />
          <span className="font-semibold">To do:</span>
          {todo.map((t) => (
            <button
              key={t.label}
              type="button"
              onClick={() => setTab(t.tab)}
              className={`rounded-full px-3 py-1 text-xs font-semibold text-white hover:underline ${t.danger ? "bg-danger" : "bg-opportunity"}`}
            >
              {t.label}
            </button>
          ))}
        </div>
      ) : null}

      <Tabs value={tab} onChange={setTab}>
        <TabList>
          <Tab value="allowance">
            <TabLabel count={can(P.selfBill) ? reportsDue : 0}>Monthly report &amp; allowance</TabLabel>
          </Tab>
          <Tab value="attendance">
            <TabLabel count={pendingLeave}>Attendance &amp; leave</TabLabel>
          </Tab>
          <Tab value="report">
            <TabLabel count={reportNeeded ? 1 : 0}>Final report</TabLabel>
          </Tab>
          <Tab value="placement">Placement</Tab>
          {it.certificate ? <Tab value="certificate">Certificate</Tab> : null}
        </TabList>

        <TabPanel value="allowance" className="space-y-3">
          <p className="text-sm text-secondary">
            When a month ends, submit a short report on its work. It is your allowance bill: your mentor reviews it, then UGC pays and issues a slip. Allowance{" "}
            {formatCurrency(policy.monthlyAllowance)}/month, excl. VAT and tax.
          </p>
          <DataTable
            emptyMessage="Your first month appears when the internship starts."
            rows={it.bills.map((b) => ({ ...b, id: b.month }))}
            columns={[
              { key: "month", label: "Month", render: (b) => <span className="font-medium">{monthLabel(b.month)}</span> },
              { key: "att", label: "Attendance", render: (b) => <span className="text-xs">{attendanceLabel(b.attendance)}</span> },
              { key: "gross", label: "Gross", render: (b) => formatCurrency(b.gross) },
              { key: "ded", label: "Deduction", render: (b) => deductionLabel(b) },
              { key: "net", label: "Net", render: (b) => (b.net == null ? "—" : <strong>{formatCurrency(b.net)}</strong>) },
              { key: "score", label: "Review", render: (b) => (b.score != null ? <Badge tone="green">{b.score}/100</Badge> : "—") },
              { key: "status", label: "Status", render: (b) => <StatusBadge status={b.status} /> },
              {
                key: "act",
                label: "",
                render: (b) => (
                  <span className="flex justify-end gap-2">
                    {b.status === "Report due" && can(P.selfBill) ? (
                      <Button
                        size="sm"
                        disabled={busy}
                        onClick={() => {
                          setMonthReport("");
                          setMonth(b);
                        }}
                      >
                        Submit report
                      </Button>
                    ) : b.report ? (
                      <Button size="sm" variant="ghost" onClick={() => setMonth(b)}>
                        View
                      </Button>
                    ) : null}
                    {b.status === "Paid" ? (
                      <Button size="sm" variant="outline" onClick={() => setSlip(slipFor(b))}>
                        <Receipt className="h-3.5 w-3.5" /> Slip
                      </Button>
                    ) : null}
                  </span>
                ),
              },
            ]}
          />
          {it.bills.some((b) => b.status === "Withheld") ? (
            <p className="text-xs text-secondary">The last month is released after your final report is accepted (cl.11 ট).</p>
          ) : null}
        </TabPanel>

        <TabPanel value="attendance" className="space-y-4">
          <Card
            title="Attendance (your daily register)"
            aside={
              <Badge tone={sum.belowMin ? "red" : "green"}>
                {sum.pct}% · need {policy.minAttendancePct}%
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
            {sum.belowMin ? (
              <p role="alert" className="rounded-xl bg-red-50 p-3 text-danger">
                Your attendance is below {policy.minAttendancePct}%. Without it you cannot receive a certificate (cl.11 ঞ).
              </p>
            ) : null}
          </Card>

          <Card
            title="Month by month"
            aside={
              <Link href="/internship/my-attendance" className="text-xs font-medium text-nexus-700 hover:underline dark:text-nexus-300">
                Every day in detail →
              </Link>
            }
          >
            <MonthlyTab self compact />
          </Card>

          <Card title="Leave" aside={pendingLeave ? <Badge tone="amber">{pendingLeave} waiting</Badge> : null}>
            {live && canAttend ? (
              <div className="grid gap-3 sm:grid-cols-[1fr_2fr_auto] sm:items-end">
                <Input
                  label="Request leave for"
                  type="date"
                  value={leave.date}
                  min={it.startDate}
                  max={it.endDate}
                  onChange={(e) => setLeave({ ...leave, date: e.target.value })}
                />
                <Input label="Reason (health or other valid cause)" value={leave.reason} onChange={(e) => setLeave({ ...leave, reason: e.target.value })} />
                <Button
                  variant="outline"
                  disabled={busy}
                  onClick={async () => {
                    if (await act("/internship/me/leave", { date: leave.date || null, reason: leave.reason }, "Leave requested"))
                      setLeave({ date: "", reason: "" });
                  }}
                >
                  Request
                </Button>
              </div>
            ) : null}
            {it.leaveRequests.length ? (
              <div className="space-y-2">
                {it.leaveRequests.map((l) => (
                  <div key={l.date} className="flex flex-wrap items-center justify-between gap-2 rounded-lg bg-white/60 px-3 py-2 dark:bg-nexus-800/60">
                    <span>
                      <span className="font-medium">{formatDate(l.date, "EEE dd MMM")}</span>
                      <span className="text-secondary"> — {l.reason || "No reason given"}</span>
                    </span>
                    <Badge tone={LEAVE_TONE[l.status]}>
                      {l.status === "Pending" ? "Waiting for decision" : l.status === "Rejected" ? "Rejected (unapproved absence)" : "Approved"}
                    </Badge>
                  </div>
                ))}
              </div>
            ) : (
              <p className="text-secondary">No leave requests.</p>
            )}
          </Card>
        </TabPanel>

        <TabPanel value="report">
          <Card
            title="Final report (cl.11 ট)"
            aside={
              <span className="text-xs text-secondary">
                Due {formatDate(it.reportDeadline)} ({policy.reportWorkingDays} working days after the end date)
              </span>
            }
          >
            {it.report ? (
              <div className="flex flex-wrap items-center gap-2">
                <Badge tone={it.report.status === "Accepted" ? "green" : "amber"}>{it.report.status}</Badge>
                <span className="text-xs text-secondary">
                  Submitted {formatDate(it.report.submittedAt)}
                  {it.report.late ? " · late" : ""}
                </span>
              </div>
            ) : null}
            {it.report?.reviewNote ? (
              <p className="rounded-xl bg-white/60 p-3 text-secondary dark:bg-nexus-800/60">Reviewer note: {it.report.reviewNote}</p>
            ) : null}
            {canWriteReport ? (
              <>
                <Textarea
                  label="Report on your work, learning and results"
                  value={report}
                  onChange={(e) => setReport(e.target.value)}
                  rows={8}
                  hint={`At least ${policy.minReportLength} characters · submit it to your department within ${policy.reportWorkingDays} working days of the end date.`}
                />
                <Button disabled={busy} onClick={() => act("/internship/me/report", { text: report }, it.report ? "Report re-submitted" : "Report submitted")}>
                  {it.report ? "Re-submit report" : "Submit report"}
                </Button>
              </>
            ) : it.report ? (
              <p className="whitespace-pre-wrap rounded-xl bg-white/60 p-3 dark:bg-nexus-800/60">{it.report.text}</p>
            ) : (
              <p className="text-secondary">No report was submitted.</p>
            )}
          </Card>
        </TabPanel>

        <TabPanel value="placement" className="grid gap-4 lg:grid-cols-2">
          <Card title="Your placement">
            <div className="grid gap-3 sm:grid-cols-2">
              <Fact label="Department">{it.department.name}</Fact>
              <Fact label="Batch">{app.batchName}</Fact>
            </div>
            <div>
              <p className="text-[11px] font-semibold uppercase tracking-[0.08em] text-slate-500">Your tasks (cl.13)</p>
              <p className="mt-1 text-secondary">{it.tasks}</p>
            </div>
          </Card>
          <Card title="Departments so far (cl.12)">
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
        </TabPanel>

        {it.certificate ? (
          <TabPanel value="certificate">
            <Card title="Certificate (cl.15)" aside={<Badge tone="green">{it.certificate.grade}</Badge>}>
              <p className="text-secondary">
                Issued {formatDate(it.certificate.issuedAt)} · no. {it.certificate.number}
              </p>
              <Button onClick={() => setCert(true)}>
                <Award className="h-4 w-4" /> View certificate
              </Button>
            </Card>
          </TabPanel>
        ) : null}
      </Tabs>

      <Modal open={Boolean(month)} onClose={() => setMonth(null)} title={month ? `Report for ${monthLabel(month.month)}` : ""} size="xl">
        {month?.status === "Report due" ? (
          <div className="space-y-3">
            <Textarea
              label="What you worked on, learned and delivered this month"
              value={monthReport}
              onChange={(e) => setMonthReport(e.target.value)}
              rows={7}
              maxLength={policy.maxMonthlyReportLength}
              hint={`At least ${policy.minMonthlyReportLength} characters · ${monthReport.trim().length}/${policy.maxMonthlyReportLength}`}
            />
            <div className="space-y-1">
              <p className="text-xs font-semibold uppercase tracking-wide text-secondary">Attendance register attached to this bill (cl.11 জ)</p>
              <AttendanceRegister days={month.register} />
            </div>
            <p className="text-xs text-secondary">
              Gross for this month: <strong>{formatCurrency(month.gross)}</strong> · attendance {attendanceLabel(month.attendance)}. Your mentor reviews
              attendance, conduct, discipline and work skill, and sets the absent days to deduct. You cannot edit the report after submitting.
            </p>
            <div className="flex justify-end">
              <Button
                disabled={busy}
                onClick={async () => {
                  if (await act(`/internship/me/bills/${month.month}/submit`, { report: monthReport }, "Monthly report submitted")) setMonth(null);
                }}
              >
                Submit report
              </Button>
            </div>
          </div>
        ) : month ? (
          <div className="space-y-3 text-sm">
            <p className="whitespace-pre-wrap rounded-xl bg-white/60 p-3 dark:bg-nexus-800/60">{month.report}</p>
            <p className="text-xs text-secondary">Submitted {formatDate(month.submittedAt)}</p>
            {month.evaluatedAt ? (
              <div className="rounded-xl border border-slate-200 p-3 dark:border-slate-700">
                <p className="font-semibold">
                  Evaluation: {month.score}/100{month.evaluatedBy ? ` · ${month.evaluatedBy}` : ""}
                </p>
                {month.scores ? <p className="text-xs text-secondary">{scoresLabel(month.scores)} (out of 25 each)</p> : null}
                <p className="text-xs text-secondary">
                  Absent days deducted: {month.deductDays} · net {formatCurrency(month.net)}
                </p>
                <p className="text-secondary">{month.evaluationNote}</p>
              </div>
            ) : (
              <p className="text-secondary">Waiting for your mentor&apos;s evaluation.</p>
            )}
            <div className="space-y-1">
              <p className="text-xs font-semibold uppercase tracking-wide text-secondary">Attendance register</p>
              <AttendanceRegister days={month.register} />
            </div>
          </div>
        ) : null}
      </Modal>

      <PaymentSlipModal slip={slip} onClose={() => setSlip(null)} />

      <Modal open={cert} onClose={() => setCert(false)} title="Certificate of internship" size="lg">
        {it.certificate ? (
          <div className="space-y-4 rounded-2xl border-4 border-double border-nexus-600 p-6 text-center">
            <p className="text-xs uppercase tracking-[0.2em] text-secondary">University Grants Commission of Bangladesh</p>
            <h3 className="text-2xl font-semibold">Internship Completion Certificate</h3>
            <p className="text-sm">This certifies that</p>
            <p className="text-xl font-semibold">{app.student.name}</p>
            <p className="text-sm">
              completed the UGC Internship ({app.batchName}) in the <strong>{it.department.name}</strong> department, {formatDate(it.startDate)} to{" "}
              {formatDate(it.endDate)}, with grade
            </p>
            <p className="text-3xl font-bold text-nexus-700">{it.certificate.grade}</p>
            <div className="flex justify-around pt-6 text-xs text-secondary">
              <span className="border-t border-slate-400 px-6 pt-1">Secretary</span>
              <span className="border-t border-slate-400 px-6 pt-1">Chairman</span>
            </div>
            <p className="text-[11px] text-secondary">No. {it.certificate.number}</p>
          </div>
        ) : null}
      </Modal>
    </div>
  );
}
