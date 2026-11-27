"use client";

import { useState } from "react";
import { Receipt } from "lucide-react";
import { Badge, Button, DataTable, ErrorState, Select, StatCard, StatusBadge } from "@/components/ui";
import { useSetup } from "@/components/layout/InternshipShell";
import { PaymentSlipModal, attendanceLabel, monthLabel } from "@/components/domain/PaymentSlip";
import { useAction, useApiQuery, useSession } from "@/hooks/useApi";
import { api, qs } from "@/lib/api";
import { P } from "@/lib/internshipPermissions";
import { formatCurrency } from "@/lib/formatters";

const BILL_STATUSES = ["Report due", "Awaiting evaluation", "Ready to pay", "Withheld", "Paid", "Accruing"];

export function StipendTab() {
  const { policy } = useSetup();
  const { can } = useSession();
  const [filter, setFilter] = useState("all");
  const [slip, setSlip] = useState(null);
  const { data, error, reload, setData } = useApiQuery(`/internship/bills${qs({ status: filter })}`);
  const { run, busy } = useAction();
  const rows = (data || []).map((b) => ({ ...b, id: `${b.applicationId}-${b.month}` }));
  const sum = (status) => rows.filter((b) => b.status === status).reduce((n, b) => n + (b.net ?? b.gross), 0);

  const pay = async (b) => {
    const next = await run(() => api.post(`/internship/bills/${b.applicationId}/${b.month}/pay${qs({ status: filter })}`), `Paid: ${b.studentName}, ${monthLabel(b.month)}`);
    if (!next) return;
    setData(next);
    // The slip is generated on payment; show it straight away.
    const paid = next.find((x) => x.applicationId === b.applicationId && x.month === b.month);
    if (paid?.slipNumber) setSlip(paid);
  };

  if (error) return <ErrorState title="Could not load bills" description={error.message} action={<Button onClick={reload}>Try again</Button>} />;

  return (
    <div className="space-y-4">
      <div className="grid gap-4 sm:grid-cols-4">
        <StatCard label="Ready to pay" value={formatCurrency(sum("Ready to pay"))} hint="Report evaluated" tone="amber" />
        <StatCard label="Awaiting evaluation" value={formatCurrency(sum("Awaiting evaluation"))} hint="Monthly report not yet scored" />
        <StatCard label="Withheld" value={formatCurrency(sum("Withheld"))} hint="Last month, final report not accepted (cl.11 ট)" tone="red" />
        <StatCard label="Paid" value={formatCurrency(sum("Paid"))} hint="Slip issued" tone="green" />
      </div>

      <p className="text-sm text-secondary">
        Allowance: {formatCurrency(policy.monthlyAllowance)} per month, excluding VAT and tax (cl.10). A month appears once it starts. When evaluating the intern&apos;s
        monthly report, the mentor enters the absent days to deduct (one day = monthly allowance ÷ working days in that month). Paying issues a slip.
      </p>

      <Select
        label="Status"
        value={filter}
        onChange={(e) => setFilter(e.target.value)}
        options={[{ value: "all", label: "Billable months" }, ...BILL_STATUSES.map((s) => ({ value: s, label: s }))]}
      />

      <DataTable
        emptyMessage={data ? "No bills in this view." : "Loading…"}
        rows={rows}
        columns={[
          {
            key: "name",
            label: "Intern",
            render: (b) => (
              <div>
                <p className="font-medium">{b.studentName}</p>
                <p className="text-xs text-secondary">{b.department}</p>
              </div>
            ),
          },
          { key: "month", label: "Month", render: (b) => monthLabel(b.month) },
          { key: "gross", label: "Gross", render: (b) => formatCurrency(b.gross) },
          { key: "att", label: "Attendance", render: (b) => <span className="text-xs">{attendanceLabel(b.attendance)}</span> },
          {
            key: "ded",
            label: "Deduction",
            render: (b) =>
              b.deductDays == null ? (
                <span className="text-xs text-secondary">At evaluation</span>
              ) : b.deductDays ? (
                <Badge tone="red">
                  −{formatCurrency(b.deduction)} · {b.deductDays} day{b.deductDays > 1 ? "s" : ""}
                </Badge>
              ) : (
                "—"
              ),
          },
          { key: "net", label: "Net", render: (b) => (b.net == null ? "—" : <strong>{formatCurrency(b.net)}</strong>) },
          { key: "score", label: "Evaluation", render: (b) => (b.score != null ? `${b.score}/100` : "—") },
          { key: "status", label: "Status", render: (b) => <StatusBadge status={b.status} /> },
          {
            key: "act",
            label: "",
            render: (b) =>
              b.status === "Ready to pay" && can(P.billPay) ? (
                <Button size="sm" disabled={busy} onClick={() => pay(b)}>
                  Pay {formatCurrency(b.net)}
                </Button>
              ) : b.status === "Paid" ? (
                <Button size="sm" variant="outline" onClick={() => setSlip(b)}>
                  <Receipt className="h-3.5 w-3.5" /> Slip
                </Button>
              ) : b.withheldReason && b.status === "Withheld" ? (
                <span className="text-xs text-secondary">{b.withheldReason}</span>
              ) : null,
          },
        ]}
      />

      <PaymentSlipModal slip={slip} onClose={() => setSlip(null)} />
    </div>
  );
}
