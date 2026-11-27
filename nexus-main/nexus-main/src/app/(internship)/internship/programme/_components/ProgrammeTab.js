"use client";

import { useState } from "react";
import Link from "next/link";
import { CalendarRange, CircleCheck, CircleX, FileText, ImageIcon, Percent, Users, Wallet } from "lucide-react";
import { Badge, Button, Checkbox, EmptyState, Modal, Progress, StatCard, Textarea } from "@/components/ui";
import { useSetup } from "@/components/layout/InternshipShell";
import { useAction, useApiQuery, useSession } from "@/hooks/useApi";
import { useOpenDocument } from "@/hooks/useOpenDocument";
import { api } from "@/lib/api";
import { P } from "@/lib/internshipPermissions";
import { fileSizeLabel, formatCurrency, formatDate } from "@/lib/formatters";

function Card({ title, description, aside, children, className = "" }) {
  return (
    <section className={`card-surface space-y-4 p-5 text-sm ${className}`}>
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <h3 className="text-sm font-semibold uppercase tracking-wide text-slate-600 dark:text-slate-300">{title}</h3>
          {description ? <p className="mt-1 text-xs text-secondary">{description}</p> : null}
        </div>
        {aside}
      </div>
      {children}
    </section>
  );
}

/** How a UGC internship runs, start to finish (cl.9–15). */
const JOURNEY = [
  ["Apply", "To an open batch, with a statement and up to 3 departments"],
  ["Selection test", "Written, oral or online (cl.9)"],
  ["Placement", "In a UGC department with a mentor"],
  ["Monthly report", "Each month: report → review → allowance paid with a slip (cl.10)"],
  ["Final report", "Within 15 working days of the end (cl.11 ট)"],
  ["Certificate", "Graded on attendance, report and evaluation (cl.15)"],
];

const DOC_STATUS = {
  Submitted: { tone: "amber", label: "Waiting for review" },
  Verified: { tone: "green", label: "Verified" },
};

/**
 * The documents that go with an application, matched from the person's library against the internship checklist.
 * Submitting attaches exactly these versions; each one opens in a new tab.
 */
function ApplyDocuments({ checklist, error }) {
  const viewer = useOpenDocument();
  if (error) return <p className="text-sm text-danger">{error.message}</p>;
  if (!checklist) return <p className="text-sm text-secondary">Matching your documents…</p>;
  const rows = checklist.rows.filter((r) => r.met || r.required);
  if (!rows.length) return null;
  return (
    <section className="space-y-2">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h3 className="text-sm font-medium">
          Documents sent with this application <span className="text-xs font-normal text-secondary">(cl.7)</span>
        </h3>
        <Link href="/account/documents" className="text-xs font-medium text-nexus-700 hover:underline dark:text-nexus-300">
          Manage in My documents
        </Link>
      </div>
      <ul className="divide-y divide-[#d5e3df] rounded-xl border border-[#d5e3df] dark:divide-nexus-700 dark:border-nexus-700">
        {rows.map((r) => {
          const doc = r.document;
          const name = r.types.map((t) => t.name).join(" or ");
          if (!doc)
            return (
              <li key={r.groupKey} className="flex flex-wrap items-center gap-3 px-3 py-2.5">
                <CircleX className="h-4 w-4 shrink-0 text-danger" aria-hidden />
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-medium">{name}</p>
                  <p className="text-xs text-danger">Missing. Upload it in My documents before you apply.</p>
                </div>
                <Link href="/account/documents" className="text-xs font-medium text-nexus-700 hover:underline dark:text-nexus-300">
                  Upload
                </Link>
              </li>
            );
          const Icon = doc.contentType?.startsWith("image/") ? ImageIcon : FileText;
          const st = DOC_STATUS[doc.status];
          return (
            <li key={r.groupKey}>
              <button
                type="button"
                onClick={() => viewer.open(doc)}
                disabled={viewer.busy}
                title="Open this file"
                className="flex w-full items-center gap-3 px-3 py-2.5 text-left transition-colors hover:bg-sage/60 dark:hover:bg-nexus-800/60"
              >
                <Icon className="h-4 w-4 shrink-0 text-success" aria-hidden />
                <span className="min-w-0 flex-1">
                  <span className="flex flex-wrap items-center gap-2 text-sm font-medium">
                    {doc.typeName}
                    {!r.required ? <span className="text-xs font-normal text-secondary">optional</span> : null}
                  </span>
                  <span className="block truncate text-xs text-secondary">
                    {doc.fileName} · {fileSizeLabel(doc.sizeBytes)} · version {doc.version}
                  </span>
                </span>
                {st ? <Badge tone={st.tone}>{st.label}</Badge> : null}
                <span className="text-xs font-medium text-nexus-700 dark:text-nexus-300">View</span>
              </button>
            </li>
          );
        })}
      </ul>
      <p className="text-xs text-secondary">These versions are sent as they are now. Files you upload later do not change a submitted application.</p>
    </section>
  );
}

export function ProgrammeTab({ myApp, onApplied }) {
  const setup = useSetup();
  const policy = setup.policy;
  const { can } = useSession();
  const elig = useApiQuery("/internship/me/eligibility");
  const batches = useApiQuery("/internship/me/open-batches");
  const { run, busy } = useAction();
  const [batch, setBatch] = useState(null);
  const [statement, setStatement] = useState("");
  const [prefs, setPrefs] = useState([]);
  const [error, setError] = useState("");
  // Fetched when the apply form opens, so it reflects uploads made since the page loaded.
  const reqs = useApiQuery(batch ? "/me/requirements" : null);
  const checklist = reqs.data?.find((m) => m.module === "internship") || (reqs.data ? { rows: [], ready: true, missing: [] } : null);

  const eligible = Boolean(elig.data?.eligible);
  const blockedByLive = myApp && !["Rejected", "Withdrawn"].includes(myApp.status);

  const togglePref = (id) => setPrefs((p) => (p.includes(id) ? p.filter((x) => x !== id) : p.length < policy.maxPreferredDepartments ? [...p, id] : p));

  const submit = async () => {
    setError("");
    try {
      const next = await run(() => api.post("/internship/me/apply", { batchId: batch.id, statement, preferredDepartmentIds: prefs }), "Application submitted", {
        silentError: true,
      });
      setBatch(null);
      setStatement("");
      setPrefs([]);
      elig.reload();
      batches.reload();
      onApplied?.(next);
    } catch (e) {
      setError(e.message);
    }
  };

  return (
    <div className="space-y-5">
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard
          label="Monthly allowance"
          value={formatCurrency(policy.monthlyAllowance)}
          hint="Excluding VAT and tax"
          icon={<Wallet className="h-5 w-5" />}
          tone="teal"
        />
        <StatCard
          label="Duration"
          value={`${policy.minMonths}–${policy.maxMonths} months`}
          hint="Not renewable (cl.8)"
          icon={<CalendarRange className="h-5 w-5" />}
          tone="blue"
        />
        <StatCard label="Seats per batch" value={policy.batchSize} hint="Selected by test (cl.9)" icon={<Users className="h-5 w-5" />} tone="violet" />
        <StatCard
          label="Attendance needed"
          value={`${policy.minAttendancePct}%`}
          hint="Unapproved absence costs one day's allowance"
          icon={<Percent className="h-5 w-5" />}
          tone="amber"
        />
      </div>

      <div className="grid gap-5 lg:grid-cols-[3fr_2fr]">
        <div className="space-y-5">
          <Card title="Open batches" aside={batches.data ? <Badge tone="blue">{batches.data.length} open</Badge> : null}>
            {batches.error ? <p className="text-danger">{batches.error.message}</p> : null}
            {batches.data?.length ? (
              <div className="space-y-3">
                {batches.data.map((b) => {
                  const taken = b.seats.capacity - b.seats.left;
                  return (
                    <div key={b.id} className="space-y-3 rounded-xl border border-slate-200 p-4 dark:border-slate-700">
                      <div className="flex flex-wrap items-start justify-between gap-3">
                        <div>
                          <p className="text-base font-semibold">
                            {b.name} <span className="text-xs font-normal text-secondary">{b.code}</span>
                          </p>
                          <p className="text-secondary">
                            Starts {formatDate(b.startDate)} · {b.durationMonths} months
                          </p>
                        </div>
                        {can(P.apply) ? (
                          <Button
                            disabled={!eligible || blockedByLive}
                            onClick={() => {
                              setError("");
                              setBatch(b);
                            }}
                          >
                            {blockedByLive ? "Application in progress" : "Apply"}
                          </Button>
                        ) : null}
                      </div>
                      <div className="grid gap-3 sm:grid-cols-2">
                        <div>
                          <p className="text-[11px] font-semibold uppercase tracking-[0.08em] text-slate-500">Applications</p>
                          <p className="mt-1 font-medium">
                            {formatDate(b.applyFrom)} – {formatDate(b.applyTo)}
                          </p>
                        </div>
                        <div>
                          <p className="text-[11px] font-semibold uppercase tracking-[0.08em] text-slate-500">Seats</p>
                          <p className="mt-1 font-medium">
                            {b.seats.left} of {b.seats.capacity} open
                          </p>
                          <Progress value={b.seats.capacity ? (taken * 100) / b.seats.capacity : 0} className="mt-1" />
                        </div>
                      </div>
                      {b.note ? <p className="text-xs text-secondary">{b.note}</p> : null}
                    </div>
                  );
                })}
                {can(P.apply) && !eligible && elig.data ? <p className="text-xs text-danger">You can apply once every eligibility condition is met.</p> : null}
              </div>
            ) : batches.data ? (
              <EmptyState title="No batch is open right now" description="UGC announces each batch with an application window. Check back later." />
            ) : (
              <p className="text-secondary">Loading…</p>
            )}
          </Card>

          <Card title="How it works" description="UGC takes interns into its own departments under the UGC Internship Policy.">
            <ol className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
              {JOURNEY.map(([title, text], i) => (
                <li key={title} className="flex gap-3 rounded-xl bg-white/60 p-3 dark:bg-nexus-800/60">
                  <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-nexus-600 text-xs font-semibold text-white">{i + 1}</span>
                  <span>
                    <span className="block font-semibold">{title}</span>
                    <span className="text-xs text-secondary">{text}</span>
                  </span>
                </li>
              ))}
            </ol>
            <p className="text-xs text-secondary">Certificate grades: {setup.gradeBands.map((g) => g.grade).join(" · ")}.</p>
          </Card>
        </div>

        <Card
          title="Your eligibility"
          description="Checked against cl.7 of the policy and your profile."
          aside={elig.data ? <Badge tone={eligible ? "green" : "red"}>{eligible ? "Eligible" : "Not eligible"}</Badge> : null}
          className="h-fit lg:sticky lg:top-24"
        >
          {elig.error ? <p className="text-danger">{elig.error.message}</p> : null}
          {elig.loading && !elig.data ? <p className="text-secondary">Checking…</p> : null}
          <ul className="space-y-3">
            {(elig.data?.rules || []).map((r) => (
              <li key={r.id} className="flex items-start gap-3">
                {r.met ? (
                  <CircleCheck className="mt-0.5 h-4 w-4 shrink-0 text-success" aria-label="Met" />
                ) : (
                  <CircleX className={`mt-0.5 h-4 w-4 shrink-0 ${r.group ? "text-secondary" : "text-danger"}`} aria-label="Not met" />
                )}
                <span>
                  <span className="font-medium">{r.label}</span> <span className="text-xs text-secondary">({r.clause})</span>
                  <span className="block text-xs text-secondary">{r.detail}</span>
                  {r.id === "documents" && !r.met ? (
                    <Link href="/account/documents" className="text-xs font-medium text-nexus-700 hover:underline dark:text-nexus-300">
                      Upload in My documents →
                    </Link>
                  ) : null}
                </span>
              </li>
            ))}
          </ul>
          <p className="text-xs text-secondary">
            You need <strong>one</strong> of the two academic conditions, plus every other row.{" "}
            {can(P.apply) ? (
              <Link href="/account/profile" className="font-medium text-nexus-700 hover:underline dark:text-nexus-300">
                Update your academic details
              </Link>
            ) : (
              "Your academic details are kept by the UGC administrator."
            )}
          </p>
        </Card>
      </div>

      <Modal
        open={Boolean(batch)}
        onClose={() => setBatch(null)}
        title={`Apply to ${batch?.name || ""}`}
        description="UGC reviews every application. Selection is by test."
        size="lg"
      >
        <div className="space-y-4">
          <Textarea
            label={
              <>
                Statement of purpose <span className="text-xs font-normal text-secondary">(cl.13)</span>
              </>
            }
            value={statement}
            onChange={(e) => setStatement(e.target.value)}
            rows={4}
            hint={`At least ${policy.minStatementLength} characters. Administration reads this to give you work that fits your studies and background.`}
            required
          />
          <fieldset>
            <legend className="mb-2 text-sm font-medium">
              Preferred departments (up to {policy.maxPreferredDepartments}) <span className="text-xs font-normal text-secondary">(cl.12)</span>
            </legend>
            <div className="grid gap-2 sm:grid-cols-2">
              {setup.departments.map((d) => (
                <Checkbox key={d.id} label={d.name} checked={prefs.includes(d.id)} onChange={() => togglePref(d.id)} />
              ))}
            </div>
          </fieldset>
          <ApplyDocuments checklist={checklist} error={reqs.error} />
          {error ? (
            <p role="alert" className="text-sm text-danger">
              {error}
            </p>
          ) : null}
          <div className="flex justify-end gap-2">
            <Button variant="ghost" onClick={() => setBatch(null)}>
              Cancel
            </Button>
            <Button onClick={submit} disabled={busy || !checklist?.ready}>
              Submit application
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
