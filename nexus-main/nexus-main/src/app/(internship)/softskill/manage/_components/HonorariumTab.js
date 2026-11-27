"use client";

import { useState } from "react";
import { Button, ErrorState, Input, Select } from "@/components/ui";
import { useAction, useApiQuery, useSession } from "@/hooks/useApi";
import { api, qs } from "@/lib/api";
import { HONORARIUM_KIND, SP } from "@/lib/softskillPermissions";
import { formatCurrency } from "@/lib/formatters";
import { Card, ErrorText } from "../../_components/ui";
import { HonorariumTable, honorariumTotals } from "../../_components/oversight";

/**
 * cl.17: honoraria of trainers (per session held), coordinators (per university group, after the final report) and monitors
 * (per visit reported), at rates set from the government's notices. Paying fixes the amount and issues a slip.
 */
export function HonorariumTab() {
  const { can } = useSession();
  const batches = useApiQuery("/softskill/batches");
  const [batchId, setBatchId] = useState("all");
  const [status, setStatus] = useState("all");
  const [kind, setKind] = useState("all");
  const list = useApiQuery(`/softskill/honoraria${qs({ batchId, status, kind })}`);
  const { run, busy } = useAction();

  const pay = async (h) => {
    if (await run(() => api.post(`/softskill/honoraria/${h.kind}/${h.sourceId}/pay`), `Paid ${formatCurrency(h.amount)} to ${h.person.name}`)) list.reload();
  };

  if (list.error) return <ErrorState title="Could not load honoraria" description={list.error.message} action={<Button onClick={list.reload}>Try again</Button>} />;
  const totals = honorariumTotals(list.data);

  return (
    <div className="space-y-5">
      {can(SP.honorariumRates) ? <RatesCard onSaved={list.reload} /> : null}
      <Card title="Honoraria" description={`On this page: paid ${formatCurrency(totals.paid)}, due ${formatCurrency(totals.due)}.`}>
        <div className="grid gap-3 sm:grid-cols-3">
          <Select
            label="Batch"
            value={batchId}
            onChange={(e) => setBatchId(e.target.value)}
            options={[{ value: "all", label: "All batches" }, ...(batches.data || []).map((b) => ({ value: String(b.id), label: `${b.name} (${b.code})` }))]}
          />
          <Select
            label="Status"
            value={status}
            onChange={(e) => setStatus(e.target.value)}
            options={["all", "Due", "Rate not set", "Paid"].map((s) => ({ value: s, label: s === "all" ? "All" : s }))}
          />
          <Select
            label="For"
            value={kind}
            onChange={(e) => setKind(e.target.value)}
            options={[{ value: "all", label: "All" }, ...Object.entries(HONORARIUM_KIND).map(([value, label]) => ({ value, label }))]}
          />
        </div>
        <HonorariumTable rows={list.data} onPay={can(SP.honorariumPay) ? pay : null} busy={busy} emptyMessage={list.data ? "Nothing here." : "Loading…"} />
      </Card>
    </div>
  );
}

function RatesCard({ onSaved }) {
  const rates = useApiQuery("/softskill/honoraria/rates");
  const { run, busy } = useAction();
  const [form, setForm] = useState(null);
  const [error, setError] = useState("");
  const f = form || (rates.data ? { trainerPerSession: String(rates.data.trainerPerSession), coordinatorPerUniversityGroup: String(rates.data.coordinatorPerUniversityGroup), expertPerVisit: String(rates.data.expertPerVisit) } : null);
  const set = (k) => (e) => setForm({ ...f, [k]: e.target.value });

  const save = async () => {
    setError("");
    try {
      const saved = await run(
        () =>
          api.put("/softskill/honoraria/rates", {
            trainerPerSession: f.trainerPerSession === "" ? null : Number(f.trainerPerSession),
            coordinatorPerUniversityGroup: f.coordinatorPerUniversityGroup === "" ? null : Number(f.coordinatorPerUniversityGroup),
            expertPerVisit: f.expertPerVisit === "" ? null : Number(f.expertPerVisit),
          }),
        "Rates saved",
        { silentError: true },
      );
      rates.setData(saved);
      setForm(null);
      onSaved();
    } catch (e) {
      setError(e.message);
    }
  };

  return (
    <Card title="Rates (BDT)" description="From the government's notices (cl.17). A rate of 0 means not set; nothing is paid at 0. Already-paid amounts do not change.">
      {f ? (
        <div className="space-y-3">
          <div className="grid gap-4 sm:grid-cols-3">
            <Input label="Trainer, per session" type="number" value={f.trainerPerSession} onChange={set("trainerPerSession")} />
            <Input label="Coordinator, per university group" type="number" value={f.coordinatorPerUniversityGroup} onChange={set("coordinatorPerUniversityGroup")} />
            <Input label="Monitor, per visit" type="number" value={f.expertPerVisit} onChange={set("expertPerVisit")} />
          </div>
          <ErrorText>{error}</ErrorText>
          <Button onClick={save} disabled={busy || !form}>
            Save rates
          </Button>
        </div>
      ) : (
        <p className="text-secondary">Loading…</p>
      )}
    </Card>
  );
}
