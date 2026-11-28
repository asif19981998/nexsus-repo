"use client";

import { useState } from "react";
import { useSearchParams } from "next/navigation";
import { Badge, Button, DataTable, Drawer, ErrorState, Input, Select, Textarea } from "@/components/ui";
import { useAction, useApiQuery } from "@/hooks/useApi";
import { useOpenDocument } from "@/hooks/useOpenDocument";
import { api, qs } from "@/lib/api";
import { TRAINER_TONE } from "@/lib/softskillPermissions";
import { formatDate } from "@/lib/formatters";
import { ErrorText, Fact, today } from "../../_components/ui";

const STATUSES = ["Submitted", "Approved", "Rejected", "Suspended"];

/** cl.12: approve university teachers as trainers and record their Training of Trainers. */
export function TrainersTab({ setup }) {
  const [status, setStatus] = useState("Submitted");
  const [universityId, setUniversityId] = useState("all");
  const list = useApiQuery(`/softskill/trainers${qs({ status, universityId })}`);
  // A notification link (?trainer=7) opens that registration.
  const linked = useSearchParams().get("trainer");
  const [open, setOpen] = useState(linked ? Number(linked) : null);

  if (list.error) return <ErrorState title="Could not load trainers" description={list.error.message} action={<Button onClick={list.reload}>Try again</Button>} />;

  return (
    <div className="space-y-4">
      <div className="grid gap-3 sm:grid-cols-3">
        <Select label="Status" value={status} onChange={(e) => setStatus(e.target.value)} options={[{ value: "all", label: "All" }, ...STATUSES.map((s) => ({ value: s, label: s }))]} />
        <Select
          label="University"
          value={universityId}
          onChange={(e) => setUniversityId(e.target.value)}
          options={[{ value: "all", label: "All universities" }, ...setup.universities.map((u) => ({ value: String(u.id), label: u.name }))]}
        />
      </div>
      <DataTable
        emptyMessage={list.data ? "No trainers here." : "Loading…"}
        rows={list.data || []}
        columns={[
          {
            key: "name",
            label: "Teacher",
            render: (t) => (
              <button type="button" className="text-left font-medium text-nexus-700 hover:underline dark:text-nexus-300" onClick={() => setOpen(t.id)}>
                {t.person.name}
                <span className="block text-xs font-normal text-secondary">{[t.person.designation, t.academicDepartment].filter(Boolean).join(", ")}</span>
              </button>
            ),
          },
          { key: "uni", label: "University", render: (t) => t.university.name },
          { key: "applied", label: "Applied", render: (t) => formatDate(t.appliedAt) },
          { key: "status", label: "Status", render: (t) => <Badge tone={TRAINER_TONE[t.status]}>{t.status}</Badge> },
          { key: "tot", label: "ToT (cl.12 খ)", render: (t) => (t.totCompleted ? <Badge tone="green">{formatDate(t.totCompletedOn)}</Badge> : <Badge tone="amber">Pending</Badge>) },
        ]}
      />
      {open ? <TrainerDrawer id={open} onClose={() => setOpen(null)} onChanged={list.reload} /> : null}
    </div>
  );
}

function TrainerDrawer({ id, onClose, onChanged }) {
  const t = useApiQuery(`/softskill/trainers/${id}`);
  const viewer = useOpenDocument();
  const { run, busy } = useAction();
  const [note, setNote] = useState("");
  const [tot, setTot] = useState({ completedOn: today(), reference: "" });
  const [error, setError] = useState("");
  const d = t.data;

  const act = async (path, body, ok) => {
    setError("");
    try {
      t.setData(await run(() => api.post(`/softskill/trainers/${id}/${path}`, body), ok, { silentError: true }));
      setNote("");
      onChanged();
    } catch (e) {
      setError(e.message);
    }
  };

  return (
    <Drawer open onClose={onClose} title={d ? d.person.name : "Trainer"}>
      {!d ? (
        <p className="p-4 text-secondary">Loading…</p>
      ) : (
        <div className="space-y-5 p-1 text-sm">
          <div className="flex flex-wrap gap-2">
            <Badge tone={TRAINER_TONE[d.status]}>{d.status}</Badge>
            <Badge tone={d.canTrain ? "green" : "slate"}>{d.canTrain ? "Can take sessions" : "Cannot take sessions yet"}</Badge>
          </div>
          <dl className="grid gap-3 sm:grid-cols-2">
            <Fact label="University">{d.university.name}</Fact>
            <Fact label="Designation">{d.person.designation}</Fact>
            <Fact label="Department">{d.academicDepartment}</Fact>
            <Fact label="Email">{d.person.email}</Fact>
            <Fact label="ToT">{d.totCompleted ? `${formatDate(d.totCompletedOn)}${d.totReference ? ` · ${d.totReference}` : ""}` : "Not recorded"}</Fact>
            {d.decisionNote ? <Fact label="Last note">{d.decisionNote}</Fact> : null}
          </dl>
          <div>
            <p className="text-xs text-secondary">Experience</p>
            <p className="whitespace-pre-line">{d.statement}</p>
          </div>
          <div className="space-y-1">
            <p className="text-xs text-secondary">Documents sent</p>
            {(d.attachedDocuments || []).map((doc) => (
              <div key={doc.id} className="flex items-center justify-between gap-2">
                <span>{doc.typeName}</span>
                <Button size="sm" variant="ghost" disabled={viewer.busy} onClick={() => viewer.open(doc)}>
                  View
                </Button>
              </div>
            ))}
          </div>

          <div className="space-y-2 rounded-xl border border-slate-200 p-4 dark:border-slate-700">
            <p className="font-semibold">Decision</p>
            <Textarea label="Note (required to reject or suspend)" value={note} onChange={(e) => setNote(e.target.value)} rows={2} />
            <div className="flex flex-wrap gap-2">
              {["Submitted", "Suspended"].includes(d.status) ? (
                <Button size="sm" disabled={busy} onClick={() => act("approve", { note }, "Trainer approved")}>
                  Approve
                </Button>
              ) : null}
              {d.status === "Submitted" ? (
                <Button size="sm" variant="ghost" disabled={busy || !note.trim()} onClick={() => act("reject", { note }, "Registration rejected")}>
                  Reject
                </Button>
              ) : null}
              {d.status === "Approved" ? (
                <Button size="sm" variant="ghost" disabled={busy || !note.trim()} onClick={() => act("suspend", { note }, "Trainer suspended")}>
                  Suspend
                </Button>
              ) : null}
            </div>
          </div>

          <div className="space-y-2 rounded-xl border border-slate-200 p-4 dark:border-slate-700">
            <p className="font-semibold">Training of Trainers (cl.12 খ)</p>
            <div className="grid gap-3 sm:grid-cols-2">
              <Input label="Completed on" type="date" value={tot.completedOn} onChange={(e) => setTot((x) => ({ ...x, completedOn: e.target.value }))} />
              <Input label="Reference" value={tot.reference} onChange={(e) => setTot((x) => ({ ...x, reference: e.target.value }))} placeholder="Certificate / batch no." />
            </div>
            <Button size="sm" variant="outline" disabled={busy} onClick={() => act("tot", tot, "ToT recorded")}>
              {d.totCompleted ? "Update ToT" : "Record ToT"}
            </Button>
          </div>
          <ErrorText>{error}</ErrorText>
        </div>
      )}
    </Drawer>
  );
}
