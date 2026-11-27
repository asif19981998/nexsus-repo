"use client";

import { useState } from "react";
import Link from "next/link";
import { Badge, Button, DataTable, EmptyState, ErrorState, PageHeader, StatusBadge, Textarea, buttonClass } from "@/components/ui";
import { LuxuryLoader } from "@/components/brand/LuxuryLoader";
import { NoAccess } from "@/components/layout/InternshipShell";
import { useAction, useApiQuery, useSession } from "@/hooks/useApi";
import { api } from "@/lib/api";
import { SP, TRAINER_TONE } from "@/lib/softskillPermissions";
import { formatCurrency, formatDate } from "@/lib/formatters";
import { Card, ErrorText, Fact, RuleList, period } from "../_components/ui";
import { HonorariumTable, honorariumTotals } from "../_components/oversight";

/**
 * A university teacher's Soft Skill page: the trainer registration (cl.12), then the university groups they coordinate or train
 * and their coming sessions.
 */
export default function TrainerPage() {
  const { can, user } = useSession();
  const mine = useApiQuery(can(SP.trainerApply) ? "/softskill/trainers/me" : null);
  const honoraria = useApiQuery(can(SP.trainerApply) ? "/softskill/honoraria/me" : null);
  const { run, busy } = useAction();
  const [statement, setStatement] = useState("");
  const [error, setError] = useState("");

  if (!can(SP.trainerApply)) return <NoAccess what="the trainer page" />;
  if (mine.error) return <ErrorState title="Could not load your trainer page" description={mine.error.message} action={<Button onClick={mine.reload}>Try again</Button>} />;
  if (!mine.data) return <LuxuryLoader variant="compact" label="Loading your trainer page" />;

  const { profile, eligibility, registration: reg, universityGroups, upcomingSessions } = mine.data;
  const canApply = eligibility.eligible;

  const apply = async () => {
    setError("");
    try {
      const next = await run(() => api.post("/softskill/trainers/me/apply", { statement }), "Registration sent to the Commission", { silentError: true });
      mine.setData(next);
      setStatement("");
    } catch (e) {
      setError(e.message);
    }
  };

  return (
    <div className="space-y-4">
      <PageHeader title="My training work" description="Register as a Soft Skill trainer, then teach and coordinate your university's groups." />

      <div className="grid gap-5 lg:grid-cols-[3fr_2fr]">
        <div className="space-y-5">
          <Card
            title="Trainer registration"
            description="The Commission approves trainers and records the Training of Trainers (cl.12 খ)."
            aside={reg ? <Badge tone={TRAINER_TONE[reg.status]}>{reg.status}</Badge> : null}
          >
            {reg ? (
              <dl className="grid gap-3 sm:grid-cols-2">
                <Fact label="University">{reg.university.name}</Fact>
                <Fact label="Applied">{formatDate(reg.appliedAt)}</Fact>
                <Fact label="ToT">{reg.totCompleted ? `Completed ${formatDate(reg.totCompletedOn)}${reg.totReference ? ` · ${reg.totReference}` : ""}` : "Not recorded yet"}</Fact>
                <Fact label="Can take sessions">{reg.canTrain ? "Yes" : "Not yet"}</Fact>
                {reg.decisionNote ? <Fact label="Commission's note">{reg.decisionNote}</Fact> : null}
              </dl>
            ) : (
              <p className="text-secondary">You have not registered yet.</p>
            )}
            {reg?.status === "Approved" && !reg.totCompleted ? (
              <p className="rounded-lg bg-[#f7f1e6] p-3 text-xs">
                Approved. You can be given sessions once the Commission records your Training of Trainers. Upload the ToT certificate in{" "}
                <Link href="/account/documents" className="font-medium underline">
                  My documents
                </Link>
                .
              </p>
            ) : null}
            {canApply ? (
              <div className="space-y-3 border-t border-slate-200 pt-4 dark:border-slate-700">
                <Textarea
                  label={reg ? "Apply again" : "Your teaching experience"}
                  value={statement}
                  onChange={(e) => setStatement(e.target.value)}
                  rows={4}
                  hint="At least 30 characters. Your documents from My documents go with it."
                />
                <ErrorText>{error}</ErrorText>
                <Button onClick={apply} disabled={busy}>
                  Send registration
                </Button>
              </div>
            ) : null}
          </Card>

          <Card title="My university groups" description="University groups you coordinate or have sessions in.">
            {universityGroups.length ? (
              <DataTable
                rows={universityGroups}
                columns={[
                  {
                    key: "batch",
                    label: "Batch",
                    render: (c) => (
                      <div>
                        <p className="font-medium">{c.batchName}</p>
                        <p className="text-xs text-secondary">
                          {c.batchCode} · {period(c.startDate, c.endDate)}
                        </p>
                      </div>
                    ),
                  },
                  { key: "status", label: "Status", render: (c) => <StatusBadge status={c.batchStatus} /> },
                  { key: "role", label: "My role", render: (c) => (c.coordinator?.id === user?.id ? <Badge tone="violet">Coordinator</Badge> : <Badge tone="blue">Trainer</Badge>) },
                  { key: "seats", label: "Trainees", render: (c) => `${c.taken}/${c.capacity}` },
                  {
                    key: "open",
                    label: "",
                    render: (c) => (
                      <Link href={`/softskill/university-groups/${c.id}`} className={buttonClass({ size: "sm", variant: "outline" })}>
                        Open
                      </Link>
                    ),
                  },
                ]}
              />
            ) : (
              <EmptyState title="No university group yet" description="The Commission names a coordinator per university; coordinators assign sessions to approved trainers." />
            )}
          </Card>
        </div>

        <div className="space-y-5">
          <Card title="Your eligibility" aside={<Badge tone={eligibility.eligible || reg ? "green" : "amber"}>{reg ? "Registered" : eligibility.eligible ? "Ready" : "Not yet"}</Badge>}>
            <RuleList rules={eligibility.rules} />
            <dl className="grid gap-3 border-t border-slate-200 pt-3 sm:grid-cols-2 dark:border-slate-700">
              <Fact label="University">{profile.university}</Fact>
              <Fact label="Designation">{profile.designation}</Fact>
            </dl>
          </Card>
          <Card title="Coming sessions">
            {upcomingSessions.length ? (
              <ul className="space-y-2">
                {upcomingSessions.map((s) => (
                  <li key={s.id} className="rounded-lg border border-slate-200 p-3 dark:border-slate-700">
                    <p className="font-medium">{s.topic}</p>
                    <p className="text-xs text-secondary">
                      {formatDate(s.date)} · {s.startTime}–{s.endTime}
                    </p>
                    <Link href={`/softskill/university-groups/${s.universityGroupId}`} className="text-xs font-medium text-nexus-700 hover:underline dark:text-nexus-300">
                      Open university group →
                    </Link>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="text-secondary">No session scheduled for you.</p>
            )}
          </Card>
          <Card
            title="My honorarium"
            description={`Per session held, and per university group coordinated once the final report is sent (cl.17). Paid ${formatCurrency(honorariumTotals(honoraria.data).paid)}.`}
          >
            <HonorariumTable rows={honoraria.data} showPerson={false} emptyMessage="Appears once your sessions are held." />
          </Card>
        </div>
      </div>
    </div>
  );
}
