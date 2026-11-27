"use client";

import { useState } from "react";
import { Badge, Button, DataTable, Input, Modal, Select, Textarea } from "@/components/ui";
import { useAction } from "@/hooks/useApi";
import { api } from "@/lib/api";
import { HONORARIUM_KIND, HONORARIUM_TONE, VISIT_TONE } from "@/lib/softskillPermissions";
import { formatCurrency, formatDate } from "@/lib/formatters";
import { ErrorText, today } from "./ui";

/** The four cl.14 (ঘ) ratings a monitor gives, each 1–5. */
export const RATINGS = [
  ["delivery", "Trainers' delivery"],
  ["engagement", "Trainees' participation"],
  ["attendance", "Punctuality and attendance records"],
  ["facilities", "Venue and equipment (cl.12 ঘ)"],
];

const SCALE = [1, 2, 3, 4, 5].map((n) => ({ value: String(n), label: `${n} — ${["Poor", "Weak", "Adequate", "Good", "Excellent"][n - 1]}` }));

/** One visit: when, who, what was observed, and the report once filed. */
export function VisitCard({ v, children }) {
  return (
    <div className="space-y-2 rounded-xl border border-slate-200 p-4 text-sm dark:border-slate-700">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <p className="font-semibold">
            {v.university} <span className="text-xs font-normal text-secondary">{v.batchCode}</span>
          </p>
          <p className="text-xs text-secondary">
            {formatDate(v.visitedOn || v.plannedOn)} · {v.monitor.name}
            {v.session ? ` · ${v.session}` : ""}
          </p>
          {v.purpose ? <p className="text-xs text-secondary">Purpose: {v.purpose}</p> : null}
        </div>
        <div className="flex items-center gap-2">
          {v.average != null ? <Badge tone={v.average >= 3 ? "green" : "red"}>{v.average}/5</Badge> : null}
          <Badge tone={VISIT_TONE[v.status]}>{v.status}</Badge>
        </div>
      </div>
      {v.status === "Submitted" ? (
        <div className="space-y-2">
          <dl className="grid gap-2 sm:grid-cols-4">
            {RATINGS.map(([key, label]) => (
              <div key={key}>
                <dt className="text-xs text-secondary">{label}</dt>
                <dd className="font-medium">{v[key]}/5</dd>
              </div>
            ))}
          </dl>
          <div>
            <p className="text-xs text-secondary">Findings</p>
            <p className="whitespace-pre-line">{v.findings}</p>
          </div>
          {v.recommendations ? (
            <div>
              <p className="text-xs text-secondary">Recommendations</p>
              <p className="whitespace-pre-line">{v.recommendations}</p>
            </div>
          ) : null}
        </div>
      ) : null}
      {children}
    </div>
  );
}

/** The monitor's report form (cl.14 ঘ). */
export function VisitReportModal({ visit, onClose, onSaved }) {
  const { run, busy } = useAction();
  const [form, setForm] = useState({ visitedOn: visit.plannedOn <= today() ? visit.plannedOn : today(), delivery: "", engagement: "", attendance: "", facilities: "", findings: "", recommendations: "" });
  const [error, setError] = useState("");
  const set = (k) => (e) => setForm((f) => ({ ...f, [k]: e.target.value }));

  const save = async () => {
    setError("");
    const body = { ...form };
    RATINGS.forEach(([k]) => (body[k] = form[k] ? Number(form[k]) : null));
    try {
      onSaved(await run(() => api.post(`/softskill/monitoring/visits/${visit.id}/report`, body), "Report filed", { silentError: true }));
    } catch (e) {
      setError(e.message);
    }
  };

  return (
    <Modal open onClose={onClose} title={`Monitoring report — ${visit.university}`} description={`${visit.batchName}${visit.session ? ` · ${visit.session}` : ""}`} size="lg">
      <div className="space-y-4">
        <Input label="Date of the visit" type="date" value={form.visitedOn} onChange={set("visitedOn")} />
        <div className="grid gap-4 sm:grid-cols-2">
          {RATINGS.map(([key, label]) => (
            <Select key={key} label={label} value={form[key]} placeholder="Rate 1–5" onChange={set(key)} options={SCALE} />
          ))}
        </div>
        <Textarea label="Findings" value={form.findings} onChange={set("findings")} rows={5} hint="What you saw: delivery, participation, records, facilities. At least 30 characters." required />
        <Textarea label="Recommendations (optional)" value={form.recommendations} onChange={set("recommendations")} rows={3} hint="The coordinator sees these on the university group's Monitoring tab." />
        <ErrorText>{error}</ErrorText>
        <div className="flex justify-end gap-2">
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={save} disabled={busy}>
            File report
          </Button>
        </div>
      </div>
    </Modal>
  );
}

/** Honorarium rows (cl.17). `onPay` shows the Pay button on rows that are due. */
export function HonorariumTable({ rows, onPay, busy, showPerson = true, emptyMessage = "No honorarium yet." }) {
  return (
    <DataTable
      emptyMessage={emptyMessage}
      rows={rows || []}
      columns={[
        ...(showPerson
          ? [
              {
                key: "who",
                label: "Person",
                render: (h) => (
                  <span>
                    <span className="font-medium">{h.person.name}</span> <span className="text-xs text-secondary">{h.role}</span>
                  </span>
                ),
              },
            ]
          : []),
        { key: "kind", label: "For", render: (h) => HONORARIUM_KIND[h.kind] || h.kind },
        {
          key: "what",
          label: "Details",
          render: (h) => (
            <span>
              {h.description}
              <span className="block text-xs text-secondary">
                {h.university} · {h.batchCode} · {formatDate(h.date)}
              </span>
            </span>
          ),
        },
        { key: "amount", label: "Amount", render: (h) => (h.amount ? formatCurrency(h.amount) : "—") },
        {
          key: "status",
          label: "Status",
          render: (h) => (
            <div>
              <Badge tone={HONORARIUM_TONE[h.status] || "slate"}>{h.status}</Badge>
              {h.slipNumber ? <p className="mt-1 text-xs text-secondary">{h.slipNumber}</p> : null}
            </div>
          ),
        },
        ...(onPay
          ? [
              {
                key: "act",
                label: "",
                render: (h) =>
                  h.status === "Due" ? (
                    <Button size="sm" disabled={busy} onClick={() => onPay(h)}>
                      Pay
                    </Button>
                  ) : null,
              },
            ]
          : []),
      ]}
    />
  );
}

/** Totals for a list of honoraria: what was paid and what is still due. */
export function honorariumTotals(rows) {
  const sum = (s) => (rows || []).filter((h) => h.status === s).reduce((n, h) => n + h.amount, 0);
  return { paid: sum("Paid"), due: sum("Due") };
}
