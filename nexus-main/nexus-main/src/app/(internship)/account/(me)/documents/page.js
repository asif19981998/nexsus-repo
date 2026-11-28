"use client";

import { useMemo, useRef, useState } from "react";
import { FileText, ImageIcon, Upload } from "lucide-react";
import { Badge, Button, ConfirmDialog, EmptyState, ErrorState, Modal, Select, StatCard } from "@/components/ui";
import { LuxuryLoader } from "@/components/brand/LuxuryLoader";
import { useAction, useApiQuery, useSession } from "@/hooks/useApi";
import { isApplicant, isTrainer } from "@/lib/internshipPermissions";
import { useOpenDocument } from "@/hooks/useOpenDocument";
import { api } from "@/lib/api";
import { fileSizeLabel, formatDate } from "@/lib/formatters";
import { cn } from "@/lib/cn";

const CATEGORIES = ["Identity", "Academic", "Career", "Other"];
const STATUS = {
  Submitted: { tone: "amber", label: "Waiting for review" },
  Verified: { tone: "green", label: "Verified" },
  Rejected: { tone: "red", label: "Rejected" },
};

/**
 * Types only one kind of account is asked for. A trainer (a university teacher) does not see the student-only ones,
 * and a student does not see the trainer-only ones, unless one is already uploaded.
 */
const STUDENT_ONLY = new Set(["STUDENT_ID", "STUDENTSHIP", "SSC", "HSC", "TRANSCRIPT", "EQUIVALENCE", "BIRTH", "RECOMMENDATION"]);
const TRAINER_ONLY = new Set(["STAFF_ID", "TOT"]);

/** "PDF, JPG or PNG" from [".pdf", ".jpg", ".jpeg", ".png"]. */
function formats(extensions = []) {
  const names = [...new Set(extensions.map((e) => e.replace(".", "").toUpperCase()).filter((n) => n !== "JPEG"))];
  return names.length <= 1 ? names.join("") : `${names.slice(0, -1).join(", ")} or ${names.at(-1)}`;
}

/**
 * The document library. It belongs to the account, not to a module: the same files serve the UGC Internship now
 * and other programmes later. Uploading a type again replaces the current file with a new version.
 */
export default function MyDocumentsPage() {
  const { can } = useSession();
  if (!isApplicant(can))
    return (
      <EmptyState
        title="No documents needed"
        description="Documents are for people who apply to programmes. Your role does not apply, so there is nothing to upload."
      />
    );
  return <MyDocuments trainer={isTrainer(can)} />;
}

function MyDocuments({ trainer }) {
  const types = useApiQuery("/document-types");
  const docs = useApiQuery("/me/documents");
  const { run, busy } = useAction();
  const viewer = useOpenDocument();
  const requirements = useApiQuery("/me/requirements");
  const [upload, setUpload] = useState(null); // { typeId }
  const [remove, setRemove] = useState(null); // document

  const byType = useMemo(() => Object.fromEntries((docs.data || []).map((d) => [d.documentTypeId, d])), [docs.data]);
  // Document type id → what each programme says about it, e.g. { required: true, programme: "UGC Internship" }. A row
  // with several types (National ID, birth registration or passport) needs only one: once one is uploaded, the others
  // become optional.
  const neededBy = useMemo(() => {
    const map = {};
    for (const m of requirements.data || [])
      for (const row of m.rows)
        for (const t of row.types) {
          let entry;
          if (!row.required) entry = { required: false };
          else if (row.types.length === 1) entry = { required: true };
          else if (row.met)
            entry =
              row.document?.documentTypeId === t.id
                ? { required: true }
                : { required: false, note: `you uploaded ${row.document?.typeName || "another one"}` };
          else entry = { required: true, note: `upload any one of ${row.types.map((x) => x.name).join(", ")}` };
          (map[t.id] ||= []).push({ ...entry, programme: m.moduleName });
        }
    return map;
  }, [requirements.data]);
  // The types offered to this person: each kind of account does not see the other kind's types they have not uploaded.
  const shown = useMemo(
    () => (types.data || []).filter((t) => !(trainer ? STUDENT_ONLY : TRAINER_ONLY).has(t.code) || byType[t.id]),
    [types.data, trainer, byType],
  );
  const reload = () => {
    docs.reload();
    requirements.reload();
  };

  const error = types.error || docs.error;
  if (error)
    return (
      <ErrorState
        title="Could not load your documents"
        description={error.message}
        action={
          <Button
            onClick={() => {
              types.reload();
              docs.reload();
            }}
          >
            Try again
          </Button>
        }
      />
    );
  if (!types.data || !docs.data) return <LuxuryLoader variant="compact" label="Loading your documents" />;

  const list = docs.data;
  const confirmRemove = async () => {
    const ok = await run(() => api.delete(`/me/documents/${remove.id}`), "Document deleted");
    setRemove(null);
    if (ok) reload();
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm text-secondary">
          {trainer
            ? "Upload once. Your Soft Skill trainer registration uses these files, and the Commission checks them."
            : "Upload once. Every programme you apply to uses these files, and the administrator checks them."}
        </p>
        <Button onClick={() => setUpload({ typeId: "" })}>
          <Upload className="h-4 w-4" /> Upload document
        </Button>
      </div>

      <div className="grid gap-3 sm:grid-cols-3">
        <StatCard label="Uploaded" value={list.length} />
        <StatCard label="Verified" value={list.filter((d) => d.status === "Verified").length} />
        <StatCard label="Need a new file" value={list.filter((d) => d.status === "Rejected").length} />
      </div>

      {CATEGORIES.map((category) => {
        const rows = shown.filter((t) => t.category === category);
        if (!rows.length) return null;
        return (
          <section key={category} className="space-y-2">
            <h2 className="text-xs font-semibold tracking-[0.14em] text-nexus-500 uppercase">{category}</h2>
            <div className="grid gap-2">
              {rows.map((t) => (
                <DocumentRow
                  key={t.id}
                  type={t}
                  doc={byType[t.id]}
                  busy={busy}
                  onUpload={() => setUpload({ typeId: String(t.id) })}
                  onView={viewer.open}
                  neededBy={neededBy[t.id]}
                  onDelete={setRemove}
                />
              ))}
            </div>
          </section>
        );
      })}
      {!types.data.length ? <EmptyState title="No document types yet" description="The administrator has not set up the document list." /> : null}

      {upload ? (
        <UploadModal
          types={shown}
          initialTypeId={upload.typeId}
          current={byType}
          onClose={() => setUpload(null)}
          onDone={() => {
            setUpload(null);
            reload();
          }}
        />
      ) : null}

      <ConfirmDialog
        open={Boolean(remove)}
        onClose={() => setRemove(null)}
        onConfirm={confirmRemove}
        loading={busy}
        danger
        title="Delete this document?"
        description={remove ? `${remove.typeName}: ${remove.fileName}. It leaves your library; a copy stays with any past application that used it.` : ""}
        confirmLabel="Delete"
      />
    </div>
  );
}

function DocumentRow({ type, doc, busy, neededBy, onUpload, onView, onDelete }) {
  const Icon = doc?.contentType?.startsWith("image/") ? ImageIcon : FileText;
  const status = doc ? STATUS[doc.status] || { tone: "slate", label: doc.status } : null;
  return (
    <div className="card-surface flex flex-wrap items-center gap-3 p-3">
      <span
        className={cn(
          "flex h-10 w-10 shrink-0 items-center justify-center rounded-lg",
          doc ? "bg-[#e8f3ef] text-success" : "bg-slate-100 text-slate-400 dark:bg-nexus-800",
        )}
      >
        <Icon className="h-5 w-5" />
      </span>
      <div className="min-w-0 flex-1">
        <p className="flex flex-wrap items-center gap-2 text-sm font-semibold">
          {type.name}
          {type.isIdentity ? <Badge tone="blue">Proves identity</Badge> : null}
          {status ? <Badge tone={status.tone}>{status.label}</Badge> : null}
        </p>
        <p className="truncate text-xs text-secondary">
          {doc
            ? `${doc.fileName} · ${fileSizeLabel(doc.sizeBytes)} · version ${doc.version} · ${formatDate(doc.uploadedAt)}`
            : type.description || `${formats(type.extensions)}, up to ${fileSizeLabel(type.maxBytes)}`}
        </p>
        {doc?.status === "Rejected" && doc.reviewNote ? <p className="text-xs text-danger">Reviewer: {doc.reviewNote}</p> : null}
        {neededBy?.length ? (
          <ul className="mt-1 space-y-1">
            {neededBy.map((n) => (
              <ProgrammeNeed key={`${n.programme}-${n.required}-${n.note || ""}`} need={n} />
            ))}
          </ul>
        ) : null}
      </div>
      <div className="flex gap-2">
        {doc ? (
          <>
            <Button size="sm" variant="outline" disabled={busy} onClick={() => onView(doc)}>
              View
            </Button>
            <Button size="sm" variant="outline" disabled={busy} onClick={onUpload}>
              Replace
            </Button>
            <Button size="sm" variant="ghost" disabled={busy} onClick={() => onDelete(doc)}>
              Delete
            </Button>
          </>
        ) : (
          <Button size="sm" disabled={busy} onClick={onUpload}>
            Upload
          </Button>
        )}
      </div>
    </div>
  );
}

/** One programme's need for a document: a red "Required" or amber "Optional" tag, then the programme name. */
function ProgrammeNeed({ need }) {
  return (
    <li className="flex flex-wrap items-center gap-1.5 text-xs">
      <span
        className={cn(
          "rounded px-1.5 py-0.5 font-semibold",
          need.required
            ? "bg-red-50 text-danger dark:bg-red-950/50 dark:text-red-300"
            : "bg-amber-50 text-opportunity-dark dark:bg-amber-950/40 dark:text-amber-300",
        )}
      >
        {need.required ? "Required" : "Optional"}
      </span>
      <span className="text-secondary">for</span>
      <span className="rounded bg-violet-50 px-1.5 py-0.5 font-semibold text-violet-700 dark:bg-violet-950/50 dark:text-violet-300">
        {need.programme}
      </span>
      {need.note ? <span className="text-secondary">: {need.note}</span> : null}
    </li>
  );
}

function UploadModal({ types, initialTypeId, current, onClose, onDone }) {
  const { run, busy } = useAction();
  const [typeId, setTypeId] = useState(initialTypeId);
  const [file, setFile] = useState(null);
  const [error, setError] = useState("");
  const input = useRef(null);

  const type = types.find((t) => String(t.id) === String(typeId));
  const existing = type ? current[type.id] : null;

  const pick = (e) => {
    const f = e.target.files?.[0] || null;
    setError("");
    setFile(f);
    if (f && type && f.size > type.maxBytes) setError(`${type.name} can be at most ${fileSizeLabel(type.maxBytes)}.`);
  };

  const submit = async (e) => {
    e.preventDefault();
    if (!type) return setError("Choose a document type.");
    if (!file) return setError("Choose a file.");
    if (file.size > type.maxBytes) return setError(`${type.name} can be at most ${fileSizeLabel(type.maxBytes)}.`);
    const body = new FormData();
    body.append("documentTypeId", String(type.id));
    body.append("file", file);
    const saved = await run(() => api.upload("/me/documents", body), existing ? "New version uploaded" : "Document uploaded", { silentError: true }).catch(
      (err) => {
        setError(err.message);
        return null;
      },
    );
    if (saved) onDone();
  };

  return (
    <Modal open onClose={onClose} title={existing ? "Replace document" : "Upload document"} size="md">
      <form onSubmit={submit} className="space-y-4" noValidate>
        <Select
          label="Document type"
          value={typeId}
          onChange={(e) => {
            setTypeId(e.target.value);
            setFile(null);
            setError("");
            if (input.current) input.current.value = "";
          }}
          placeholder="Choose a type"
          options={CATEGORIES.flatMap((c) => types.filter((t) => t.category === c).map((t) => ({ value: String(t.id), label: `${c} · ${t.name}` })))}
        />
        {type ? (
          <div className="space-y-1.5">
            <label htmlFor="doc-file" className="block text-sm font-medium text-slate-800 dark:text-slate-200">
              File <span className="text-danger">*</span>
            </label>
            <input
              id="doc-file"
              ref={input}
              type="file"
              accept={type.extensions.join(",")}
              onChange={pick}
              className="block w-full text-sm file:mr-3 file:rounded-lg file:border-0 file:bg-sage file:px-3 file:py-2 file:text-sm file:font-medium file:text-nexus-800 dark:file:bg-nexus-800 dark:file:text-nexus-100"
            />
            <p className="text-xs text-secondary">
              {formats(type.extensions)}, up to {fileSizeLabel(type.maxBytes)}.{type.description ? ` ${type.description}` : ""}
            </p>
            {file ? <p className="text-xs">{`${file.name} · ${fileSizeLabel(file.size)}`}</p> : null}
            {existing ? (
              <p className="rounded-lg bg-[#f7f1e6] p-2 text-xs text-opportunity-dark">
                This replaces version {existing.version} ({existing.fileName}). The new file needs review again.
              </p>
            ) : null}
          </div>
        ) : null}
        {error ? (
          <p role="alert" className="rounded-lg bg-red-50 p-2 text-sm text-danger">
            {error}
          </p>
        ) : null}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={onClose} disabled={busy}>
            Cancel
          </Button>
          <Button type="submit" disabled={busy || !type || !file}>
            {busy ? "Uploading…" : "Upload"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
