"use client";

import { Printer } from "lucide-react";
import { Badge, Button, Modal } from "@/components/ui";
import { DAY_TONE } from "@/lib/internshipPermissions";
import { formatCurrency, formatDate } from "@/lib/formatters";

/** "2027-03" → "March 2027". */
export function monthLabel(month) {
  return formatDate(`${month}-01`, "MMMM yyyy");
}

/** Deduction cell: the mentor sets the absent days when evaluating the month, so it is blank until then. */
export function deductionLabel(b) {
  if (b.deductDays == null) return "At evaluation";
  if (!b.deductDays) return "—";
  return `−${formatCurrency(b.deduction)} (${b.deductDays} d)`;
}

/** The month's register so far, e.g. "18/20 present · 2 absent · 1 leave". */
export function attendanceLabel(a) {
  if (!a?.closedDays) return "—";
  return `${a.present}/${a.closedDays} present${a.unapproved ? ` · ${a.unapproved} absent` : ""}${a.approved ? ` · ${a.approved} leave` : ""}`;
}

/** cl.10: the month is reviewed on these four, each 0–25. Keys match the API's EvaluateMonthRequest and MonthlyScores. */
export const MONTHLY_CRITERIA = [
  { key: "attendance", label: "Attendance" },
  { key: "conduct", label: "Conduct" },
  { key: "discipline", label: "Discipline" },
  { key: "workSkill", label: "Work skill" },
];
export const MONTHLY_CRITERION_MAX = 25;

/** "Attendance 20 · Conduct 21 · …", or null for months evaluated before the four criteria. */
export function scoresLabel(scores) {
  if (!scores) return null;
  return MONTHLY_CRITERIA.map((c) => `${c.label} ${scores[c.key]}`).join(" · ");
}

/** The month's attendance register (cl.11 জ), which goes with the bill. */
export function AttendanceRegister({ days }) {
  if (!days?.length) return <p className="text-sm text-secondary">No working days yet this month.</p>;
  const cell = "px-3 py-1.5";
  return (
    <div className="overflow-x-auto rounded-xl border border-slate-200 dark:border-slate-700">
      <table className="w-full text-left text-xs">
        <thead className="bg-white/60 text-secondary dark:bg-nexus-800/60">
          <tr>
            <th className={cell}>Date</th>
            <th className={cell}>Status</th>
            <th className={cell}>In</th>
            <th className={cell}>Out</th>
            <th className={cell}>Reason</th>
          </tr>
        </thead>
        <tbody>
          {days.map((d) => (
            <tr key={d.date} className="border-t border-slate-200 dark:border-slate-700">
              <td className={cell}>{formatDate(d.date, "EEE dd MMM")}</td>
              <td className={cell}>
                <Badge tone={DAY_TONE[d.kind] || "slate"} className="h-5">
                  {d.label}
                </Badge>
              </td>
              <td className={cell}>{d.checkIn?.slice(0, 5) || "—"}</td>
              <td className={cell}>{d.checkOut?.slice(0, 5) || "—"}</td>
              <td className={cell}>{d.reason || ""}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function Row({ label, value, strong }) {
  return (
    <div className="flex justify-between gap-4 border-b border-dashed border-slate-300 py-1.5 last:border-b-0 dark:border-slate-600">
      <span className="text-secondary">{label}</span>
      <span className={strong ? "font-semibold" : undefined}>{value}</span>
    </div>
  );
}

function printSlip() {
  document.body.classList.add("printing-slip");
  const done = () => {
    document.body.classList.remove("printing-slip");
    window.removeEventListener("afterprint", done);
  };
  window.addEventListener("afterprint", done);
  window.print();
}

/**
 * The allowance payment slip (cl.10) for one paid month.
 * slip: { slipNumber, studentName, department, mentor?, batchName?, month, periodDays, gross, deduction, deductDays, net, score?, evaluatedBy?, paidAt, paidBy? }
 */
export function PaymentSlipModal({ slip, onClose }) {
  return (
    <Modal open={Boolean(slip)} onClose={onClose} title="Allowance payment slip" size="xl">
      {slip ? (
        <div className="space-y-4">
          <div className="slip-print space-y-4 rounded-2xl border-2 border-nexus-600 p-6 text-sm">
            <div className="flex flex-wrap items-start justify-between gap-3 border-b border-slate-300 pb-3 dark:border-slate-600">
              <div>
                <p className="text-xs uppercase tracking-[0.2em] text-secondary">University Grants Commission of Bangladesh</p>
                <h3 className="text-lg font-semibold">UGC Internship · Monthly allowance slip</h3>
              </div>
              <div className="text-right">
                <p className="text-xs text-secondary">Slip no.</p>
                <p className="font-mono font-semibold">{slip.slipNumber || "—"}</p>
              </div>
            </div>

            <div className="grid gap-x-8 sm:grid-cols-2">
              <div>
                <Row label="Intern" value={slip.studentName} strong />
                {slip.batchName ? <Row label="Batch" value={slip.batchName} /> : null}
                <Row label="Department" value={slip.department} />
                {slip.mentor ? <Row label="Mentor" value={slip.mentor} /> : null}
              </div>
              <div>
                <Row label="Month" value={monthLabel(slip.month)} strong />
                <Row label="Working days in period" value={slip.periodDays} />
                <Row label="Monthly evaluation" value={slip.score != null ? `${slip.score}/100${slip.evaluatedBy ? ` · ${slip.evaluatedBy}` : ""}` : "—"} />
                <Row label="Paid on" value={formatDate(slip.paidAt, "dd MMM yyyy, hh:mm a")} />
              </div>
            </div>

            <div className="rounded-xl bg-white/60 p-3 dark:bg-nexus-800/60">
              <Row label="Gross allowance" value={formatCurrency(slip.gross)} />
              <Row
                label={`Deduction${slip.deductDays ? ` (${slip.deductDays} unapproved day${slip.deductDays > 1 ? "s" : ""})` : ""}`}
                value={slip.deduction ? `−${formatCurrency(slip.deduction)}` : "—"}
              />
              <Row label="Net paid" value={formatCurrency(slip.net)} strong />
            </div>

            {slip.scores ? <p className="text-xs text-secondary">Monthly review (cl.10, out of 25 each): {scoresLabel(slip.scores)}</p> : null}

            <p className="text-xs text-secondary">Excluding VAT and tax (cl.10). Computer-generated slip; no signature needed.</p>
            <div className="flex justify-between pt-6 text-xs text-secondary">
              <span>Paid by: {slip.paidBy || "—"}</span>
              <span className="border-t border-slate-400 px-6 pt-1">Accounts &amp; Budget</span>
            </div>
          </div>

          <div className="flex justify-end">
            <Button onClick={printSlip}>
              <Printer className="h-4 w-4" /> Print / save PDF
            </Button>
          </div>
        </div>
      ) : null}
    </Modal>
  );
}
