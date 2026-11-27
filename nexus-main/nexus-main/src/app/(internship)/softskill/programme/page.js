"use client";

import { useState } from "react";
import Link from "next/link";
import { Award, CalendarRange, Clock, Percent, Users, Wallet } from "lucide-react";
import {
  Badge,
  Button,
  DataTable,
  EmptyState,
  ErrorState,
  Modal,
  PageHeader,
  Select,
  StatCard,
  StatusBadge,
  Tab,
  TabList,
  TabPanel,
  Tabs,
  Textarea,
  Input,
} from "@/components/ui";
import { LuxuryLoader } from "@/components/brand/LuxuryLoader";
import { NoAccess } from "@/components/layout/InternshipShell";
import { useAction, useApiQuery, useSession } from "@/hooks/useApi";
import { api } from "@/lib/api";
import { ATTENDANCE_TONE, SESSION_TONE, SP, STIPEND_TONE } from "@/lib/softskillPermissions";
import { formatCurrency, formatDate } from "@/lib/formatters";
import { Card, ErrorText, Fact, Pct, RuleList, period } from "../_components/ui";

/** How the training runs, cl.6–15 of the UGC Soft Skill Training Policy. */
const JOURNEY = [
  ["Apply", "To an open batch, at your own university (cl.9)"],
  ["Selection", "The Commission screens and selects up to 30 per university (cl.6)"],
  ["Sessions", "48 hours over 3 months, about 4 a week (cl.8, cl.13)"],
  ["Monthly assessment", "On that month's two topics; pass to earn the stipend (cl.10, cl.14)"],
  ["Learning report", "Or a small project, within 15 working days of the end (cl.11 ঝ)"],
  ["Certificate", "On the average of the three assessments (cl.15)"],
];

export default function SoftSkillProgrammePage() {
  const { can } = useSession();
  const canSelf = can(SP.selfView);
  const canProgramme = can(SP.programmeView);
  const setup = useApiQuery("/softskill/setup");
  const mine = useApiQuery(canSelf ? "/softskill/me" : null);
  const [tab, setTab] = useState(null);

  if (!canSelf && !canProgramme) return <NoAccess what="the Soft Skill programme" />;
  if (setup.error || mine.error)
    return <ErrorState title="Could not load the programme" description={(setup.error || mine.error).message} action={<Button onClick={() => (setup.reload(), mine.reload())}>Try again</Button>} />;
  if (!setup.data || (canSelf && mine.data === undefined)) return <LuxuryLoader variant="compact" label="Loading the Soft Skill programme" />;

  const data = mine.data || null;
  const trainee = data?.trainee || null;
  const current = tab || (trainee ? "training" : data ? "application" : "programme");

  return (
    <div className="space-y-2">
      <PageHeader
        title="UGC Soft Skill Training"
        description="Communication, problem solving, innovation, entrepreneurship, AI tools and freelancing — 48 hours at your own university, with a stipend each month you pass."
      />
      <Tabs value={current} onChange={setTab}>
        <TabList>
          {canProgramme ? <Tab value="programme">Programme</Tab> : null}
          {data ? <Tab value="application">My application</Tab> : null}
          {trainee ? <Tab value="training">My training</Tab> : null}
        </TabList>
        {canProgramme ? (
          <TabPanel value="programme">
            <ProgrammeTab
              setup={setup.data}
              mine={data}
              onApplied={(next) => {
                mine.setData(next);
                setTab("application");
              }}
            />
          </TabPanel>
        ) : null}
        {data ? (
          <TabPanel value="application">
            <ApplicationTab app={data.application} onChange={mine.setData} />
          </TabPanel>
        ) : null}
        {trainee ? (
          <TabPanel value="training">
            <TrainingTab setup={setup.data} trainee={trainee} onChange={mine.setData} />
          </TabPanel>
        ) : null}
      </Tabs>
    </div>
  );
}

// ---------- programme and apply ----------

function ProgrammeTab({ setup, mine, onApplied }) {
  const policy = setup.policy;
  const { can } = useSession();
  const elig = useApiQuery("/softskill/me/eligibility");
  const batches = useApiQuery("/softskill/me/open-batches");
  const { run, busy } = useAction();
  const [batch, setBatch] = useState(null);
  const [statement, setStatement] = useState("");
  const [error, setError] = useState("");
  const eligible = Boolean(elig.data?.eligible);
  const blockedByLive = mine && !["Rejected", "Withdrawn"].includes(mine.application.status);

  const submit = async () => {
    setError("");
    try {
      const next = await run(() => api.post("/softskill/me/apply", { batchId: batch.batchId, statement }), "Application submitted", { silentError: true });
      setBatch(null);
      setStatement("");
      elig.reload();
      batches.reload();
      onApplied?.(next);
    } catch (e) {
      setError(e.message);
    }
  };

  return (
    <div className="space-y-5">
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard label="Stipend per month passed" value={formatCurrency(policy.monthlyStipend)} hint="Up to 3 months (cl.10)" icon={<Wallet className="h-5 w-5" />} tone="teal" />
        <StatCard label="Duration" value={`${policy.durationMonths} months`} hint={`${policy.totalHours} hours, not renewable (cl.8)`} icon={<CalendarRange className="h-5 w-5" />} tone="blue" />
        <StatCard label="Seats per university" value={policy.seatsPerUniversity} hint={`${policy.maxBatchesPerYear} batches a year (cl.6)`} icon={<Users className="h-5 w-5" />} tone="violet" />
        <StatCard label="Attendance needed" value={`${policy.minAttendancePct}%`} hint="To finish the training (cl.11 ছ)" icon={<Percent className="h-5 w-5" />} tone="amber" />
      </div>

      <div className="grid gap-5 lg:grid-cols-[3fr_2fr]">
        <div className="space-y-5">
          <Card title="Open batches" aside={batches.data ? <Badge tone="blue">{batches.data.length} open</Badge> : null}>
            {batches.error ? <p className="text-danger">{batches.error.message}</p> : null}
            {batches.data?.length ? (
              <div className="space-y-3">
                {batches.data.map((b) => (
                  <div key={b.batchId} className="space-y-3 rounded-xl border border-slate-200 p-4 dark:border-slate-700">
                    <div className="flex flex-wrap items-start justify-between gap-3">
                      <div>
                        <p className="text-base font-semibold">
                          {b.name} <span className="text-xs font-normal text-secondary">{b.code}</span>
                        </p>
                        <p className="text-secondary">Training {period(b.startDate, b.endDate)}</p>
                      </div>
                      {can(SP.apply) ? (
                        <Button
                          disabled={!eligible || blockedByLive || !b.universityGroupId}
                          onClick={() => {
                            setError("");
                            setBatch(b);
                          }}
                        >
                          {blockedByLive ? "Application in progress" : "Apply"}
                        </Button>
                      ) : null}
                    </div>
                    <div className="grid gap-3 sm:grid-cols-2">
                      <Fact label="Applications">{period(b.applyFrom, b.applyTo)}</Fact>
                      {b.universityGroupId ? (
                        <Fact label={`Seats at ${b.university}`}>
                          {b.seatsLeft} of {policy.seatsPerUniversity} open{b.venue ? ` · ${b.venue}` : ""}
                        </Fact>
                      ) : (
                        <Fact label="Your university">Not taking part in this batch</Fact>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            ) : batches.data ? (
              <EmptyState title="No batch is open right now" description="The Commission calls for applications twice a year at most (cl.6 খ, cl.9)." />
            ) : (
              <p className="text-secondary">Loading…</p>
            )}
          </Card>

          <Card title="How it works" description="Held at your university, alongside your regular classes (cl.8).">
            <ol className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
              {JOURNEY.map(([title, text], i) => (
                <li key={title} className="flex gap-3 rounded-xl bg-white/60 p-3 dark:bg-nexus-800/60">
                  <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-nexus-600 text-xs font-semibold text-white">{i + 1}</span>
                  <span>
                    <span className="block font-semibold">{title}</span>
                    <span className="text-xs text-secondary">{text}</span>
                  </span>
                </li>
              ))}
            </ol>
            <div>
              <p className="mb-2 text-xs font-semibold uppercase text-slate-500">Six topics, 8 hours each (cl.13)</p>
              <ul className="grid gap-1 sm:grid-cols-2">
                {setup.topics.map((t) => (
                  <li key={t.id} className="text-xs">
                    <span className="font-medium">{t.name}</span> <span className="text-secondary">· month {t.assessmentNo}</span>
                  </li>
                ))}
              </ul>
            </div>
            <p className="text-xs text-secondary">Certificate categories: {setup.gradeBands.map((g) => `${g.grade} (${g.min}%+)`).join(" · ")}.</p>
          </Card>
        </div>

        <Card
          title="Your eligibility"
          description="Checked against cl.7 of the policy and your profile."
          aside={elig.data ? <Badge tone={eligible ? "green" : "red"}>{eligible ? "Eligible" : "Not eligible"}</Badge> : null}
          className="h-fit lg:sticky lg:top-24"
        >
          {elig.error ? <p className="text-danger">{elig.error.message}</p> : null}
          {elig.loading && !elig.data ? <p className="text-secondary">Checking…</p> : null}
          <RuleList rules={elig.data?.rules} />
        </Card>
      </div>

      <Modal open={Boolean(batch)} onClose={() => setBatch(null)} title={`Apply to ${batch?.name || ""}`} description={`At ${batch?.university || ""}. The Commission screens every application (cl.9).`} size="lg">
        <div className="space-y-4">
          <Textarea
            label="Why do you want to join?"
            value={statement}
            onChange={(e) => setStatement(e.target.value)}
            rows={4}
            hint={`At least ${policy.minStatementLength} characters. Your documents from My documents are attached as they are now.`}
            required
          />
          <p className="text-xs text-secondary">
            By applying you agree to attend every session on time and keep to the rules of cl.11.{" "}
            <Link href="/account/documents" className="font-medium text-nexus-700 hover:underline dark:text-nexus-300">
              Check My documents
            </Link>
          </p>
          <ErrorText>{error}</ErrorText>
          <div className="flex justify-end gap-2">
            <Button variant="ghost" onClick={() => setBatch(null)}>
              Cancel
            </Button>
            <Button onClick={submit} disabled={busy}>
              Submit application
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
}

// ---------- application ----------

function ApplicationTab({ app, onChange }) {
  const { can } = useSession();
  const { run, busy } = useAction();
  const withdrawable = ["Submitted", "Shortlisted", "Waitlisted", "Selected"].includes(app.status);

  const withdraw = async () => {
    const next = await run(() => api.post("/softskill/me/withdraw"), "Application withdrawn");
    if (next) onChange(next);
  };

  return (
    <div className="grid gap-5 lg:grid-cols-[3fr_2fr]">
      <Card title={app.batchName} description={`${app.batchCode} · ${app.university}`} aside={<StatusBadge status={app.status} />}>
        <dl className="grid gap-3 sm:grid-cols-2">
          <Fact label="Applied">{formatDate(app.appliedAt)}</Fact>
          <Fact label="Qualified as">{app.basis}</Fact>
          <Fact label="Documents sent">{app.documents.join(", ") || "—"}</Fact>
          {app.screeningScore != null ? <Fact label="Screening score">{app.screeningScore}/100</Fact> : null}
        </dl>
        <div>
          <p className="text-xs text-secondary">Statement</p>
          <p className="whitespace-pre-line">{app.statement}</p>
        </div>
        {app.rejectionReason ? <p className="rounded-lg bg-red-50 p-3 text-danger">{app.rejectionReason}</p> : null}
        {withdrawable && can(SP.apply) ? (
          <Button variant="outline" disabled={busy} onClick={withdraw}>
            Withdraw application
          </Button>
        ) : null}
      </Card>
      <Card title="History">
        <ol className="space-y-3">
          {app.history.map((h, i) => (
            <li key={i} className="flex gap-3">
              <span className="mt-1.5 h-2 w-2 shrink-0 rounded-full bg-nexus-600" />
              <span>
                <span className="font-medium">{h.status}</span> <span className="text-xs text-secondary">{formatDate(h.at, "dd MMM yyyy, HH:mm")}</span>
                {h.note ? <span className="block text-xs text-secondary">{h.note}</span> : null}
              </span>
            </li>
          ))}
        </ol>
      </Card>
    </div>
  );
}

// ---------- the running training ----------

function TrainingTab({ setup, trainee: t, onChange }) {
  const { can } = useSession();
  const { run, busy } = useAction();
  const policy = setup.policy;
  const [leave, setLeave] = useState(null);
  const [leaveForm, setLeaveForm] = useState({ reasonType: "Health", reason: "" });
  const [report, setReport] = useState(null);
  const [error, setError] = useState("");
  const running = t.status === "Active";
  const reportOpen = ["Active", "Completed"].includes(t.status) && t.report?.status !== "Accepted";

  const askLeave = async () => {
    setError("");
    try {
      const next = await run(() => api.post("/softskill/me/leave", { sessionId: leave.id, ...leaveForm }), "Leave requested", { silentError: true });
      onChange(next);
      setLeave(null);
    } catch (e) {
      setError(e.message);
    }
  };
  const sendReport = async () => {
    setError("");
    try {
      const next = await run(() => api.post("/softskill/me/report", report), "Report submitted", { silentError: true });
      onChange(next);
      setReport(null);
    } catch (e) {
      setError(e.message);
    }
  };

  return (
    <div className="space-y-5">
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard label="Attendance" value={`${t.attendance.pct}%`} hint={`${t.attendance.attendedHours} of ${t.attendance.heldHours} hours held · need ${policy.minAttendancePct}%`} icon={<Clock className="h-5 w-5" />} tone={t.attendance.belowMin ? "amber" : "teal"} />
        <StatCard label="Assessments passed" value={`${t.assessments.filter((a) => a.passed).length} of 3`} hint={`Pass mark ${policy.assessmentPassMarks}/${policy.assessmentMaxMarks}`} icon={<Percent className="h-5 w-5" />} tone="blue" />
        <StatCard label="Stipend paid" value={formatCurrency(t.stipends.filter((s) => s.status === "Paid").reduce((n, s) => n + s.amount, 0))} hint="One per month passed (cl.10)" icon={<Wallet className="h-5 w-5" />} tone="violet" />
        <StatCard label="Certificate" value={t.certificate?.grade || (t.computed.grade ? `${t.computed.grade} (so far)` : "—")} hint={t.computed.average != null ? `Average ${t.computed.average}%` : "After all three assessments"} icon={<Award className="h-5 w-5" />} tone="green" />
      </div>

      {t.cancelReason ? <p className="rounded-lg bg-red-50 p-3 text-sm text-danger">Participation cancelled: {t.cancelReason}</p> : null}
      {t.certificate ? (
        <Card title="Certificate" aside={<Badge tone="green">{t.certificate.grade}</Badge>}>
          <dl className="grid gap-3 sm:grid-cols-3">
            <Fact label="Number">{t.certificate.number}</Fact>
            <Fact label="Average">{t.certificate.average}%</Fact>
            <Fact label="Issued">{formatDate(t.certificate.issuedAt)}</Fact>
          </dl>
        </Card>
      ) : null}
      {t.outcomeReasons?.length ? (
        <Card title="Not certified">
          <ul className="list-disc pl-5 text-danger">
            {t.outcomeReasons.map((r) => (
              <li key={r}>{r}</li>
            ))}
          </ul>
        </Card>
      ) : null}

      <div className="grid gap-5 lg:grid-cols-[3fr_2fr]">
        <Card title="Sessions" description={`${t.university.name}${t.venue ? ` · ${t.venue}` : ""} · ${period(t.startDate, t.endDate)}`}>
          <DataTable
            emptyMessage="The coordinator has not scheduled sessions yet."
            rows={t.sessions}
            columns={[
              { key: "date", label: "Date", render: (s) => `${formatDate(s.session.date)} · ${s.session.startTime}–${s.session.endTime}` },
              { key: "topic", label: "Topic", render: (s) => <span title={s.session.trainer.name}>{s.session.topic}</span> },
              {
                key: "att",
                label: "Attendance",
                render: (s) =>
                  s.attendance ? (
                    <Badge tone={ATTENDANCE_TONE[s.attendance] || "slate"}>{s.attendance}</Badge>
                  ) : (
                    <Badge tone={SESSION_TONE[s.session.status]}>{s.session.status}</Badge>
                  ),
              },
              {
                key: "act",
                label: "",
                render: (s) =>
                  running && can(SP.selfLeave) && s.session.status === "Scheduled" && !s.attendance ? (
                    <Button size="sm" variant="ghost" onClick={() => (setError(""), setLeave(s.session))}>
                      Ask leave
                    </Button>
                  ) : s.reason ? (
                    <span className="text-xs text-secondary">{s.reason}</span>
                  ) : null,
              },
            ]}
          />
        </Card>

        <div className="space-y-5">
          <Card title="Monthly assessments" description="Each covers that month's two topics (cl.14 ক).">
            <ul className="space-y-2">
              {t.assessments.map((a) => (
                <li key={a.no} className="flex items-center justify-between gap-2">
                  <span>
                    Month {a.no} <span className="text-xs text-secondary">{a.date ? formatDate(a.date) : "not set"}</span>
                  </span>
                  {a.passed == null ? (
                    <Badge tone="slate">Pending</Badge>
                  ) : a.absent ? (
                    <Badge tone="red">Absent</Badge>
                  ) : (
                    <Badge tone={a.passed ? "green" : "red"}>
                      {a.marks}/{a.maxMarks}
                    </Badge>
                  )}
                </li>
              ))}
            </ul>
          </Card>
          <Card title="Stipend" description="Paid by the Commission's finance section for each month you pass (cl.10).">
            <ul className="space-y-2">
              {t.stipends.map((s) => (
                <li key={s.no} className="space-y-0.5">
                  <div className="flex items-center justify-between gap-2">
                    <span>Month {s.no}</span>
                    <Badge tone={STIPEND_TONE[s.status]}>
                      {s.status}
                      {s.status === "Paid" ? ` · ${formatCurrency(s.amount)}` : ""}
                    </Badge>
                  </div>
                  {s.reason ? <p className="text-xs text-secondary">{s.reason}</p> : null}
                  {s.slipNumber ? <p className="text-xs text-secondary">Slip {s.slipNumber}</p> : null}
                </li>
              ))}
            </ul>
          </Card>
          <Card
            title="Learning report"
            description={`A report or small project, due by ${formatDate(t.reportDeadline)} (cl.11 ঝ).`}
            aside={t.report ? <StatusBadge status={t.report.status} /> : null}
          >
            {t.report ? (
              <div className="space-y-1">
                <p className="font-medium">
                  {t.report.title} <span className="text-xs text-secondary">({t.report.type === "MiniProject" ? "small project" : "report"})</span>
                </p>
                <p className="text-xs text-secondary">
                  Submitted {formatDate(t.report.submittedAt)}
                  {t.report.late ? " · late" : ""}
                </p>
                {t.report.reviewNote ? <p className="rounded-lg bg-[#f7f1e6] p-2 text-xs">{t.report.reviewNote}</p> : null}
              </div>
            ) : (
              <p className="text-secondary">Not submitted yet.</p>
            )}
            {reportOpen && can(SP.selfReport) ? (
              <Button
                size="sm"
                onClick={() => {
                  setError("");
                  setReport({ type: t.report?.type || "Report", title: t.report?.title || "", text: t.report?.text || "" });
                }}
              >
                {t.report ? "Revise and resubmit" : "Submit report"}
              </Button>
            ) : null}
          </Card>
        </div>
      </div>

      <Modal open={Boolean(leave)} onClose={() => setLeave(null)} title="Ask the coordinator for leave" description={leave ? `${leave.topic}, ${formatDate(leave.date)}` : ""}>
        <div className="space-y-4">
          <Select
            label="Reason"
            value={leaveForm.reasonType}
            onChange={(e) => setLeaveForm((f) => ({ ...f, reasonType: e.target.value }))}
            options={[
              { value: "Health", label: "Health" },
              { value: "Other", label: "Another acceptable reason" },
            ]}
          />
          <Textarea label="Details" value={leaveForm.reason} onChange={(e) => setLeaveForm((f) => ({ ...f, reason: e.target.value }))} rows={3} hint="cl.11 ছ: absence only for health or another acceptable reason. You still need 80% overall." />
          <ErrorText>{error}</ErrorText>
          <div className="flex justify-end gap-2">
            <Button variant="ghost" onClick={() => setLeave(null)}>
              Cancel
            </Button>
            <Button onClick={askLeave} disabled={busy}>
              Send request
            </Button>
          </div>
        </div>
      </Modal>

      <Modal open={Boolean(report)} onClose={() => setReport(null)} title="Learning report or small project" size="lg">
        {report ? (
          <div className="space-y-4">
            <Select
              label="Type"
              value={report.type}
              onChange={(e) => setReport((r) => ({ ...r, type: e.target.value }))}
              options={[
                { value: "Report", label: "Learning report" },
                { value: "MiniProject", label: "Small project" },
              ]}
            />
            <Input label="Title" value={report.title} onChange={(e) => setReport((r) => ({ ...r, title: e.target.value }))} maxLength={200} required />
            <Textarea
              label="What you learned or built"
              value={report.text}
              onChange={(e) => setReport((r) => ({ ...r, text: e.target.value }))}
              rows={8}
              hint={`At least ${policy.minReportLength} characters.`}
              required
            />
            <ErrorText>{error}</ErrorText>
            <div className="flex justify-end gap-2">
              <Button variant="ghost" onClick={() => setReport(null)}>
                Cancel
              </Button>
              <Button onClick={sendReport} disabled={busy}>
                Submit
              </Button>
            </div>
          </div>
        ) : null}
      </Modal>
    </div>
  );
}
