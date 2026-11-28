"use client";

import { useState } from "react";
import { useSearchParams } from "next/navigation";
import { Badge, Button, DataTable, Drawer, ErrorState, Input, Select, StatusBadge, Textarea } from "@/components/ui";
import { useAction, useApiQuery, useSession } from "@/hooks/useApi";
import { useOpenDocument } from "@/hooks/useOpenDocument";
import { api, qs } from "@/lib/api";
import { SP } from "@/lib/softskillPermissions";
import { formatDate } from "@/lib/formatters";
import { ErrorText, Fact } from "../../_components/ui";

/** cl.9: the Commission screens applications and selects up to each university's seats (cl.6 ক). */
export function ApplicationsTab({ setup }) {
  const batches = useApiQuery("/softskill/batches");
  const [batchId, setBatchId] = useState("all");
  const [status, setStatus] = useState("all");
  const list = useApiQuery(`/softskill/applications${qs({ batchId, status })}`);
  // A notification link (?app=12) opens that application.
  const linked = useSearchParams().get("app");
  const [openId, setOpenId] = useState(linked ? Number(linked) : null);

  if (list.error) return <ErrorState title="Could not load applications" description={list.error.message} action={<Button onClick={list.reload}>Try again</Button>} />;

  return (
    <div className="space-y-4">
      <div className="grid gap-3 sm:grid-cols-3">
        <Select
          label="Batch"
          value={batchId}
          onChange={(e) => setBatchId(e.target.value)}
          options={[{ value: "all", label: "All batches" }, ...(batches.data || []).map((b) => ({ value: String(b.id), label: `${b.name} (${b.code})` }))]}
        />
        <Select label="Status" value={status} onChange={(e) => setStatus(e.target.value)} options={[{ value: "all", label: "All statuses" }, ...setup.applicationStatuses.map((s) => ({ value: s, label: s }))]} />
      </div>
      <DataTable
        emptyMessage={list.data ? "No applications." : "Loading…"}
        rows={list.data || []}
        columns={[
          {
            key: "student",
            label: "Student",
            render: (a) => (
              <button type="button" className="text-left font-medium text-nexus-700 hover:underline dark:text-nexus-300" onClick={() => setOpenId(a.id)}>
                {a.student.name}
                <span className="block text-xs font-normal text-secondary">{a.basis}</span>
              </button>
            ),
          },
          { key: "uni", label: "University", render: (a) => a.university },
          { key: "batch", label: "Batch", render: (a) => a.batchCode },
          { key: "applied", label: "Applied", render: (a) => formatDate(a.appliedAt) },
          { key: "score", label: "Screening", render: (a) => (a.screeningScore != null ? `${a.screeningScore}/100` : "—") },
          { key: "status", label: "Status", render: (a) => <StatusBadge status={a.status} /> },
        ]}
      />
      {openId ? (
        <ApplicationDrawer
          id={openId}
          onClose={() => setOpenId(null)}
          onChanged={() => {
            list.reload();
            batches.reload();
          }}
        />
      ) : null}
    </div>
  );
}

function ApplicationDrawer({ id, onClose, onChanged }) {
  const { can } = useSession();
  const app = useApiQuery(`/softskill/applications/${id}`);
  const viewer = useOpenDocument();
  const { run, busy } = useAction();
  const [score, setScore] = useState("");
  const [reason, setReason] = useState("");
  const [error, setError] = useState("");
  const a = app.data;

  const act = async (path, body, ok) => {
    setError("");
    try {
      const next = await run(() => api.post(`/softskill/applications/${id}/${path}`, body), ok, { silentError: true });
      app.setData(next);
      onChanged();
    } catch (e) {
      setError(e.message);
    }
  };

  const before = a && ["Submitted", "Shortlisted", "Waitlisted"].includes(a.status);

  return (
    <Drawer open onClose={onClose} title={a ? a.student.name : "Application"}>
      {!a ? (
        <p className="p-4 text-secondary">Loading…</p>
      ) : (
        <div className="space-y-5 p-1 text-sm">
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={a.status} />
            <Badge tone="slate">{a.university}</Badge>
            <Badge tone="slate">{a.batchCode}</Badge>
          </div>
          <dl className="grid gap-3 sm:grid-cols-2">
            <Fact label="Email">{a.student.email}</Fact>
            <Fact label="Qualified as (cl.7)">{a.basis}</Fact>
            <Fact label="Applied">{formatDate(a.appliedAt)}</Fact>
            <Fact label="Screening score">{a.screeningScore != null ? `${a.screeningScore}/100` : null}</Fact>
          </dl>
          <div>
            <p className="text-xs text-secondary">Statement</p>
            <p className="whitespace-pre-line">{a.statement}</p>
          </div>
          <div className="space-y-1">
            <p className="text-xs text-secondary">Documents sent</p>
            <ul className="space-y-1">
              {(a.attachedDocuments || []).map((d) => (
                <li key={d.id} className="flex items-center justify-between gap-2">
                  <span>
                    {d.typeName} <span className="text-xs text-secondary">v{d.version}</span>
                  </span>
                  <span className="flex items-center gap-2">
                    <Badge tone={d.status === "Verified" ? "green" : d.status === "Rejected" ? "red" : "amber"}>{d.status}</Badge>
                    <Button size="sm" variant="ghost" disabled={viewer.busy} onClick={() => viewer.open(d)}>
                      View
                    </Button>
                  </span>
                </li>
              ))}
            </ul>
          </div>

          {before && (can(SP.applicationScreen) || can(SP.applicationSelect)) ? (
            <div className="space-y-3 rounded-xl border border-slate-200 p-4 dark:border-slate-700">
              <p className="font-semibold">Screening (cl.9)</p>
              {can(SP.applicationScreen) ? (
                <div className="flex flex-wrap items-end gap-2">
                  <Input label="Score (optional, 0–100)" type="number" value={score} onChange={(e) => setScore(e.target.value)} className="max-w-40" />
                  <Button size="sm" variant="outline" disabled={busy || score === ""} onClick={() => act("score", { score: Number(score) }, "Score recorded")}>
                    Record score
                  </Button>
                </div>
              ) : null}
              <div className="flex flex-wrap gap-2">
                {can(SP.applicationScreen) && a.status === "Submitted" ? (
                  <Button size="sm" variant="outline" disabled={busy} onClick={() => act("shortlist", {}, "Shortlisted")}>
                    Shortlist
                  </Button>
                ) : null}
                {can(SP.applicationSelect) ? (
                  <Button size="sm" disabled={busy} onClick={() => act("select", {}, "Selected")}>
                    Select
                  </Button>
                ) : null}
                {can(SP.applicationScreen) && a.status !== "Waitlisted" ? (
                  <Button size="sm" variant="ghost" disabled={busy} onClick={() => act("waitlist", {}, "Waitlisted")}>
                    Waitlist
                  </Button>
                ) : null}
              </div>
            </div>
          ) : null}

          {can(SP.applicationScreen) && (before || a.status === "Selected") ? (
            <div className="space-y-2">
              <Textarea label="Reject with reason" value={reason} onChange={(e) => setReason(e.target.value)} rows={2} />
              <Button size="sm" variant="danger" disabled={busy || !reason.trim()} onClick={() => act("reject", { reason }, "Application rejected")}>
                Reject
              </Button>
            </div>
          ) : null}
          <ErrorText>{error}</ErrorText>

          <div>
            <p className="mb-2 text-xs text-secondary">History</p>
            <ol className="space-y-2">
              {a.history.map((h, i) => (
                <li key={i}>
                  <span className="font-medium">{h.status}</span> <span className="text-xs text-secondary">{formatDate(h.at, "dd MMM yyyy, HH:mm")} · {h.byName}</span>
                  {h.note ? <span className="block text-xs text-secondary">{h.note}</span> : null}
                </li>
              ))}
            </ol>
          </div>
        </div>
      )}
    </Drawer>
  );
}
