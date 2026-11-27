"use client";

import { useState } from "react";
import { Badge, Button, DataTable, ErrorState, Input, Modal, StatusBadge, Textarea } from "@/components/ui";
import { useSetup } from "@/components/layout/InternshipShell";
import { useAction, useApiQuery, useSession } from "@/hooks/useApi";
import { api } from "@/lib/api";
import { P } from "@/lib/internshipPermissions";
import { formatDate } from "@/lib/formatters";

const NEXT = {
  Draft: { to: "Applications open", label: "Open applications" },
  "Applications open": { to: "Selection", label: "Close & start selection" },
  Selection: { to: "Running", label: "Start internship" },
  Running: { to: "Completed", label: "Mark completed" },
};

export function BatchesTab() {
  const { policy } = useSetup();
  const { can } = useSession();
  const { data: batches, error, reload, setData } = useApiQuery("/internship/batches");
  const { run, busy } = useAction();
  const empty = { name: "", capacity: String(policy.batchSize), durationMonths: "4", applyFrom: "", applyTo: "", startDate: "", note: "" };
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState(empty);
  const [formError, setFormError] = useState("");
  const [override, setOverride] = useState(null); // string[] of limits that need an override
  const [reason, setReason] = useState("");
  const manage = can(P.batchManage);

  const set = (k) => (e) => setForm({ ...form, [k]: e.target.value });

  const close = () => {
    setOpen(false);
    setOverride(null);
    setReason("");
    setFormError("");
  };

  const submit = async () => {
    setFormError("");
    const body = {
      name: form.name,
      capacity: form.capacity === "" ? null : Number(form.capacity),
      durationMonths: form.durationMonths === "" ? null : Number(form.durationMonths),
      applyFrom: form.applyFrom || null,
      applyTo: form.applyTo || null,
      startDate: form.startDate || null,
      note: form.note,
      overrideReason: override ? reason : null,
    };
    try {
      await run(() => api.post("/internship/batches", body), "Batch created", { silentError: true });
      close();
      setForm(empty);
      reload();
    } catch (e) {
      if (e.code === "needs_override") {
        if (override) setFormError("Enter a reason to approve the override.");
        setOverride(e.data || [e.message]);
        return;
      }
      setFormError(e.message);
    }
  };

  const move = async (b, to) => {
    const updated = await run(() => api.post(`/internship/batches/${b.id}/status`, { status: to }), `${b.name}: ${to}`);
    if (updated) setData(batches.map((x) => (x.id === updated.id ? updated : x)));
  };

  if (error) return <ErrorState title="Could not load batches" description={error.message} action={<Button onClick={reload}>Try again</Button>} />;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm text-secondary">
          Limits: {policy.batchSize} seats per batch and {policy.maxBatchesPerYear} batches per year. Going over needs a recorded override by the Commission (cl.6 গ).
        </p>
        {manage ? <Button onClick={() => setOpen(true)}>Create batch</Button> : null}
      </div>

      <DataTable
        emptyMessage={batches ? "No batches yet." : "Loading…"}
        rows={batches || []}
        columns={[
          {
            key: "name",
            label: "Batch",
            render: (b) => (
              <div>
                <p className="font-medium">{b.name}</p>
                <p className="text-xs text-secondary">{b.code}</p>
              </div>
            ),
          },
          { key: "status", label: "Status", render: (b) => <StatusBadge status={b.status} /> },
          { key: "window", label: "Apply window", render: (b) => `${formatDate(b.applyFrom)} – ${formatDate(b.applyTo)}` },
          { key: "run", label: "Internship", render: (b) => `${formatDate(b.startDate)} · ${b.durationMonths} mo` },
          { key: "seats", label: "Seats", render: (b) => `${b.seats.taken}/${b.seats.capacity}` },
          { key: "override", label: "", render: (b) => (b.override ? <Badge tone="amber">Override: {b.override.reason}</Badge> : null) },
          {
            key: "actions",
            label: "",
            render: (b) =>
              manage ? (
                <div className="flex gap-2">
                  {NEXT[b.status] ? (
                    <Button size="sm" disabled={busy} onClick={() => move(b, NEXT[b.status].to)}>
                      {NEXT[b.status].label}
                    </Button>
                  ) : null}
                  {["Draft", "Applications open"].includes(b.status) ? (
                    <Button size="sm" variant="ghost" disabled={busy} onClick={() => move(b, "Cancelled")}>
                      Cancel
                    </Button>
                  ) : null}
                </div>
              ) : null,
          },
        ]}
      />

      <Modal open={open} onClose={close} title="Create batch" description="Policy limits are checked by the server before the batch is saved." size="lg">
        <div className="space-y-4">
          <Input label="Batch name" value={form.name} onChange={set("name")} placeholder="e.g. 2027 Batch 2" required />
          <div className="grid gap-4 sm:grid-cols-2">
            <Input label="Seats" type="number" value={form.capacity} onChange={set("capacity")} hint={`Policy: ${policy.batchSize}`} />
            <Input label="Duration (months)" type="number" value={form.durationMonths} onChange={set("durationMonths")} hint={`${policy.minMonths}–${policy.maxMonths}`} />
            <Input label="Applications open" type="date" value={form.applyFrom} onChange={set("applyFrom")} />
            <Input label="Applications close" type="date" value={form.applyTo} onChange={set("applyTo")} />
            <Input label="Internship starts" type="date" value={form.startDate} onChange={set("startDate")} />
          </div>
          <Textarea label="Note (optional)" value={form.note} onChange={set("note")} rows={2} />

          {override ? (
            <div className="space-y-3 rounded-xl border border-opportunity/40 bg-[#f7f1e6] p-4 text-sm dark:bg-nexus-800/60">
              <p className="font-semibold">Override needed</p>
              <ul className="list-disc pl-5">
                {override.map((r) => (
                  <li key={r}>{r}</li>
                ))}
              </ul>
              {can(P.batchOverride) ? (
                <Textarea label="Reason for the override (Commission decision)" value={reason} onChange={(e) => setReason(e.target.value)} rows={2} required />
              ) : (
                <p className="text-danger">Your role cannot approve overrides. Change the values to stay within the limits.</p>
              )}
            </div>
          ) : null}

          {formError ? (
            <p role="alert" className="text-sm text-danger">
              {formError}
            </p>
          ) : null}
          <div className="flex justify-end gap-2">
            <Button variant="ghost" onClick={close}>
              Cancel
            </Button>
            <Button onClick={submit} disabled={busy}>
              {override ? "Create with override" : "Create batch"}
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
