"use client";

import { useState } from "react";
import Link from "next/link";
import { Badge, Button, DataTable, ErrorState, Modal, Select, StatusBadge, Textarea } from "@/components/ui";
import { useAction, useApiQuery, useSession } from "@/hooks/useApi";
import { api, qs } from "@/lib/api";
import { SP } from "@/lib/softskillPermissions";
import { ErrorText, Pct } from "../../_components/ui";

/** Trainees across university groups: cancellation (cl.11 ঞ) and the certificate on the average of three assessments (cl.15). */
export function TraineesTab({ setup }) {
  const { can } = useSession();
  const batches = useApiQuery("/softskill/batches");
  const [batchId, setBatchId] = useState("all");
  const list = useApiQuery(`/softskill/trainees${qs({ batchId })}`);
  const { run, busy } = useAction();
  const [cancelling, setCancelling] = useState(null);
  const [reason, setReason] = useState("");
  const [error, setError] = useState("");
  const [detail, setDetail] = useState(null);

  const issue = async (t) => {
    const r = await run(() => api.post(`/softskill/trainees/${t.id}/outcome`), "Outcome issued");
    if (r) list.reload();
  };
  const cancel = async () => {
    setError("");
    try {
      await run(() => api.post(`/softskill/trainees/${cancelling.id}/cancel`, { reason }), "Participation cancelled", { silentError: true });
      setCancelling(null);
      setReason("");
      list.reload();
    } catch (e) {
      setError(e.message);
    }
  };
  const openDetail = async (t) => setDetail(await run(() => api.get(`/softskill/trainees/${t.id}`)));

  if (list.error) return <ErrorState title="Could not load trainees" description={list.error.message} action={<Button onClick={list.reload}>Try again</Button>} />;

  return (
    <div className="space-y-4">
      <div className="grid gap-3 sm:grid-cols-3">
        <Select
          label="Batch"
          value={batchId}
          onChange={(e) => setBatchId(e.target.value)}
          options={[{ value: "all", label: "All batches" }, ...(batches.data || []).map((b) => ({ value: String(b.id), label: `${b.name} (${b.code})` }))]}
        />
      </div>
      <p className="text-xs text-secondary">
        A certificate needs the three assessments, {setup.policy.minAttendancePct}% attendance, an accepted learning report and the university&apos;s final report.
        Categories: {setup.gradeBands.map((g) => `${g.grade} ${g.min}%+`).join(" · ")}.
      </p>
      <DataTable
        emptyMessage={list.data ? "No trainees." : "Loading…"}
        rows={list.data || []}
        columns={[
          {
            key: "name",
            label: "Trainee",
            render: (t) => (
              <button type="button" className="text-left font-medium text-nexus-700 hover:underline dark:text-nexus-300" onClick={() => openDetail(t)}>
                {t.student.name}
              </button>
            ),
          },
          {
            key: "uni",
            label: "University group",
            render: (t) => (
              <Link href={`/softskill/university-groups/${t.universityGroupId}`} className="hover:underline">
                {t.university} <span className="text-xs text-secondary">{t.batchCode}</span>
              </Link>
            ),
          },
          { key: "status", label: "Status", render: (t) => <StatusBadge status={t.status} /> },
          { key: "att", label: "Attendance", render: (t) => <Pct value={t.attendancePct} min={setup.policy.minAttendancePct} /> },
          { key: "ass", label: "Assessments", render: (t) => `${t.assessmentsDone}/3` },
          { key: "avg", label: "Average", render: (t) => (t.average != null ? `${t.average}%` : "—") },
          {
            key: "out",
            label: "Outcome",
            render: (t) =>
              t.certificateNumber ? (
                <Badge tone="green">
                  {t.grade} · {t.certificateNumber}
                </Badge>
              ) : t.ready ? (
                <Badge tone={t.certifiable ? "blue" : "red"}>{t.certifiable ? `Ready: ${t.grade}` : "Not certifiable"}</Badge>
              ) : (
                <span className="text-xs text-secondary">Pending</span>
              ),
          },
          {
            key: "act",
            label: "",
            render: (t) => (
              <div className="flex gap-1.5">
                {can(SP.certificateIssue) && t.status === "Completed" && t.ready ? (
                  <Button size="sm" disabled={busy} onClick={() => issue(t)}>
                    Issue outcome
                  </Button>
                ) : null}
                {can(SP.traineeCancel) && ["Selected", "Active"].includes(t.status) ? (
                  <Button size="sm" variant="ghost" onClick={() => (setError(""), setCancelling(t))}>
                    Cancel
                  </Button>
                ) : null}
              </div>
            ),
          },
        ]}
      />

      <Modal open={Boolean(cancelling)} onClose={() => setCancelling(null)} title={`Cancel ${cancelling?.student.name || ""}'s participation`} description="cl.11 (ঞ): for conduct against discipline. Unpaid stipends stop.">
        <div className="space-y-4">
          <Textarea label="Reason" value={reason} onChange={(e) => setReason(e.target.value)} rows={3} required />
          <ErrorText>{error}</ErrorText>
          <div className="flex justify-end gap-2">
            <Button variant="ghost" onClick={() => setCancelling(null)}>
              Back
            </Button>
            <Button variant="danger" onClick={cancel} disabled={busy || !reason.trim()}>
              Cancel participation
            </Button>
          </div>
        </div>
      </Modal>

      <Modal open={Boolean(detail)} onClose={() => setDetail(null)} title={detail?.student.name || ""} description={detail ? `${detail.university.name} · ${detail.batchCode}` : ""} size="lg">
        {detail ? (
          <div className="space-y-3 text-sm">
            <p>
              Attendance <strong>{detail.attendance.pct}%</strong> ({detail.attendance.attendedHours}/{detail.attendance.heldHours} h) · Report{" "}
              <strong>{detail.report?.status || "not submitted"}</strong>
            </p>
            <p>Assessments: {detail.assessments.map((a) => `M${a.no} ${a.passed == null ? "—" : a.absent ? "absent" : `${a.marks}/${a.maxMarks}`}`).join(" · ")}</p>
            <p>Stipends: {detail.stipends.map((s) => `M${s.no} ${s.status}`).join(" · ")}</p>
            {detail.computed.pending.length ? (
              <div>
                <p className="font-medium">Still pending</p>
                <ul className="list-disc pl-5 text-secondary">
                  {detail.computed.pending.map((p) => (
                    <li key={p}>{p}</li>
                  ))}
                </ul>
              </div>
            ) : null}
            {detail.computed.reasons.length ? (
              <div>
                <p className="font-medium text-danger">Would not be certified</p>
                <ul className="list-disc pl-5 text-danger">
                  {detail.computed.reasons.map((p) => (
                    <li key={p}>{p}</li>
                  ))}
                </ul>
              </div>
            ) : null}
          </div>
        ) : null}
      </Modal>
    </div>
  );
}
