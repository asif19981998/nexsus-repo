"use client";

import { useEffect, useRef, useState } from "react";
import { useSearchParams } from "next/navigation";
import { MoreHorizontal } from "lucide-react";
import { Badge, Button, DataTable, DropdownMenu, ErrorState, IconButton, Input, Modal, Select, Textarea } from "@/components/ui";
import { useSetup } from "@/components/layout/InternshipShell";
import { useAction, useApiQuery, useSession } from "@/hooks/useApi";
import { api, qs } from "@/lib/api";
import { P } from "@/lib/internshipPermissions";
import { formatDate } from "@/lib/formatters";
import { cn } from "@/lib/cn";
import { AttachedDocuments } from "@/components/documents/AttachedDocuments";

const TESTABLE = ["Shortlisted", "Waitlisted"];
const REJECTABLE = ["Submitted", "Shortlisted", "Waitlisted", "Selected"];

/**
 * Each status as the admin reads it: a plain name, a colour, and what happens next. The API keeps its own status
 * names ("Submitted"…); only the wording here changes.
 */
export const STATUS = {
  Submitted: { label: "New", tone: "blue", next: "Waiting for screening" },
  Shortlisted: { label: "Shortlisted", tone: "teal", next: "Selection test next" },
  Waitlisted: { label: "On waitlist", tone: "amber", next: "Selected if a seat frees up" },
  Selected: { label: "Selected", tone: "violet", next: "Waiting for placement" },
  Active: { label: "Interning", tone: "green", next: "Placed in a department" },
  Completed: { label: "Finished", tone: "teal", next: "Certificate decision pending" },
  Certified: { label: "Certified", tone: "green", next: "Certificate issued" },
  "Not certified": { label: "Not certified", tone: "red", next: "Finished without a certificate" },
  Rejected: { label: "Rejected", tone: "red", next: null },
  Withdrawn: { label: "Withdrawn", tone: "slate", next: "The student withdrew" },
  Terminated: { label: "Ended early", tone: "red", next: "Internship terminated" },
};

/** The stage tabs above the list, in the order an application moves through them. */
const STAGES = [
  { key: "new", label: "New", statuses: ["Submitted"], hint: "Check each applicant's documents and eligibility, then shortlist or reject." },
  { key: "shortlisted", label: "Shortlisted", statuses: ["Shortlisted"], hint: "Record the selection test (cl.9), then select or move to the waitlist." },
  { key: "waitlist", label: "Waitlist", statuses: ["Waitlisted"], hint: "Select someone from here when a seat frees up." },
  {
    key: "selected",
    label: "To place",
    statuses: ["Selected"],
    hint: "Place each selected student in a department with a mentor. Placing starts the internship.",
  },
  { key: "active", label: "Interning", statuses: ["Active"], hint: "Running internships. Attendance and reports are under Interns." },
  { key: "finished", label: "Finished", statuses: ["Completed", "Certified", "Not certified"], hint: "Internships that have ended." },
  { key: "closed", label: "Closed", statuses: ["Rejected", "Withdrawn", "Terminated"], hint: "Rejected, withdrawn or ended early." },
];

function StatusCell({ app }) {
  const st = STATUS[app.status] || { label: app.status, tone: "slate", next: null };
  let next = st.next;
  if (TESTABLE.includes(app.status)) next = app.exam ? `Test score ${app.exam.score}/100` : "Test not recorded yet";
  else if (app.status === "Selected" && app.exam) next = `${next} · test ${app.exam.score}/100`;
  return (
    <div className="space-y-1">
      <Badge tone={st.tone}>{st.label}</Badge>
      {next ? <p className="text-xs text-secondary">{next}</p> : null}
    </div>
  );
}

export function ApplicationsTab() {
  const setup = useSetup();
  const { policy, departments, examModes } = setup;
  const { can } = useSession();
  const [batchId, setBatchId] = useState("all");
  const [picked, setPicked] = useState(null); // stage key; null = the first stage with something in it
  const batches = useApiQuery(can(P.batchView) ? "/internship/batches" : null);
  // Every status at once: the stage tabs count and filter on this page.
  const list = useApiQuery(`/internship/applications${qs({ batchId })}`);
  const mentors = useApiQuery(can(P.internPlace) ? "/users/mentors" : null);
  const { run, busy } = useAction();
  const [modal, setModal] = useState(null); // { type, app }
  const [detail, setDetail] = useState(null);
  const [form, setForm] = useState({});
  const [error, setError] = useState("");

  const selectedBatch = batchId !== "all" ? batches.data?.find((b) => String(b.id) === String(batchId)) : null;

  const openModal = async (type, app) => {
    setError("");
    setModal({ type, app });
    if (type === "view") {
      setDetail(null);
      try {
        setDetail(await api.get(`/internship/applications/${app.id}`));
      } catch (e) {
        setError(e.message);
      }
    }
    if (type === "exam") setForm({ mode: app.exam?.mode || examModes[0], date: app.exam?.date || setup.today, score: app.exam?.score ?? "" });
    if (type === "reject") setForm({ reason: "" });
    if (type === "place") {
      const b = batches.data?.find((x) => x.id === app.batchId);
      const dept = departments.find((d) => d.name === app.preferredDepartments?.[0]) || departments[0];
      setForm({
        departmentId: String(dept?.id || ""),
        mentorId: "",
        startDate: b?.startDate || setup.today,
        months: String(b?.durationMonths || 4),
        tasks: "",
      });
    }
  };

  // A notification link (?app=12) opens that application once the list is loaded, on the stage it sits in.
  const linkedApp = useRef(useSearchParams().get("app"));
  useEffect(() => {
    if (!linkedApp.current || !list.data) return;
    const target = list.data.find((x) => String(x.id) === linkedApp.current);
    linkedApp.current = null;
    if (!target) return;
    setPicked(STAGES.find((st) => st.statuses.includes(target.status))?.key || "all");
    openModal("view", target);
    // openModal is recreated each render; this runs once, when the list first arrives.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [list.data]);

  const closeModal = () => setModal(null);
  const set = (k) => (e) => setForm({ ...form, [k]: e.target.value });

  const quick = async (app, action, msg) => {
    if (await run(() => api.post(`/internship/applications/${app.id}/${action}`), msg)) {
      list.reload();
      batches.reload();
    }
  };

  const submitModal = async (path, body, msg) => {
    setError("");
    try {
      await run(() => api.post(path, body), msg, { silentError: true });
      closeModal();
      list.reload();
      batches.reload();
    } catch (e) {
      setError(e.message);
    }
  };

  const deptMentors = (mentors.data || []).filter((m) => !form.departmentId || String(m.departmentId) === String(form.departmentId));
  const mentorOptions = (deptMentors.length ? deptMentors : mentors.data || []).map((m) => ({
    value: String(m.id),
    label: `${m.name}${m.department ? ` · ${m.department}` : ""}`,
  }));

  if (list.error)
    return <ErrorState title="Could not load applications" description={list.error.message} action={<Button onClick={list.reload}>Try again</Button>} />;

  /** The next step for an application as one button, and the other allowed steps for the "more" menu. */
  const actionsFor = (x) => {
    const steps = [];
    if (x.status === "Submitted" && can(P.applicationScreen)) steps.push({ label: "Shortlist", onClick: () => quick(x, "shortlist", "Shortlisted") });
    if (TESTABLE.includes(x.status) && can(P.applicationExam))
      steps.push({ label: x.exam ? "Edit test result" : "Record test", onClick: () => openModal("exam", x) });
    if (TESTABLE.includes(x.status) && can(P.applicationSelect)) steps.push({ label: "Select", onClick: () => quick(x, "select", "Selected") });
    if (x.status === "Shortlisted" && can(P.applicationScreen)) steps.push({ label: "Move to waitlist", onClick: () => quick(x, "waitlist", "Waitlisted") });
    if (x.status === "Selected" && can(P.internPlace)) steps.push({ label: "Place", onClick: () => openModal("place", x) });
    // Without a test score the next step is to record it; with one, to select.
    const primary = TESTABLE.includes(x.status) && x.exam ? steps.find((st) => st.label === "Select") || steps[0] : steps[0];
    const more = steps.filter((st) => st !== primary).map((st) => ({ ...st, disabled: busy }));
    if (REJECTABLE.includes(x.status) && can(P.applicationScreen)) {
      if (more.length) more.push({ divider: true });
      more.push({ label: "Reject…", danger: true, onClick: () => openModal("reject", x) });
    }
    return { primary, more };
  };

  const all = list.data || [];
  const countOf = (st) => all.filter((x) => st.statuses.includes(x.status)).length;
  const stage = picked ?? (list.data ? STAGES.find((st) => countOf(st) > 0)?.key || "all" : "new");
  const current = STAGES.find((x) => x.key === stage);
  const rows = current ? all.filter((x) => current.statuses.includes(x.status)) : all;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end gap-3">
        {batches.data ? (
          <Select
            label="Batch"
            value={batchId}
            onChange={(e) => setBatchId(e.target.value)}
            options={[{ value: "all", label: "All batches" }, ...batches.data.map((b) => ({ value: String(b.id), label: b.name }))]}
          />
        ) : null}
        {selectedBatch ? (
          <Badge tone={selectedBatch.seats.left ? "green" : "red"} className="mb-1 px-3 py-1 text-xs">
            Seats: {selectedBatch.seats.taken} of {selectedBatch.seats.capacity} filled · {selectedBatch.seats.left} left
          </Badge>
        ) : null}
      </div>

      <div className="space-y-2">
        <div role="tablist" aria-label="Application stage" className="flex flex-wrap gap-1.5">
          {[...STAGES, { key: "all", label: "All" }].map((st) => {
            const n = st.statuses ? countOf(st) : all.length;
            const on = stage === st.key;
            return (
              <button
                key={st.key}
                type="button"
                role="tab"
                aria-selected={on}
                onClick={() => setPicked(st.key)}
                className={cn(
                  "inline-flex items-center gap-2 rounded-full border px-3 py-1.5 text-sm transition-colors",
                  on
                    ? "border-ocean bg-ocean font-semibold text-white dark:border-sky dark:bg-sky dark:text-nexus-950"
                    : "border-[#d5e3df] text-nexus-800 hover:border-ocean dark:border-nexus-700 dark:text-nexus-200",
                  !on && !n && "opacity-60",
                )}
              >
                {st.label}
                <span className={cn("text-xs tabular-nums", on ? "opacity-90" : "text-secondary")}>{list.data ? n : "…"}</span>
              </button>
            );
          })}
        </div>
        {current?.hint ? <p className="text-xs text-secondary">{current.hint}</p> : null}
      </div>

      <DataTable
        className="lg:overflow-visible"
        emptyMessage={list.data ? (current ? `Nothing in “${current.label}” right now.` : "No applications yet.") : "Loading…"}
        rows={rows}
        columns={[
          {
            key: "who",
            label: "Applicant",
            render: (x) => (
              <div>
                <p className="font-medium">{x.student.name}</p>
                <p className="text-xs text-secondary">
                  {x.student.university || "—"} · {x.batchName}
                </p>
              </div>
            ),
          },
          { key: "status", label: "Status", render: (x) => <StatusCell app={x} /> },
          { key: "prefs", label: "Preferred", render: (x) => x.preferredDepartments.join(", ") },
          { key: "at", label: "Applied", render: (x) => formatDate(x.appliedAt) },
          {
            key: "actions",
            label: "",
            render: (x) => {
              const { primary, more } = actionsFor(x);
              return (
                <div className="flex items-center justify-end gap-1.5">
                  <Button size="sm" variant="ghost" onClick={() => openModal("view", x)}>
                    View
                  </Button>
                  {primary ? (
                    <Button size="sm" disabled={busy} onClick={primary.onClick}>
                      {primary.label}
                    </Button>
                  ) : null}
                  {more.length ? (
                    <DropdownMenu
                      trigger={
                        <IconButton label={`More for ${x.student.name}`}>
                          <MoreHorizontal className="h-4 w-4" />
                        </IconButton>
                      }
                      items={more}
                    />
                  ) : null}
                </div>
              );
            },
          },
        ]}
      />

      <Modal
        open={modal?.type === "view"}
        onClose={closeModal}
        title={modal?.app.student.name || ""}
        description={modal ? `${modal.app.batchName} · eligibility basis ${modal.app.basis || "—"}` : ""}
        size="lg"
      >
        {detail ? (
          <div className="space-y-4 text-sm">
            <div>
              <p className="font-semibold">Statement of purpose</p>
              <p className="mt-1 text-secondary">{detail.statement}</p>
            </div>
            <p>
              <span className="font-semibold">Email:</span> {detail.student.email}
            </p>
            <div className="space-y-1">
              <p className="font-semibold">Documents</p>
              <AttachedDocuments attached={detail.attachedDocuments} names={detail.documents} />
            </div>
            {detail.rejectionReason ? (
              <p className="text-danger">
                <span className="font-semibold">Rejected:</span> {detail.rejectionReason}
              </p>
            ) : null}
            <div>
              <p className="font-semibold">History</p>
              <ol className="mt-1 space-y-1">
                {detail.history.map((h, i) => (
                  <li key={i} className="flex flex-wrap gap-2">
                    <Badge tone={STATUS[h.status]?.tone || "slate"}>{STATUS[h.status]?.label || h.status}</Badge>
                    <span className="text-secondary">
                      {formatDate(h.at)}
                      {h.byName ? ` · ${h.byName}` : ""}
                      {h.note ? ` — ${h.note}` : ""}
                    </span>
                  </li>
                ))}
              </ol>
            </div>
          </div>
        ) : (
          <p className="text-sm text-secondary">{error || "Loading…"}</p>
        )}
      </Modal>

      <Modal
        open={modal?.type === "exam"}
        onClose={closeModal}
        title="Record selection test"
        description={modal ? `${modal.app.student.name} (cl.9: written, oral or online)` : ""}
      >
        <div className="space-y-4">
          <Select label="Mode" value={form.mode} onChange={set("mode")} options={examModes} />
          <Input label="Date" type="date" value={form.date || ""} onChange={set("date")} />
          <Input label="Score (0–100)" type="number" value={form.score} onChange={set("score")} />
          {error ? (
            <p role="alert" className="text-sm text-danger">
              {error}
            </p>
          ) : null}
          <Button
            disabled={busy}
            onClick={() =>
              submitModal(
                `/internship/applications/${modal.app.id}/exam`,
                { mode: form.mode, date: form.date || null, score: form.score === "" ? null : Number(form.score) },
                "Test result saved",
              )
            }
          >
            Save result
          </Button>
        </div>
      </Modal>

      <Modal open={modal?.type === "reject"} onClose={closeModal} title="Reject application" size="sm">
        <div className="space-y-4">
          <Textarea label="Reason (shown in the record)" value={form.reason || ""} onChange={set("reason")} rows={3} />
          {error ? (
            <p role="alert" className="text-sm text-danger">
              {error}
            </p>
          ) : null}
          <Button
            variant="danger"
            disabled={busy}
            onClick={() => submitModal(`/internship/applications/${modal.app.id}/reject`, { reason: form.reason }, "Application rejected")}
          >
            Reject
          </Button>
        </div>
      </Modal>

      <Modal
        open={modal?.type === "place"}
        onClose={closeModal}
        title="Place in a UGC department"
        description={modal ? `${modal.app.student.name} · asked for ${modal.app.preferredDepartments.join(", ")}` : ""}
        size="lg"
      >
        <div className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <Select
              label="Department"
              value={form.departmentId}
              onChange={(e) => setForm({ ...form, departmentId: e.target.value, mentorId: "" })}
              options={departments.map((d) => ({ value: String(d.id), label: d.name }))}
            />
            <Select label="Mentor (supervisor)" value={form.mentorId} onChange={set("mentorId")} options={mentorOptions} placeholder="Choose mentor" />
            <Input label="Start date" type="date" value={form.startDate || ""} onChange={set("startDate")} />
            <Input
              label="Duration (months)"
              type="number"
              value={form.months}
              onChange={set("months")}
              hint={`${policy.minMonths}–${policy.maxMonths}, not renewable (cl.8)`}
            />
          </div>
          {mentors.data && !deptMentors.length ? (
            <p className="text-xs text-secondary">No mentor is registered in this department, so all mentors are listed. Create one under Users.</p>
          ) : null}
          <Textarea label="Work plan (cl.13)" value={form.tasks || ""} onChange={set("tasks")} rows={3} hint="Match the intern's education and background." />
          {error ? (
            <p role="alert" className="text-sm text-danger">
              {error}
            </p>
          ) : null}
          <Button
            disabled={busy}
            onClick={() =>
              submitModal(
                `/internship/applications/${modal.app.id}/place`,
                {
                  departmentId: form.departmentId ? Number(form.departmentId) : null,
                  mentorId: form.mentorId ? Number(form.mentorId) : null,
                  startDate: form.startDate || null,
                  months: form.months === "" ? null : Number(form.months),
                  tasks: form.tasks,
                },
                "Intern placed — internship is active",
              )
            }
          >
            Place and activate
          </Button>
        </div>
      </Modal>
    </div>
  );
}
