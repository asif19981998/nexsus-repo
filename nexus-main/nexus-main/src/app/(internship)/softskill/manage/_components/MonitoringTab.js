"use client";

import { useState } from "react";
import Link from "next/link";
import { Badge, Button, DataTable, ErrorState, Input, Modal, Select, Textarea } from "@/components/ui";
import { useAction, useApiQuery } from "@/hooks/useApi";
import { api, qs } from "@/lib/api";
import { formatDate } from "@/lib/formatters";
import { Card, ErrorText, Pct } from "../../_components/ui";
import { VisitCard } from "../../_components/oversight";

/**
 * cl.14 (ঘ): every university group's health with what needs attention, and the monitoring visits — planned by the Commission,
 * carried out by its officers or by academia/industry experts.
 */
export function MonitoringTab({ setup }) {
  const batches = useApiQuery("/softskill/batches");
  const [batchId, setBatchId] = useState("all");
  const health = useApiQuery(`/softskill/monitoring/health${qs({ batchId })}`);
  const visits = useApiQuery(`/softskill/monitoring/visits${qs({ batchId })}`);
  const { run, busy } = useAction();
  const [planning, setPlanning] = useState(null); // universityGroup summary

  const cancel = async (v) => {
    if (await run(() => api.post(`/softskill/monitoring/visits/${v.id}/cancel`), "Visit cancelled")) visits.reload();
  };

  if (health.error) return <ErrorState title="Could not load monitoring" description={health.error.message} action={<Button onClick={health.reload}>Try again</Button>} />;

  return (
    <div className="space-y-5">
      <div className="grid gap-3 sm:grid-cols-3">
        <Select
          label="Batch"
          value={batchId}
          onChange={(e) => setBatchId(e.target.value)}
          options={[{ value: "all", label: "All batches" }, ...(batches.data || []).map((b) => ({ value: String(b.id), label: `${b.name} (${b.code})` }))]}
        />
      </div>

      <Card title="University group health" description="University groups in selection, running or completed batches; those needing attention first.">
        <DataTable
          emptyMessage={health.data ? "No university group to monitor." : "Loading…"}
          rows={health.data || []}
          columns={[
            {
              key: "c",
              label: "University group",
              render: (h) => (
                <Link href={`/softskill/university-groups/${h.universityGroup.id}`} className="hover:underline">
                  <span className="font-medium">{h.universityGroup.university.name}</span>
                  <span className="block text-xs text-secondary">
                    {h.universityGroup.batchCode} · {h.universityGroup.batchStatus}
                  </span>
                </Link>
              ),
            },
            { key: "t", label: "Trainees", render: (h) => h.trainees },
            {
              key: "att",
              label: "Attendance",
              render: (h) => (
                <span>
                  <Pct value={h.averageAttendancePct} min={setup.policy.minAttendancePct} />
                  {h.belowMinAttendance ? <span className="block text-xs text-danger">{h.belowMinAttendance} below</span> : null}
                </span>
              ),
            },
            {
              key: "hrs",
              label: "Hours held",
              render: (h) => (
                <span>
                  {h.hoursHeld}/{h.totalHours}
                  {h.expectedHoursByNow != null ? <span className="block text-xs text-secondary">~{h.expectedHoursByNow} expected</span> : null}
                </span>
              ),
            },
            { key: "pass", label: "Pass rate", render: (h) => (h.passRatePct != null ? `${h.passRatePct}%` : "—") },
            {
              key: "visits",
              label: "Visits",
              render: (h) => (
                <span>
                  {h.visits}
                  {h.averageRating != null ? <Badge className="ml-1" tone={h.averageRating >= 3 ? "green" : "red"}>{h.averageRating}/5</Badge> : null}
                  {h.lastVisit ? <span className="block text-xs text-secondary">last {formatDate(h.lastVisit)}</span> : null}
                </span>
              ),
            },
            {
              key: "flags",
              label: "Needs attention",
              render: (h) =>
                h.flags.length ? (
                  <ul className="list-disc space-y-0.5 pl-4 text-xs text-danger">
                    {h.flags.map((f) => (
                      <li key={f}>{f}</li>
                    ))}
                  </ul>
                ) : (
                  <Badge tone="green">On track</Badge>
                ),
            },
            {
              key: "act",
              label: "",
              render: (h) =>
                ["Selection", "Running"].includes(h.universityGroup.batchStatus) ? (
                  <Button size="sm" variant="outline" onClick={() => setPlanning(h.universityGroup)}>
                    Plan visit
                  </Button>
                ) : null,
            },
          ]}
        />
      </Card>

      <Card title="Visits">
        {visits.data?.length ? (
          <div className="grid gap-3 lg:grid-cols-2">
            {visits.data.map((v) => (
              <VisitCard key={v.id} v={v}>
                {v.status === "Planned" ? (
                  <Button size="sm" variant="ghost" disabled={busy} onClick={() => cancel(v)}>
                    Cancel visit
                  </Button>
                ) : null}
              </VisitCard>
            ))}
          </div>
        ) : (
          <p className="text-secondary">{visits.data ? "No visits yet. Plan one from a university group above." : "Loading…"}</p>
        )}
      </Card>

      {planning ? (
        <PlanVisitModal
          universityGroup={planning}
          onClose={() => setPlanning(null)}
          onSaved={() => {
            setPlanning(null);
            visits.reload();
          }}
        />
      ) : null}
    </div>
  );
}

function PlanVisitModal({ universityGroup, onClose, onSaved }) {
  const monitors = useApiQuery("/softskill/monitoring/monitors");
  const detail = useApiQuery(`/softskill/university-groups/${universityGroup.id}`);
  const { run, busy } = useAction();
  const [form, setForm] = useState({ monitorId: "", sessionId: "", plannedOn: "", purpose: "" });
  const [error, setError] = useState("");
  const set = (k) => (e) => setForm((f) => ({ ...f, [k]: e.target.value }));
  const sessions = (detail.data?.sessions || []).filter((s) => s.status !== "Cancelled");

  const save = async () => {
    setError("");
    try {
      await run(
        () =>
          api.post("/softskill/monitoring/visits", {
            universityGroupId: universityGroup.id,
            monitorId: form.monitorId ? Number(form.monitorId) : null,
            sessionId: form.sessionId ? Number(form.sessionId) : null,
            plannedOn: form.plannedOn || null,
            purpose: form.purpose,
          }),
        "Visit planned",
        { silentError: true },
      );
      onSaved();
    } catch (e) {
      setError(e.message);
    }
  };

  return (
    <Modal open onClose={onClose} title={`Plan a visit — ${universityGroup.university.name}`} description={`${universityGroup.batchName}, ${formatDate(universityGroup.startDate)} – ${formatDate(universityGroup.endDate)}`}>
      <div className="space-y-4">
        <Select
          label="Monitor"
          value={form.monitorId}
          placeholder={monitors.data ? "Choose an expert or officer" : "Loading…"}
          onChange={set("monitorId")}
          options={(monitors.data || []).map((m) => ({ value: String(m.id), label: `${m.name}${m.designation ? `, ${m.designation}` : ""} — ${m.role === "Monitor" ? "expert" : "Commission"}` }))}
          hint="Experts are user accounts with the Monitor role, created in Users."
        />
        <Select
          label="Session to observe (optional)"
          value={form.sessionId}
          onChange={(e) => {
            const s = sessions.find((x) => String(x.id) === e.target.value);
            setForm((f) => ({ ...f, sessionId: e.target.value, plannedOn: s ? s.date : f.plannedOn }));
          }}
          options={[{ value: "", label: "— none —" }, ...sessions.map((s) => ({ value: String(s.id), label: `${formatDate(s.date)} · ${s.topic} · ${s.startTime}` }))]}
        />
        <Input label="Date" type="date" value={form.plannedOn} onChange={set("plannedOn")} disabled={Boolean(form.sessionId)} />
        <Textarea label="Purpose (optional)" value={form.purpose} onChange={set("purpose")} rows={2} />
        <ErrorText>{error}</ErrorText>
        <div className="flex justify-end gap-2">
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={save} disabled={busy}>
            Plan visit
          </Button>
        </div>
      </div>
    </Modal>
  );
}
