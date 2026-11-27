"use client";

import { useState } from "react";
import { ShieldAlert, ShieldCheck } from "lucide-react";
import { Badge, Button, EmptyState, ErrorState, Modal, Textarea } from "@/components/ui";
import { useAction, useApiQuery, useSession } from "@/hooks/useApi";
import { useOpenDocument } from "@/hooks/useOpenDocument";
import { api } from "@/lib/api";
import { fileSizeLabel, formatDate } from "@/lib/formatters";

const CATEGORIES = ["Identity", "Academic", "Career", "Other"];
const STATUS = {
  Submitted: { tone: "amber", label: "To review" },
  Verified: { tone: "green", label: "Verified" },
  Rejected: { tone: "red", label: "Rejected" },
};

/**
 * A user's document library, for the administrator: open each file, verify it, or send it back with a reason.
 * Verifying an identity document (NID, birth registration, passport) also marks the student's identity verified.
 */
export function UserDocumentsModal({ user, manage, onClose, onChanged }) {
  const { user: me } = useSession();
  const docs = useApiQuery(`/users/${user.id}/documents`);
  const detail = useApiQuery(`/users/${user.id}`);
  const { run, busy } = useAction();
  const viewer = useOpenDocument();
  const [rejecting, setRejecting] = useState(null); // document id
  const [note, setNote] = useState("");

  const own = me?.id === user.id;
  const canReview = manage && !own;
  const identity = detail.data?.student ? detail.data.student.identityVerified : null;

  const refresh = () => {
    docs.reload();
    detail.reload();
    onChanged?.();
  };

  const verify = async (d) => {
    if (await run(() => api.post(`/documents/${d.id}/verify`), `${d.typeName} verified`)) refresh();
  };

  const reject = async (d) => {
    if (await run(() => api.post(`/documents/${d.id}/reject`, { note }), `${d.typeName} sent back`)) {
      setRejecting(null);
      setNote("");
      refresh();
    }
  };

  const list = docs.data || [];
  return (
    <Modal open onClose={onClose} title={`Documents — ${user.fullName}`} description={user.email} size="xl">
      <div className="space-y-4">
        {identity !== null ? (
          <div className="flex flex-wrap items-center gap-2 rounded-lg border border-[#d5e3df] p-3 text-sm dark:border-nexus-800">
            {identity ? <ShieldCheck className="h-4 w-4 text-success" /> : <ShieldAlert className="h-4 w-4 text-opportunity-dark" />}
            <span className="font-medium">Identity {identity ? "verified" : "not verified"}</span>
            <span className="text-xs text-secondary">
              {identity
                ? "Every programme accepts this person's identity."
                : "Verify a National ID, birth registration or passport below to verify the identity."}
            </span>
          </div>
        ) : null}
        {own ? <p className="text-xs text-secondary">These are your own documents. Another administrator reviews them.</p> : null}

        {docs.error ? (
          <ErrorState title="Could not load the documents" description={docs.error.message} action={<Button onClick={docs.reload}>Try again</Button>} />
        ) : docs.data && !list.length ? (
          <EmptyState title="No documents uploaded" description="This person has not uploaded anything to their library yet." />
        ) : (
          CATEGORIES.map((category) => {
            const rows = list.filter((d) => d.category === category);
            if (!rows.length) return null;
            return (
              <section key={category} className="space-y-2">
                <h3 className="text-xs font-semibold tracking-[0.14em] text-nexus-500 uppercase">{category}</h3>
                {rows.map((d) => {
                  const s = STATUS[d.status] || { tone: "slate", label: d.status };
                  return (
                    <div key={d.id} className="space-y-2 rounded-lg border border-[#d5e3df] p-3 dark:border-nexus-800">
                      <div className="flex flex-wrap items-center gap-3">
                        <div className="min-w-0 flex-1">
                          <p className="flex flex-wrap items-center gap-2 text-sm font-semibold">
                            {d.typeName} <Badge tone={s.tone}>{s.label}</Badge>
                          </p>
                          <p className="truncate text-xs text-secondary">
                            {d.fileName} · {fileSizeLabel(d.sizeBytes)} · version {d.version} · uploaded {formatDate(d.uploadedAt)}
                          </p>
                          {d.reviewedAt ? (
                            <p className="text-xs text-secondary">
                              {d.status === "Verified" ? "Verified" : "Reviewed"} by {d.reviewedBy || "—"} on {formatDate(d.reviewedAt)}
                              {d.status === "Rejected" && d.reviewNote ? <span className="text-danger"> · {d.reviewNote}</span> : null}
                            </p>
                          ) : null}
                        </div>
                        <div className="flex flex-wrap gap-1.5">
                          <Button size="sm" variant="outline" disabled={viewer.busy} onClick={() => viewer.open(d)}>
                            View
                          </Button>
                          {canReview && d.status !== "Verified" ? (
                            <Button size="sm" disabled={busy} onClick={() => verify(d)}>
                              Verify
                            </Button>
                          ) : null}
                          {canReview && d.status !== "Rejected" ? (
                            <Button
                              size="sm"
                              variant="ghost"
                              disabled={busy}
                              onClick={() => {
                                setRejecting(d.id);
                                setNote("");
                              }}
                            >
                              Reject
                            </Button>
                          ) : null}
                        </div>
                      </div>
                      {rejecting === d.id ? (
                        <div className="space-y-2">
                          <Textarea
                            label="What is wrong?"
                            value={note}
                            onChange={(e) => setNote(e.target.value)}
                            rows={2}
                            maxLength={500}
                            placeholder="e.g. The scan is cut off at the bottom. Upload the full page."
                          />
                          <div className="flex justify-end gap-2">
                            <Button size="sm" variant="ghost" onClick={() => setRejecting(null)}>
                              Cancel
                            </Button>
                            <Button size="sm" variant="danger" disabled={busy || !note.trim()} onClick={() => reject(d)}>
                              Send back
                            </Button>
                          </div>
                        </div>
                      ) : null}
                    </div>
                  );
                })}
              </section>
            );
          })
        )}
      </div>
    </Modal>
  );
}
