"use client";

import { useState } from "react";
import { Check, Info, XCircle } from "lucide-react";
import { Badge, Button, ConfirmDialog, StatusBadge } from "@/components/ui";
import { useAction, useSession } from "@/hooks/useApi";
import { api } from "@/lib/api";
import { P } from "@/lib/internshipPermissions";
import { formatDate } from "@/lib/formatters";
import { AttachedDocuments } from "@/components/documents/AttachedDocuments";
import { cn } from "@/lib/cn";

const STEPS = ["Submitted", "Shortlisted", "Test done", "Selected", "Active", "Completed", "Certified"];

function stepIndex(app) {
  switch (app.status) {
    case "Submitted":
      return 0;
    case "Shortlisted":
      return app.exam ? 2 : 1;
    case "Waitlisted":
      return 2;
    case "Selected":
      return 3;
    case "Active":
      return 4;
    case "Completed":
      return 5;
    case "Certified":
      return 6;
    default:
      return app.internship ? 4 : app.exam ? 2 : 0;
  }
}

const NEXT_HINT = {
  Submitted: "UGC is screening applications. You will be told if you are shortlisted.",
  Shortlisted: "Wait for UGC to record your selection test result, or attend the test if you have a date.",
  Waitlisted: "All seats are filled. UGC will offer a seat if one opens.",
  Selected: "You are selected. UGC will place you in a department and set your start date.",
  Active: "Your internship is running. Open the My internship tab.",
  Completed: "Submit your final report in the My internship tab.",
  Certified: "Your certificate is issued. Open the My internship tab to view it.",
};

const WITHDRAWABLE = ["Submitted", "Shortlisted", "Waitlisted", "Selected"];

function Card({ title, aside, children, className = "" }) {
  return (
    <section className={`card-surface space-y-4 p-5 text-sm ${className}`}>
      {title || aside ? (
        <div className="flex flex-wrap items-center justify-between gap-2">
          {title ? <h3 className="text-sm font-semibold uppercase tracking-wide text-slate-600 dark:text-slate-300">{title}</h3> : <span />}
          {aside}
        </div>
      ) : null}
      {children}
    </section>
  );
}

function Fact({ label, children }) {
  return (
    <div className="min-w-0">
      <p className="text-[11px] font-semibold uppercase tracking-[0.08em] text-slate-500">{label}</p>
      <div className="mt-1 font-medium text-slate-900 dark:text-slate-50">{children}</div>
    </div>
  );
}

export function ApplicationTab({ app, onChange }) {
  const { can } = useSession();
  const { run, busy } = useAction();
  const [confirm, setConfirm] = useState(false);
  const step = stepIndex(app);
  const ended = ["Rejected", "Withdrawn", "Terminated", "Not certified"].includes(app.status);

  const withdraw = async () => {
    const next = await run(() => api.post("/internship/me/withdraw"), "Application withdrawn");
    setConfirm(false);
    if (next) onChange(next);
  };

  return (
    <div className="space-y-5">
      <div className="card-surface grid gap-4 p-5 sm:grid-cols-2 lg:grid-cols-4">
        <Fact label="Batch">
          {app.batchName} <span className="text-xs font-normal text-secondary">{app.batchCode}</span>
        </Fact>
        <Fact label="Applied">{formatDate(app.appliedAt)}</Fact>
        <Fact label="Selection test">
          {app.exam ? (
            <span className="flex flex-wrap items-center gap-2">
              {app.exam.mode}, {formatDate(app.exam.date)} <Badge tone="blue">{app.exam.score}/100</Badge>
            </span>
          ) : (
            "Not yet"
          )}
        </Fact>
        <Fact label="Status">
          <StatusBadge status={app.status} />
        </Fact>
      </div>

      <Card title="Progress">
        <ol className="flex flex-wrap items-start gap-y-4" aria-label="Application progress">
          {STEPS.map((s, i) => {
            const done = !ended && i < step;
            const current = !ended && i === step;
            return (
              <li key={s} className="flex min-w-[6.5rem] flex-1 flex-col items-center gap-2 text-center" aria-current={current ? "step" : undefined}>
                <div className="flex w-full items-center">
                  <span className={cn("h-0.5 flex-1", i === 0 ? "bg-transparent" : done || current ? "bg-success" : "bg-slate-300 dark:bg-slate-600")} />
                  <span
                    className={cn(
                      "flex h-8 w-8 shrink-0 items-center justify-center rounded-full border-2 text-xs font-semibold",
                      done && "border-success bg-success text-white",
                      current && "border-success bg-[#e8f3ef] text-success",
                      !done && !current && "border-slate-300 text-secondary dark:border-slate-600",
                    )}
                  >
                    {done ? <Check className="h-4 w-4" /> : i + 1}
                  </span>
                  <span className={cn("h-0.5 flex-1", i === STEPS.length - 1 ? "bg-transparent" : done ? "bg-success" : "bg-slate-300 dark:bg-slate-600")} />
                </div>
                <span className={cn("text-xs", current ? "font-semibold text-success" : done ? "font-medium" : "text-secondary")}>{s}</span>
              </li>
            );
          })}
        </ol>

        {ended ? (
          <p role="status" className="flex items-start gap-2 rounded-xl bg-red-50 p-3 text-danger">
            <XCircle className="mt-0.5 h-4 w-4 shrink-0" />
            <span>
              {app.status === "Rejected" && `Not successful: ${app.rejectionReason || "no reason recorded"}.`}
              {app.status === "Withdrawn" && "You withdrew this application. You can apply to a later batch."}
              {app.status === "Terminated" && `Internship terminated: ${app.internship?.outcome?.reasons?.join(" ") || ""}`}
              {app.status === "Not certified" && `No certificate: ${app.internship?.outcome?.reasons?.join(" ") || ""}`}
            </span>
          </p>
        ) : (
          <p className="flex items-start gap-2 rounded-xl bg-[#e8f0f7] p-3 text-nexus-800 dark:bg-nexus-800/60 dark:text-nexus-100">
            <Info className="mt-0.5 h-4 w-4 shrink-0" />
            <span>
              <span className="font-semibold">Next: </span>
              {NEXT_HINT[app.status]}
            </span>
          </p>
        )}
      </Card>

      <div className="grid gap-5 lg:grid-cols-[3fr_2fr]">
        <Card title="Your application">
          <div className="grid gap-4 sm:grid-cols-2">
            <Fact label="Preferred departments">
              <span className="flex flex-wrap gap-1.5">
                {app.preferredDepartments.length
                  ? app.preferredDepartments.map((d, i) => (
                      <Badge key={d} tone="slate">
                        {i + 1}. {d}
                      </Badge>
                    ))
                  : "—"}
              </span>
            </Fact>
            <Fact label="Eligible as">{app.basis || "—"}</Fact>
          </div>
          <div>
            <p className="text-[11px] font-semibold uppercase tracking-[0.08em] text-slate-500">Statement of purpose</p>
            <p className="mt-1 max-h-60 overflow-y-auto whitespace-pre-wrap rounded-xl bg-white/60 p-3 dark:bg-nexus-800/60">{app.statement}</p>
          </div>
          {app.attachedDocuments?.length || app.documents?.length ? (
            <Fact label="Documents sent with this application">
              <AttachedDocuments attached={app.attachedDocuments} names={app.documents} />
            </Fact>
          ) : null}
          {WITHDRAWABLE.includes(app.status) && can(P.apply) ? (
            <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 pt-4 dark:border-slate-700">
              <p className="text-xs text-secondary">Changed your mind? You can withdraw until you are placed.</p>
              <Button variant="outline" onClick={() => setConfirm(true)}>
                Withdraw application
              </Button>
            </div>
          ) : null}
        </Card>

        <Card title="History" className="h-fit">
          <ol className="space-y-4 border-l-2 border-nexus-600/40 pl-4">
            {[...app.history].reverse().map((h, i) => (
              <li key={i} className="relative">
                <span className="absolute -left-[1.4rem] top-1.5 h-2.5 w-2.5 rounded-full bg-nexus-600" />
                <div className="flex flex-wrap items-center gap-2">
                  <StatusBadge status={h.status} />
                  <span className="text-xs text-secondary">{formatDate(h.at, "dd MMM yyyy, hh:mm a")}</span>
                </div>
                {h.note ? <p className="mt-1 text-secondary">{h.note}</p> : null}
              </li>
            ))}
          </ol>
        </Card>
      </div>

      <ConfirmDialog
        open={confirm}
        onClose={() => setConfirm(false)}
        title="Withdraw your application?"
        description="You can apply to a later batch if one opens."
        confirmLabel={busy ? "Withdrawing…" : "Withdraw"}
        danger
        onConfirm={withdraw}
      />
    </div>
  );
}
