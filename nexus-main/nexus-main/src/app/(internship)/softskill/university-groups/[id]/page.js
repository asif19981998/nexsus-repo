"use client";

import { useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import {
  Badge,
  Button,
  Checkbox,
  DataTable,
  EmptyState,
  ErrorState,
  Input,
  Modal,
  PageHeader,
  Select,
  StatusBadge,
  Tab,
  TabList,
  TabPanel,
  Tabs,
  Textarea,
} from "@/components/ui";
import { LuxuryLoader } from "@/components/brand/LuxuryLoader";
import { useAction, useApiQuery, useSession } from "@/hooks/useApi";
import { api } from "@/lib/api";
import { SESSION_TONE, SP } from "@/lib/softskillPermissions";
import { formatDate } from "@/lib/formatters";
import { Card, ErrorText, Fact, Pct, period, today } from "../../_components/ui";
import { VisitCard, VisitReportModal } from "../../_components/oversight";

const LEAVE_TONE = { Pending: "amber", Approved: "green", Rejected: "red", Attended: "blue" };

/**
 * One university group. The coordinator plans sessions, takes attendance, decides leave, sets assessments and marks,
 * reviews learning reports and sends the final report (cl.12 গ, cl.14). A trainer sees the university group, takes attendance at
 * their own sessions and enters marks. The server checks every action against the university group.
 */
export default function UniversityGroupPage() {
  const { id } = useParams();
  const { user, can } = useSession();
  const universityGroup = useApiQuery(`/softskill/university-groups/${id}`);
  const setup = useApiQuery("/softskill/setup");
  const [tab, setTab] = useState("schedule");

  if (universityGroup.error) return <ErrorState title="Could not load the university group" description={universityGroup.error.message} action={<Button onClick={universityGroup.reload}>Try again</Button>} />;
  if (!universityGroup.data || !setup.data) return <LuxuryLoader variant="compact" label="Loading the university group" />;

  const d = universityGroup.data;
  const c = d.universityGroup;
  const pending = d.leaveRequests.filter((l) => l.status === "Pending").length;
  const back = can(SP.universityGroupViewAll) ? "/softskill/manage?tab=batches" : can(SP.monitorVisit) ? "/softskill/monitoring" : "/softskill/trainer";
  const role = d.access.coordinate
    ? ["violet", "You coordinate this university group"]
    : d.access.monitor
      ? ["amber", "Monitor view (read-only)"]
      : can(SP.universityGroupViewAll)
        ? ["slate", "Commission view"]
        : ["blue", "Trainer view"];
  const openVisits = (d.visits || []).filter((v) => v.status === "Planned").length;

  return (
    <div className="space-y-2">
      <PageHeader
        title={`${c.university.name} — ${c.batchName}`}
        description={`${c.batchCode} · ${period(c.startDate, c.endDate)} · ${c.taken} trainees · Coordinator: ${c.coordinator?.name || "not named yet"}${c.venue ? ` · ${c.venue}` : ""}`}
        actions={
          <Link href={back} className="text-sm font-medium text-nexus-700 hover:underline dark:text-nexus-300">
            ← Back
          </Link>
        }
      />
      <div className="flex flex-wrap gap-2 pb-2">
        <StatusBadge status={c.batchStatus} />
        <Badge tone={role[0]}>{role[1]}</Badge>
      </div>
      <Tabs value={tab} onChange={setTab}>
        <TabList>
          <Tab value="schedule">Schedule</Tab>
          <Tab value="trainees">Trainees</Tab>
          <Tab value="leave">Leave{pending ? ` (${pending})` : ""}</Tab>
          <Tab value="assessments">Assessments</Tab>
          <Tab value="final">Final report</Tab>
          <Tab value="monitoring">Monitoring{openVisits ? ` (${openVisits})` : ""}</Tab>
        </TabList>
        <TabPanel value="schedule">
          <ScheduleTab d={d} me={user} onChange={universityGroup.setData} />
        </TabPanel>
        <TabPanel value="trainees">
          <TraineesTab d={d} policy={setup.data.policy} onChange={universityGroup.setData} />
        </TabPanel>
        <TabPanel value="leave">
          <LeaveTab d={d} onChange={universityGroup.setData} />
        </TabPanel>
        <TabPanel value="assessments">
          <AssessmentsTab d={d} onChange={universityGroup.setData} />
        </TabPanel>
        <TabPanel value="final">
          <FinalReportTab d={d} policy={setup.data.policy} onChange={universityGroup.setData} />
        </TabPanel>
        <TabPanel value="monitoring">
          <MonitoringTab d={d} me={user} onReported={universityGroup.reload} />
        </TabPanel>
      </Tabs>
    </div>
  );
}

// ---------- schedule and attendance ----------

const EMPTY_SESSION = { topicId: "", trainerId: "", date: "", startTime: "10:00", endTime: "14:00", note: "" };

function ScheduleTab({ d, me, onChange }) {
  const { run, busy } = useAction();
  const [editing, setEditing] = useState(null); // { id?, form }
  const [sheet, setSheet] = useState(null); // { session, marks }
  const [error, setError] = useState("");
  const running = d.universityGroup.batchStatus === "Running";
  const active = d.trainees.filter((t) => t.status === "Active");

  const saveSession = async () => {
    setError("");
    const f = editing.form;
    const body = { ...f, topicId: f.topicId ? Number(f.topicId) : null, trainerId: f.trainerId ? Number(f.trainerId) : null, date: f.date || null };
    try {
      const next = await run(
        () => (editing.id ? api.put(`/softskill/university-groups/${d.universityGroup.id}/sessions/${editing.id}`, body) : api.post(`/softskill/university-groups/${d.universityGroup.id}/sessions`, body)),
        editing.id ? "Session updated" : "Session added",
        { silentError: true },
      );
      onChange(next);
      setEditing(null);
    } catch (e) {
      setError(e.message);
    }
  };
  const cancel = async (s) => {
    const next = await run(() => api.post(`/softskill/university-groups/${d.universityGroup.id}/sessions/${s.id}/cancel`), "Session cancelled");
    if (next) onChange(next);
  };
  const openSheet = (s) => {
    setError("");
    const byTrainee = {};
    active.forEach((t) => (byTrainee[t.id] = { status: "Present", checkIn: s.startTime, checkOut: s.endTime }));
    setSheet({ session: s, marks: byTrainee });
  };
  const saveSheet = async () => {
    setError("");
    const marks = Object.entries(sheet.marks).map(([traineeId, m]) => ({ traineeId: Number(traineeId), ...m }));
    try {
      const next = await run(() => api.post(`/softskill/university-groups/${d.universityGroup.id}/sessions/${sheet.session.id}/attendance`, { marks }), "Attendance saved", { silentError: true });
      onChange(next);
      setSheet(null);
    } catch (e) {
      setError(e.message);
    }
  };
  const setMark = (traineeId, key, value) => setSheet((s) => ({ ...s, marks: { ...s.marks, [traineeId]: { ...s.marks[traineeId], [key]: value } } }));

  return (
    <div className="space-y-5">
      <Card title="Topics (cl.13)" description="Each topic has 8 hours; assessments cover two topics a month.">
        <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-3">
          {d.topics.map((t) => (
            <div key={t.id} className="rounded-lg border border-slate-200 p-3 dark:border-slate-700">
              <p className="font-medium">{t.name}</p>
              <p className="text-xs text-secondary">
                Month {t.assessmentNo} · planned {t.scheduled}/{t.hours} h · held {t.held} h
              </p>
            </div>
          ))}
        </div>
      </Card>

      <Card
        title="Sessions"
        aside={
          d.access.coordinate ? (
            <Button size="sm" onClick={() => (setError(""), setEditing({ form: { ...EMPTY_SESSION } }))}>
              Add session
            </Button>
          ) : null
        }
      >
        <DataTable
          emptyMessage="No session planned yet."
          rows={d.sessions}
          columns={[
            { key: "date", label: "Date", render: (s) => `${formatDate(s.date)} · ${s.startTime}–${s.endTime}` },
            { key: "topic", label: "Topic", render: (s) => s.topic },
            { key: "trainer", label: "Trainer", render: (s) => s.trainer.name },
            { key: "status", label: "Status", render: (s) => <Badge tone={SESSION_TONE[s.status]}>{s.status}</Badge> },
            { key: "present", label: "Present", render: (s) => (s.status === "Held" ? `${s.present}/${s.marked}` : "—") },
            {
              key: "act",
              label: "",
              render: (s) => {
                const mayTake = running && s.status !== "Cancelled" && s.date <= today() && (d.access.takeAttendanceAll || s.trainer.id === me?.id);
                return (
                  <div className="flex gap-1.5">
                    {mayTake ? (
                      <Button size="sm" onClick={() => openSheet(s)}>
                        {s.status === "Held" ? "Re-take" : "Attendance"}
                      </Button>
                    ) : null}
                    {d.access.coordinate && s.status === "Scheduled" ? (
                      <>
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => (
                            setError(""),
                            setEditing({ id: s.id, form: { topicId: String(s.topicId), trainerId: String(s.trainer.id), date: s.date, startTime: s.startTime, endTime: s.endTime, note: s.note || "" } })
                          )}
                        >
                          Edit
                        </Button>
                        <Button size="sm" variant="ghost" disabled={busy} onClick={() => cancel(s)}>
                          Cancel
                        </Button>
                      </>
                    ) : null}
                  </div>
                );
              },
            },
          ]}
        />
      </Card>

      <Modal open={Boolean(editing)} onClose={() => setEditing(null)} title={editing?.id ? "Edit session" : "Add session"} description={`Within ${period(d.universityGroup.startDate, d.universityGroup.endDate)}.`}>
        {editing ? (
          <div className="space-y-4">
            <Select
              label="Topic"
              value={editing.form.topicId}
              placeholder="Choose a topic"
              onChange={(e) => setEditing((x) => ({ ...x, form: { ...x.form, topicId: e.target.value } }))}
              options={d.topics.map((t) => ({ value: String(t.id), label: `${t.name} (${Math.max(0, t.hours - t.scheduled)} h left)` }))}
            />
            <Select
              label="Trainer"
              value={editing.form.trainerId}
              placeholder="Choose a trainer"
              onChange={(e) => setEditing((x) => ({ ...x, form: { ...x.form, trainerId: e.target.value } }))}
              options={d.trainers.map((t) => ({ value: String(t.userId), label: `${t.name}${t.canTrain ? "" : " — ToT pending"}` }))}
              hint="Approved trainers of this university. A trainer without the ToT cannot take sessions (cl.12 খ)."
            />
            <div className="grid gap-4 sm:grid-cols-3">
              <Input label="Date" type="date" value={editing.form.date} onChange={(e) => setEditing((x) => ({ ...x, form: { ...x.form, date: e.target.value } }))} />
              <Input label="Starts" type="time" value={editing.form.startTime} onChange={(e) => setEditing((x) => ({ ...x, form: { ...x.form, startTime: e.target.value } }))} />
              <Input label="Ends" type="time" value={editing.form.endTime} onChange={(e) => setEditing((x) => ({ ...x, form: { ...x.form, endTime: e.target.value } }))} />
            </div>
            <Input label="Note (optional)" value={editing.form.note} onChange={(e) => setEditing((x) => ({ ...x, form: { ...x.form, note: e.target.value } }))} />
            <ErrorText>{error}</ErrorText>
            <div className="flex justify-end gap-2">
              <Button variant="ghost" onClick={() => setEditing(null)}>
                Cancel
              </Button>
              <Button onClick={saveSession} disabled={busy}>
                Save
              </Button>
            </div>
          </div>
        ) : null}
      </Modal>

      <Modal open={Boolean(sheet)} onClose={() => setSheet(null)} title="Attendance" description={sheet ? `${sheet.session.topic}, ${formatDate(sheet.session.date)} — arrival and departure (cl.11 চ)` : ""} size="lg">
        {sheet ? (
          <div className="space-y-4">
            {active.length ? (
              <ul className="divide-y divide-slate-200 dark:divide-slate-700">
                {active.map((t) => {
                  const m = sheet.marks[t.id];
                  return (
                    <li key={t.id} className="grid items-end gap-3 py-2 sm:grid-cols-[2fr_1.2fr_1fr_1fr]">
                      <span className="font-medium">{t.student.name}</span>
                      <Select
                        label="Mark"
                        value={m.status}
                        onChange={(e) => setMark(t.id, "status", e.target.value)}
                        options={[
                          { value: "Present", label: "Present" },
                          { value: "Absent", label: "Absent" },
                          ...(d.access.coordinate ? [{ value: "Leave", label: "Leave" }] : []),
                        ]}
                      />
                      <Input label="In" type="time" value={m.checkIn} disabled={m.status !== "Present"} onChange={(e) => setMark(t.id, "checkIn", e.target.value)} />
                      <Input label="Out" type="time" value={m.checkOut} disabled={m.status !== "Present"} onChange={(e) => setMark(t.id, "checkOut", e.target.value)} />
                    </li>
                  );
                })}
              </ul>
            ) : (
              <EmptyState title="No active trainee" description="Trainees appear once the batch is running." />
            )}
            <p className="text-xs text-secondary">Approved leave stays as leave. Anyone not marked present is absent.</p>
            <ErrorText>{error}</ErrorText>
            <div className="flex justify-end gap-2">
              <Button variant="ghost" onClick={() => setSheet(null)}>
                Cancel
              </Button>
              <Button onClick={saveSheet} disabled={busy}>
                Save attendance
              </Button>
            </div>
          </div>
        ) : null}
      </Modal>
    </div>
  );
}

// ---------- trainees and learning reports ----------

function TraineesTab({ d, policy, onChange }) {
  const { run, busy } = useAction();
  const [review, setReview] = useState(null); // { trainee, accept, note }
  const [error, setError] = useState("");

  const send = async () => {
    setError("");
    try {
      const next = await run(
        () => api.post(`/softskill/university-groups/${d.universityGroup.id}/reports/review`, { traineeId: review.trainee.id, accept: review.accept, note: review.note }),
        review.accept ? "Report accepted" : "Revision requested",
        { silentError: true },
      );
      onChange(next);
      setReview(null);
    } catch (e) {
      setError(e.message);
    }
  };

  return (
    <Card title="Trainees" description={`Attendance needs ${policy.minAttendancePct}% (cl.11 ছ). Learning reports are due by ${formatDate(d.reportDeadline)} (cl.11 ঝ).`}>
      <DataTable
        emptyMessage="No trainee selected for this university group yet."
        rows={d.trainees}
        columns={[
          { key: "name", label: "Trainee", render: (t) => <span className="font-medium">{t.student.name}</span> },
          { key: "status", label: "Status", render: (t) => <StatusBadge status={t.status} /> },
          { key: "att", label: "Attendance", render: (t) => <Pct value={t.attendance.pct} min={policy.minAttendancePct} /> },
          {
            key: "marks",
            label: "Assessments",
            render: (t) =>
              t.assessments.map((a) => (a.passed == null ? "—" : a.absent ? "Abs" : `${a.marks}`)).join(" · "),
          },
          {
            key: "report",
            label: "Report",
            render: (t) =>
              t.report ? (
                <div className="space-y-1">
                  <StatusBadge status={t.report.status} />
                  {t.report.late ? <Badge tone="amber">Late</Badge> : null}
                </div>
              ) : (
                <span className="text-xs text-secondary">Not submitted</span>
              ),
          },
          {
            key: "act",
            label: "",
            render: (t) =>
              d.access.coordinate && t.report?.status === "Submitted" ? (
                <Button size="sm" variant="outline" onClick={() => (setError(""), setReview({ trainee: t, accept: true, note: "" }))}>
                  Review report
                </Button>
              ) : null,
          },
        ]}
      />
      <Modal open={Boolean(review)} onClose={() => setReview(null)} title={review ? `${review.trainee.student.name}: ${review.trainee.report.title}` : ""} size="lg">
        {review ? (
          <div className="space-y-4">
            <p className="text-xs text-secondary">{review.trainee.report.type === "MiniProject" ? "Small project" : "Learning report"} · submitted {formatDate(review.trainee.report.submittedAt)}</p>
            <p className="max-h-72 overflow-y-auto whitespace-pre-line rounded-lg bg-white/60 p-3 text-sm dark:bg-nexus-800/60">{review.trainee.report.text}</p>
            <Checkbox label="Accept this report" checked={review.accept} onChange={(e) => setReview((r) => ({ ...r, accept: e.target.checked }))} />
            <Textarea label={review.accept ? "Note (optional)" : "What needs to change"} value={review.note} onChange={(e) => setReview((r) => ({ ...r, note: e.target.value }))} rows={3} />
            <ErrorText>{error}</ErrorText>
            <div className="flex justify-end gap-2">
              <Button variant="ghost" onClick={() => setReview(null)}>
                Cancel
              </Button>
              <Button onClick={send} disabled={busy}>
                {review.accept ? "Accept" : "Ask for revision"}
              </Button>
            </div>
          </div>
        ) : null}
      </Modal>
    </Card>
  );
}

// ---------- leave ----------

function LeaveTab({ d, onChange }) {
  const { run, busy } = useAction();
  const decide = async (l, approve) => {
    const next = await run(() => api.post(`/softskill/university-groups/${d.universityGroup.id}/leave/decide`, { attendanceId: l.attendanceId, approve }), approve ? "Leave approved" : "Leave rejected");
    if (next) onChange(next);
  };
  return (
    <Card title="Leave requests" description="Trainees ask through the coordinator, for health or another acceptable reason (cl.11 ছ).">
      <DataTable
        emptyMessage="No leave requests."
        rows={d.leaveRequests}
        columns={[
          { key: "who", label: "Trainee", render: (l) => <span className="font-medium">{l.student}</span> },
          { key: "when", label: "Session", render: (l) => `${formatDate(l.date)} · ${l.topic}` },
          { key: "why", label: "Reason", render: (l) => `${l.reasonType || ""}${l.reason ? `: ${l.reason}` : ""}` },
          { key: "status", label: "Status", render: (l) => <Badge tone={LEAVE_TONE[l.status] || "slate"}>{l.status}</Badge> },
          {
            key: "act",
            label: "",
            render: (l) =>
              d.access.coordinate && l.status === "Pending" ? (
                <div className="flex gap-1.5">
                  <Button size="sm" disabled={busy} onClick={() => decide(l, true)}>
                    Approve
                  </Button>
                  <Button size="sm" variant="ghost" disabled={busy} onClick={() => decide(l, false)}>
                    Reject
                  </Button>
                </div>
              ) : null,
          },
        ]}
      />
    </Card>
  );
}

// ---------- assessments ----------

function AssessmentsTab({ d, onChange }) {
  const { run, busy } = useAction();
  const [creating, setCreating] = useState(null); // { no, date }
  const [entering, setEntering] = useState(null); // { assessment, rows }
  const [error, setError] = useState("");
  const missing = [1, 2, 3].filter((n) => !d.assessments.some((a) => a.no === n));
  const enterable = d.trainees.filter((t) => ["Active", "Completed"].includes(t.status));

  const create = async () => {
    setError("");
    try {
      const next = await run(() => api.post(`/softskill/university-groups/${d.universityGroup.id}/assessments`, { no: Number(creating.no), date: creating.date || null }), "Assessment set", { silentError: true });
      onChange(next);
      setCreating(null);
    } catch (e) {
      setError(e.message);
    }
  };
  const openEntry = (a) => {
    setError("");
    const rows = {};
    enterable.forEach((t) => {
      const r = t.assessments.find((x) => x.no === a.no);
      rows[t.id] = { marks: r?.marks ?? "", absent: r?.absent ?? false, malpractice: r?.malpractice ?? false, note: r?.note || "" };
    });
    setEntering({ assessment: a, rows });
  };
  const saveResults = async () => {
    setError("");
    const results = Object.entries(entering.rows).map(([traineeId, r]) => ({
      traineeId: Number(traineeId),
      marks: r.absent || r.marks === "" ? null : Number(r.marks),
      absent: r.absent,
      malpractice: r.malpractice,
      note: r.note || null,
    }));
    try {
      const next = await run(() => api.post(`/softskill/university-groups/${d.universityGroup.id}/assessments/${entering.assessment.id}/results`, { results }), "Marks saved", { silentError: true });
      onChange(next);
      setEntering(null);
    } catch (e) {
      setError(e.message);
    }
  };
  const setRow = (id, key, value) => setEntering((x) => ({ ...x, rows: { ...x.rows, [id]: { ...x.rows[id], [key]: value } } }));

  return (
    <Card
      title="Monthly assessments (cl.14)"
      description="One at the end of each month on that month's two topics. A passed month earns that month's stipend (cl.10)."
      aside={
        d.access.enterMarks && missing.length ? (
          <Button size="sm" onClick={() => (setError(""), setCreating({ no: String(missing[0]), date: today() }))}>
            Set assessment
          </Button>
        ) : null
      }
    >
      {d.assessments.length ? (
        <DataTable
          rows={d.assessments}
          columns={[
            { key: "no", label: "Month", render: (a) => `Assessment ${a.no}` },
            { key: "date", label: "Date", render: (a) => formatDate(a.date) },
            { key: "marks", label: "Marks", render: (a) => `Pass ${a.passMarks}/${a.maxMarks}` },
            { key: "done", label: "Entered", render: (a) => `${a.entered} · passed ${a.passed}` },
            {
              key: "act",
              label: "",
              render: (a) =>
                d.access.enterMarks && a.date <= today() ? (
                  <Button size="sm" variant="outline" onClick={() => openEntry(a)}>
                    Enter marks
                  </Button>
                ) : null,
            },
          ]}
        />
      ) : (
        <EmptyState title="No assessment set" description="Set each month's assessment when its date is fixed." />
      )}

      <Modal open={Boolean(creating)} onClose={() => setCreating(null)} title="Set an assessment">
        {creating ? (
          <div className="space-y-4">
            <Select label="Month" value={creating.no} onChange={(e) => setCreating((c) => ({ ...c, no: e.target.value }))} options={missing.map((n) => ({ value: String(n), label: `Assessment ${n}` }))} />
            <Input label="Date" type="date" value={creating.date} onChange={(e) => setCreating((c) => ({ ...c, date: e.target.value }))} />
            <ErrorText>{error}</ErrorText>
            <div className="flex justify-end gap-2">
              <Button variant="ghost" onClick={() => setCreating(null)}>
                Cancel
              </Button>
              <Button onClick={create} disabled={busy}>
                Save
              </Button>
            </div>
          </div>
        ) : null}
      </Modal>

      <Modal open={Boolean(entering)} onClose={() => setEntering(null)} title={entering ? `Assessment ${entering.assessment.no} marks` : ""} description={entering ? `Out of ${entering.assessment.maxMarks}, pass mark ${entering.assessment.passMarks}.` : ""} size="lg">
        {entering ? (
          <div className="space-y-4">
            <ul className="divide-y divide-slate-200 dark:divide-slate-700">
              {enterable.map((t) => {
                const r = entering.rows[t.id];
                return (
                  <li key={t.id} className="grid items-end gap-3 py-2 sm:grid-cols-[2fr_1fr_auto_auto]">
                    <span className="font-medium">{t.student.name}</span>
                    <Input label="Marks" type="number" value={r.marks} disabled={r.absent} onChange={(e) => setRow(t.id, "marks", e.target.value)} />
                    <Checkbox label="Absent" checked={r.absent} onChange={(e) => setRow(t.id, "absent", e.target.checked)} />
                    <Checkbox label="Unfair means" checked={r.malpractice} onChange={(e) => setRow(t.id, "malpractice", e.target.checked)} />
                  </li>
                );
              })}
            </ul>
            <p className="text-xs text-secondary">Unfair means counts as failed (cl.11 জ). A month already paid cannot change.</p>
            <ErrorText>{error}</ErrorText>
            <div className="flex justify-end gap-2">
              <Button variant="ghost" onClick={() => setEntering(null)}>
                Cancel
              </Button>
              <Button onClick={saveResults} disabled={busy}>
                Save marks
              </Button>
            </div>
          </div>
        ) : null}
      </Modal>
    </Card>
  );
}

// ---------- final report ----------

function FinalReportTab({ d, policy, onChange }) {
  const { run, busy } = useAction();
  const [form, setForm] = useState({ report: d.finalReport || "", recommendation: d.finalRecommendation || "" });
  const [error, setError] = useState("");
  const ended = d.universityGroup.endDate < today();

  const send = async () => {
    setError("");
    try {
      const next = await run(() => api.post(`/softskill/university-groups/${d.universityGroup.id}/final-report`, form), "Final report sent to the Commission", { silentError: true });
      onChange(next);
    } catch (e) {
      setError(e.message);
    }
  };

  return (
    <Card
      title="Final evaluation report (cl.14 ঙ)"
      description="After the batch ends, the university evaluates the trainees' work and sends its recommendations. Certificates follow it (cl.15)."
      aside={d.finalReportAt ? <Badge tone="green">Sent {formatDate(d.finalReportAt)}</Badge> : null}
    >
      {d.access.coordinate ? (
        <div className="space-y-4">
          {!ended ? <p className="rounded-lg bg-[#f7f1e6] p-3 text-xs">The report can be sent after {formatDate(d.universityGroup.endDate)}.</p> : null}
          <Textarea label="Evaluation of the batch" value={form.report} onChange={(e) => setForm((f) => ({ ...f, report: e.target.value }))} rows={8} hint={`At least ${policy.minFinalReportLength} characters.`} />
          <Textarea label="Recommendations" value={form.recommendation} onChange={(e) => setForm((f) => ({ ...f, recommendation: e.target.value }))} rows={4} />
          <ErrorText>{error}</ErrorText>
          <Button onClick={send} disabled={busy || !ended}>
            {d.finalReportAt ? "Send again" : "Send to the Commission"}
          </Button>
        </div>
      ) : d.finalReport ? (
        <dl className="space-y-3">
          <Fact label="Evaluation">
            <span className="whitespace-pre-line">{d.finalReport}</span>
          </Fact>
          <Fact label="Recommendations">
            <span className="whitespace-pre-line">{d.finalRecommendation}</span>
          </Fact>
        </dl>
      ) : (
        <p className="text-secondary">The coordinator has not sent it yet.</p>
      )}
    </Card>
  );
}

// ---------- monitoring (cl.14 ঘ) ----------

function MonitoringTab({ d, me, onReported }) {
  const [reporting, setReporting] = useState(null);
  const visits = d.visits || [];
  return (
    <Card
      title="Monitoring visits (cl.14 ঘ)"
      description="Commission officers and academia/industry experts visit the university group. Their findings and recommendations come back here."
    >
      {visits.length ? (
        <div className="space-y-3">
          {visits.map((v) => (
            <VisitCard key={v.id} v={v}>
              {v.status === "Planned" && v.monitor.id === me?.id ? (
                <Button size="sm" onClick={() => setReporting(v)}>
                  File report
                </Button>
              ) : null}
            </VisitCard>
          ))}
        </div>
      ) : (
        <EmptyState title="No visit yet" description="The Commission plans visits from its Monitoring screen." />
      )}
      {reporting ? (
        <VisitReportModal
          visit={reporting}
          onClose={() => setReporting(null)}
          onSaved={() => {
            setReporting(null);
            onReported();
          }}
        />
      ) : null}
    </Card>
  );
}
