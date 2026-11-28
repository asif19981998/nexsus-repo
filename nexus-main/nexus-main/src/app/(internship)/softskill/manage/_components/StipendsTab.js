"use client";

import { useState } from "react";
import { useSearchParams } from "next/navigation";
import { Badge, Button, DataTable, ErrorState, Modal, Select, Textarea } from "@/components/ui";
import { useAction, useApiQuery, useSession } from "@/hooks/useApi";
import { api, qs } from "@/lib/api";
import { SP, STIPEND_TONE } from "@/lib/softskillPermissions";
import { formatCurrency, formatDate } from "@/lib/formatters";
import { ErrorText } from "../../_components/ui";

const STATUSES = ["Ready to pay", "Withheld", "Paid", "Not eligible", "Awaiting assessment"];

/**
 * cl.10: one stipend per trainee per month passed. The finance section checks attendance, conduct and discipline,
 * then pays (a slip is issued) or withholds with a reason.
 */
export function StipendsTab({ setup }) {
  const { can } = useSession();
  const batches = useApiQuery("/softskill/batches");
  // A notification link (?batch=3) opens filtered to that batch.
  const linkedBatch = useSearchParams().get("batch");
  const [batchId, setBatchId] = useState(linkedBatch || "all");
  const [status, setStatus] = useState("Ready to pay");
  const list = useApiQuery(`/softskill/stipends${qs({ batchId, status })}`);
  const { run, busy } = useAction();
  const [withholding, setWithholding] = useState(null);
  const [reason, setReason] = useState("");
  const [error, setError] = useState("");
  const pay = can(SP.stipendPay);

  const act = async (r, path, body, ok) => {
    const res = await run(() => api.post(`/softskill/stipends/${r.traineeId}/${r.no}/${path}`, body), ok);
    if (res) list.reload();
    return res;
  };
  const withhold = async () => {
    setError("");
    try {
      await run(() => api.post(`/softskill/stipends/${withholding.traineeId}/${withholding.no}/withhold`, { reason }), "Stipend withheld", { silentError: true });
      setWithholding(null);
      setReason("");
      list.reload();
    } catch (e) {
      setError(e.message);
    }
  };

  if (list.error) return <ErrorState title="Could not load stipends" description={list.error.message} action={<Button onClick={list.reload}>Try again</Button>} />;
  const total = (list.data || []).filter((r) => r.status === "Ready to pay").reduce((n, r) => n + r.amount, 0);

  return (
    <div className="space-y-4">
      <div className="grid gap-3 sm:grid-cols-3">
        <Select
          label="Batch"
          value={batchId}
          onChange={(e) => setBatchId(e.target.value)}
          options={[{ value: "all", label: "All batches" }, ...(batches.data || []).map((b) => ({ value: String(b.id), label: `${b.name} (${b.code})` }))]}
        />
        <Select label="Status" value={status} onChange={(e) => setStatus(e.target.value)} options={[{ value: "all", label: "All" }, ...STATUSES.map((s) => ({ value: s, label: s }))]} />
      </div>
      <p className="text-sm text-secondary">
        {formatCurrency(setup.policy.monthlyStipend)} for each month passed; a failed month earns nothing and several tests in one month earn one stipend (cl.10 গ, ঘ).
        {total ? ` Ready to pay on this page: ${formatCurrency(total)}.` : ""}
      </p>
      <DataTable
        emptyMessage={list.data ? "Nothing here." : "Loading…"}
        rows={list.data || []}
        columns={[
          { key: "who", label: "Trainee", render: (r) => <span className="font-medium">{r.student}</span> },
          { key: "uni", label: "University group", render: (r) => `${r.university} · ${r.batchCode}` },
          { key: "no", label: "Month", render: (r) => r.no },
          { key: "marks", label: "Marks", render: (r) => (r.marks != null ? `${r.marks}/${r.maxMarks}` : "—") },
          { key: "att", label: "Month attendance", render: (r) => `${r.monthAttendancePct}%` },
          {
            key: "status",
            label: "Status",
            render: (r) => (
              <div>
                <Badge tone={STIPEND_TONE[r.status]}>{r.status}</Badge>
                {r.reason ? <p className="mt-1 text-xs text-secondary">{r.reason}</p> : null}
                {r.slipNumber ? (
                  <p className="mt-1 text-xs text-secondary">
                    {r.slipNumber} · {formatDate(r.paidAt)} · {r.paidBy}
                  </p>
                ) : null}
              </div>
            ),
          },
          { key: "amount", label: "Amount", render: (r) => (r.amount ? formatCurrency(r.amount) : "—") },
          {
            key: "act",
            label: "",
            render: (r) =>
              pay ? (
                <div className="flex gap-1.5">
                  {r.status === "Ready to pay" ? (
                    <>
                      <Button size="sm" disabled={busy} onClick={() => act(r, "pay", {}, `Paid month ${r.no} to ${r.student}`)}>
                        Pay
                      </Button>
                      <Button size="sm" variant="ghost" onClick={() => (setError(""), setWithholding(r))}>
                        Withhold
                      </Button>
                    </>
                  ) : null}
                  {r.status === "Withheld" ? (
                    <Button size="sm" variant="outline" disabled={busy} onClick={() => act(r, "release", {}, "Released")}>
                      Release
                    </Button>
                  ) : null}
                </div>
              ) : null,
          },
        ]}
      />
      <Modal open={Boolean(withholding)} onClose={() => setWithholding(null)} title={`Withhold month ${withholding?.no || ""} for ${withholding?.student || ""}`} description="cl.10 (খ): after checking attendance, conduct and discipline.">
        <div className="space-y-4">
          <Textarea label="Reason" value={reason} onChange={(e) => setReason(e.target.value)} rows={3} required />
          <ErrorText>{error}</ErrorText>
          <div className="flex justify-end gap-2">
            <Button variant="ghost" onClick={() => setWithholding(null)}>
              Cancel
            </Button>
            <Button onClick={withhold} disabled={busy || !reason.trim()}>
              Withhold
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
