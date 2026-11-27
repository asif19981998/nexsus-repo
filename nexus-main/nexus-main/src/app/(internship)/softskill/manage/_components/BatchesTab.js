"use client";

import { useState } from "react";
import Link from "next/link";
import { Badge, Button, Checkbox, DataTable, EmptyState, ErrorState, Input, Modal, SearchSelect, Select, StatusBadge, Textarea, buttonClass } from "@/components/ui";
import { useAction, useApiQuery, useSession } from "@/hooks/useApi";
import { api } from "@/lib/api";
import { SP } from "@/lib/softskillPermissions";
import { Card, ErrorText, period } from "../../_components/ui";

const NEXT = {
  Draft: { to: "Applications open", label: "Open applications" },
  "Applications open": { to: "Selection", label: "Close & start selection" },
  Selection: { to: "Running", label: "Start training" },
  Running: { to: "Completed", label: "Mark completed" },
};

/** A 400 "needs_override" means a cl.6 limit; the form then asks for the Commission's reason. */
function OverrideBox({ limits, reason, setReason }) {
  const { can } = useSession();
  if (!limits) return null;
  return (
    <div className="space-y-3 rounded-xl border border-opportunity/40 bg-[#f7f1e6] p-4 text-sm dark:bg-nexus-800/60">
      <p className="font-semibold">Override needed (cl.6 গ)</p>
      <ul className="list-disc pl-5">
        {limits.map((r) => (
          <li key={r}>{r}</li>
        ))}
      </ul>
      {can(SP.batchOverride) ? (
        <Textarea label="Reason (Commission decision)" value={reason} onChange={(e) => setReason(e.target.value)} rows={2} required />
      ) : (
        <p className="text-danger">Your role cannot approve overrides.</p>
      )}
    </div>
  );
}

export function BatchesTab({ setup }) {
  const policy = setup.policy;
  const { can } = useSession();
  const manage = can(SP.batchManage);
  const { data: batches, error, reload, setData } = useApiQuery("/softskill/batches");
  const { run, busy } = useAction();
  const [creating, setCreating] = useState(false);
  const [universityGroupEdit, setUniversityGroupEdit] = useState(null); // { batch, universityGroup?, form }
  const [coordinator, setCoordinator] = useState(null); // { universityGroup, value }
  const [override, setOverride] = useState(null);
  const [reason, setReason] = useState("");
  const [formError, setFormError] = useState("");

  const replace = (updated) => updated && setData(batches.map((b) => (b.id === updated.id ? updated : b)));
  const reset = () => {
    setOverride(null);
    setReason("");
    setFormError("");
  };

  /** Runs a write that may hit a cl.6 limit; returns the result or null. */
  const withOverride = async (fn, ok) => {
    setFormError("");
    try {
      return await run(fn, ok, { silentError: true });
    } catch (e) {
      if (e.code === "needs_override") {
        if (override) setFormError("Enter a reason to approve the override.");
        setOverride(e.data || [e.message]);
      } else setFormError(e.message);
      return null;
    }
  };

  const move = async (b, to) => replace(await run(() => api.post(`/softskill/batches/${b.id}/status`, { status: to }), `${b.name}: ${to}`));

  const saveUniversityGroup = async () => {
    const { batch, universityGroup, form } = universityGroupEdit;
    const body = {
      universityId: form.universityId ? Number(form.universityId) : null,
      capacity: form.capacity === "" ? null : Number(form.capacity),
      venue: form.venue,
      overrideReason: override ? reason : null,
    };
    const updated = await withOverride(
      () => (universityGroup ? api.put(`/softskill/batches/university-groups/${universityGroup.id}`, body) : api.post(`/softskill/batches/${batch.id}/university-groups`, body)),
      universityGroup ? "University group updated" : "University added",
    );
    if (updated) {
      replace(updated);
      setUniversityGroupEdit(null);
      reset();
    }
  };

  const removeUniversityGroup = async (c) => replace(await run(() => api.delete(`/softskill/batches/university-groups/${c.id}`), `${c.university.name} removed`));

  if (error) return <ErrorState title="Could not load batches" description={error.message} action={<Button onClick={reload}>Try again</Button>} />;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm text-secondary">
          Up to {policy.seatsPerUniversity} trainees per university per batch and {policy.maxBatchesPerYear} batches a year (cl.6). Training runs {policy.durationMonths} months, {policy.totalHours} hours (cl.8).
        </p>
        {manage ? (
          <Button
            onClick={() => {
              reset();
              setCreating(true);
            }}
          >
            Create batch
          </Button>
        ) : null}
      </div>

      {!batches ? <p className="text-secondary">Loading…</p> : null}
      {batches && !batches.length ? <EmptyState title="No batches yet" description="Create a batch, add the taking part universities, then open applications." /> : null}

      {(batches || []).map((b) => (
        <Card
          key={b.id}
          title={b.name}
          description={`${b.code} · apply ${period(b.applyFrom, b.applyTo)} · training ${period(b.startDate, b.endDate)} · ${b.taken}/${b.capacity} selected`}
          aside={
            <div className="flex flex-wrap items-center gap-2">
              <StatusBadge status={b.status} />
              {b.overrideReason ? <Badge tone="amber">Override: {b.overrideReason}</Badge> : null}
              {manage && NEXT[b.status] ? (
                <Button size="sm" disabled={busy} onClick={() => move(b, NEXT[b.status].to)}>
                  {NEXT[b.status].label}
                </Button>
              ) : null}
              {manage && ["Draft", "Applications open", "Selection"].includes(b.status) ? (
                <Button size="sm" variant="ghost" disabled={busy} onClick={() => move(b, "Cancelled")}>
                  Cancel batch
                </Button>
              ) : null}
            </div>
          }
        >
          <DataTable
            emptyMessage="No university added yet."
            rows={b.universityGroups}
            columns={[
              { key: "uni", label: "University", render: (c) => <span className="font-medium">{c.university.name}</span> },
              { key: "seats", label: "Seats", render: (c) => `${c.taken}/${c.capacity}${c.overrideReason ? " *" : ""}` },
              {
                key: "coord",
                label: "Coordinator (cl.12 গ)",
                render: (c) =>
                  c.coordinator ? (
                    c.coordinator.name
                  ) : (
                    <Badge tone="amber">Not named</Badge>
                  ),
              },
              { key: "venue", label: "Venue", render: (c) => c.venue || "—" },
              { key: "final", label: "Final report", render: (c) => (c.finalReportSubmitted ? <Badge tone="green">Sent</Badge> : "—") },
              {
                key: "act",
                label: "",
                render: (c) => (
                  <div className="flex flex-wrap gap-1.5">
                    <Link href={`/softskill/university-groups/${c.id}`} className={buttonClass({ size: "sm", variant: "outline" })}>
                      Open
                    </Link>
                    {manage ? (
                      <>
                        <Button size="sm" variant="ghost" onClick={() => setCoordinator({ universityGroup: c, value: c.coordinator ? String(c.coordinator.id) : "" })}>
                          Coordinator
                        </Button>
                        <Button
                          size="sm"
                          variant="ghost"
                          onClick={() => {
                            reset();
                            setUniversityGroupEdit({ batch: b, universityGroup: c, form: { universityId: String(c.university.id), capacity: String(c.capacity), venue: c.venue || "" } });
                          }}
                        >
                          Edit
                        </Button>
                        {["Draft", "Applications open"].includes(b.status) && c.taken === 0 ? (
                          <Button size="sm" variant="ghost" disabled={busy} onClick={() => removeUniversityGroup(c)}>
                            Remove
                          </Button>
                        ) : null}
                      </>
                    ) : null}
                  </div>
                ),
              },
            ]}
          />
          {manage && ["Draft", "Applications open"].includes(b.status) ? (
            <Button
              size="sm"
              variant="outline"
              onClick={() => {
                reset();
                setUniversityGroupEdit({ batch: b, universityGroup: null, form: { universityId: "", capacity: String(policy.seatsPerUniversity), venue: "" } });
              }}
            >
              Add university
            </Button>
          ) : null}
        </Card>
      ))}

      {creating ? (
        <CreateBatchModal
          setup={setup}
          onClose={() => {
            setCreating(false);
            reset();
          }}
          withOverride={withOverride}
          override={override}
          reason={reason}
          setReason={setReason}
          formError={formError}
          busy={busy}
          onCreated={(b) => {
            setData([b, ...(batches || [])]);
            setCreating(false);
            reset();
          }}
        />
      ) : null}

      <Modal
        open={Boolean(universityGroupEdit)}
        onClose={() => {
          setUniversityGroupEdit(null);
          reset();
        }}
        title={universityGroupEdit?.universityGroup ? `${universityGroupEdit.universityGroup.university.name}` : "Add a university"}
        description={`Seats default to ${policy.seatsPerUniversity} (cl.6 ক).`}
      >
        {universityGroupEdit ? (
          <div className="space-y-4">
            {!universityGroupEdit.universityGroup ? (
              <div className="space-y-1">
                <p className="text-sm font-medium">University</p>
                <SearchSelect
                  value={universityGroupEdit.form.universityId}
                  onChange={(v) => setUniversityGroupEdit((x) => ({ ...x, form: { ...x.form, universityId: v } }))}
                  options={setup.universities
                    .filter((u) => u.isPublic && !universityGroupEdit.batch.universityGroups.some((c) => c.university.id === u.id))
                    .map((u) => ({ value: String(u.id), label: u.name, sub: u.shortName }))}
                />
              </div>
            ) : null}
            <div className="grid gap-4 sm:grid-cols-2">
              <Input label="Seats" type="number" value={universityGroupEdit.form.capacity} onChange={(e) => setUniversityGroupEdit((x) => ({ ...x, form: { ...x.form, capacity: e.target.value } }))} />
              <Input label="Venue" value={universityGroupEdit.form.venue} onChange={(e) => setUniversityGroupEdit((x) => ({ ...x, form: { ...x.form, venue: e.target.value } }))} placeholder="Room / building" />
            </div>
            <OverrideBox limits={override} reason={reason} setReason={setReason} />
            <ErrorText>{formError}</ErrorText>
            <div className="flex justify-end gap-2">
              <Button variant="ghost" onClick={() => setUniversityGroupEdit(null)}>
                Cancel
              </Button>
              <Button onClick={saveUniversityGroup} disabled={busy}>
                {override ? "Save with override" : "Save"}
              </Button>
            </div>
          </div>
        ) : null}
      </Modal>

      {coordinator ? <CoordinatorModal state={coordinator} onClose={() => setCoordinator(null)} onSaved={(b) => (replace(b), setCoordinator(null))} /> : null}
    </div>
  );
}

function CreateBatchModal({ setup, onClose, withOverride, override, reason, setReason, formError, busy, onCreated }) {
  const [form, setForm] = useState({ name: "", applyFrom: "", applyTo: "", startDate: "", note: "", universityIds: [] });
  const [filter, setFilter] = useState("");
  const set = (k) => (e) => setForm((f) => ({ ...f, [k]: e.target.value }));
  const publicUnis = setup.universities.filter((u) => u.isPublic && `${u.name} ${u.shortName || ""}`.toLowerCase().includes(filter.toLowerCase()));
  const toggle = (id) => setForm((f) => ({ ...f, universityIds: f.universityIds.includes(id) ? f.universityIds.filter((x) => x !== id) : [...f.universityIds, id] }));

  const submit = async () => {
    const created = await withOverride(
      () =>
        api.post("/softskill/batches", {
          name: form.name,
          applyFrom: form.applyFrom || null,
          applyTo: form.applyTo || null,
          startDate: form.startDate || null,
          note: form.note,
          universityIds: form.universityIds,
          overrideReason: override ? reason : null,
        }),
      "Batch created",
    );
    if (created) onCreated(created);
  };

  return (
    <Modal open onClose={onClose} title="Create batch" description={`Training lasts ${setup.policy.durationMonths} months from the start date (cl.8). Limits are checked by the server.`} size="lg">
      <div className="space-y-4">
        <Input label="Batch name" value={form.name} onChange={set("name")} placeholder="e.g. Soft Skill 2027 — Batch 1" required />
        <div className="grid gap-4 sm:grid-cols-3">
          <Input label="Applications open" type="date" value={form.applyFrom} onChange={set("applyFrom")} />
          <Input label="Applications close" type="date" value={form.applyTo} onChange={set("applyTo")} />
          <Input label="Training starts" type="date" value={form.startDate} onChange={set("startDate")} />
        </div>
        <div className="space-y-2">
          <div className="flex items-end justify-between gap-2">
            <p className="text-sm font-medium">
              Universities taking part <span className="text-xs font-normal text-secondary">({form.universityIds.length} chosen)</span>
            </p>
            <Input placeholder="Filter…" value={filter} onChange={(e) => setFilter(e.target.value)} className="max-w-48" />
          </div>
          <div className="grid max-h-56 gap-1 overflow-y-auto rounded-lg border border-slate-200 p-2 sm:grid-cols-2 dark:border-slate-700">
            {publicUnis.map((u) => (
              <Checkbox key={u.id} label={u.name} checked={form.universityIds.includes(u.id)} onChange={() => toggle(u.id)} />
            ))}
          </div>
          <p className="text-xs text-secondary">Universities can also be added later, until selection starts.</p>
        </div>
        <Textarea label="Note (optional)" value={form.note} onChange={set("note")} rows={2} />
        <OverrideBox limits={override} reason={reason} setReason={setReason} />
        <ErrorText>{formError}</ErrorText>
        <div className="flex justify-end gap-2">
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={submit} disabled={busy}>
            {override ? "Create with override" : "Create batch"}
          </Button>
        </div>
      </div>
    </Modal>
  );
}

/** cl.12 (গ): the coordinator is an approved trainer of the university group's own university. */
function CoordinatorModal({ state, onClose, onSaved }) {
  const { universityGroup } = state;
  const trainers = useApiQuery(`/softskill/trainers?status=Approved&universityId=${universityGroup.university.id}`);
  const { run, busy } = useAction();
  const [value, setValue] = useState(state.value);
  const save = async () => {
    const b = await run(() => api.put(`/softskill/batches/university-groups/${universityGroup.id}/coordinator`, { coordinatorId: value ? Number(value) : null }), "Coordinator saved");
    if (b) onSaved(b);
  };
  return (
    <Modal open onClose={onClose} title={`Coordinator — ${universityGroup.university.name}`} description="Choose an approved trainer of this university.">
      <div className="space-y-4">
        {trainers.data && !trainers.data.length ? (
          <p className="rounded-lg bg-[#f7f1e6] p-3 text-xs">No approved trainer at this university yet. Approve one in the Trainers tab first.</p>
        ) : null}
        <Select
          label="Coordinator"
          value={value}
          placeholder={trainers.data ? "Choose a trainer" : "Loading…"}
          onChange={(e) => setValue(e.target.value)}
          options={[{ value: "", label: "— none —" }, ...(trainers.data || []).map((t) => ({ value: String(t.person.id), label: `${t.person.name}${t.person.designation ? `, ${t.person.designation}` : ""}` }))]}
        />
        <div className="flex justify-end gap-2">
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={save} disabled={busy}>
            Save
          </Button>
        </div>
      </div>
    </Modal>
  );
}
