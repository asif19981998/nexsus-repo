"use client";

import { useState } from "react";
import Link from "next/link";
import { Badge, Button, DataTable, ErrorState, Select, StatusBadge, buttonClass } from "@/components/ui";
import { useApiQuery } from "@/hooks/useApi";
import { qs } from "@/lib/api";
import { formatDate } from "@/lib/formatters";

const INTERN_STATUSES = ["Active", "Completed", "Certified", "Not certified", "Terminated"];

export function InternsTab() {
  const [filter, setFilter] = useState("all");
  const list = useApiQuery(`/internship/interns${qs({ status: filter })}`);

  if (list.error) return <ErrorState title="Could not load interns" description={list.error.message} action={<Button onClick={list.reload}>Try again</Button>} />;

  return (
    <div className="space-y-4">
      <Select
        label="Status"
        value={filter}
        onChange={(e) => setFilter(e.target.value)}
        options={[{ value: "all", label: "All interns" }, ...INTERN_STATUSES.map((s) => ({ value: s, label: s }))]}
      />
      <DataTable
        emptyMessage={list.data ? "No interns in this view." : "Loading…"}
        rows={list.data || []}
        columns={[
          {
            key: "who",
            label: "Intern",
            render: (x) => (
              <div>
                <p className="font-medium">{x.student.name}</p>
                <p className="text-xs text-secondary">{x.batchName}</p>
              </div>
            ),
          },
          {
            key: "dept",
            label: "Department",
            render: (x) => (
              <div>
                <p>{x.department}</p>
                {x.mentor ? <p className="text-xs text-secondary">Mentor: {x.mentor}</p> : null}
              </div>
            ),
          },
          { key: "period", label: "Period", render: (x) => `${formatDate(x.startDate)} – ${formatDate(x.endDate)}` },
          {
            key: "att",
            label: "Attendance",
            render: (x) => (
              <Badge tone={x.belowMin ? "red" : "green"}>
                {x.attendancePct}%{x.belowMin ? " · low" : ""}
              </Badge>
            ),
          },
          {
            key: "pending",
            label: "Pending",
            render: (x) => {
              const bits = [];
              if (x.pendingLeave) bits.push(`${x.pendingLeave} leave`);
              if (x.monthlyToEvaluate) bits.push(`${x.monthlyToEvaluate} monthly report${x.monthlyToEvaluate > 1 ? "s" : ""}`);
              if (x.reportSubmitted) bits.push("report");
              return bits.length ? <Badge tone="amber">{bits.join(", ")}</Badge> : "—";
            },
          },
          { key: "status", label: "Status", render: (x) => <StatusBadge status={x.status} /> },
          {
            key: "act",
            label: "",
            render: (x) => (
              <Link href={`/internship/manage/interns/${x.id}`} className={buttonClass({ size: "sm", variant: "outline" })}>
                Manage
              </Link>
            ),
          },
        ]}
      />
    </div>
  );
}
