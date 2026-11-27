"use client";

import Link from "next/link";
import { CircleCheck, CircleX } from "lucide-react";
import { Badge } from "@/components/ui";
import { formatDate } from "@/lib/formatters";

/** A titled card, the same frame the internship screens use. */
export function Card({ title, description, aside, children, className = "" }) {
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

export function Fact({ label, children }) {
  return (
    <div>
      <dt className="text-xs text-secondary">{label}</dt>
      <dd className="font-medium break-words">{children ?? "—"}</dd>
    </div>
  );
}

/** Eligibility rows from the API: each with its clause, a tick or cross, and a detail line. */
export function RuleList({ rules }) {
  return (
    <ul className="space-y-3">
      {(rules || []).map((r) => (
        <li key={r.id} className="flex items-start gap-3">
          {r.met ? (
            <CircleCheck className="mt-0.5 h-4 w-4 shrink-0 text-success" aria-label="Met" />
          ) : (
            <CircleX className="mt-0.5 h-4 w-4 shrink-0 text-danger" aria-label="Not met" />
          )}
          <span>
            <span className="font-medium">{r.label}</span> <span className="text-xs text-secondary">({r.clause})</span>
            <span className="block text-xs text-secondary">{r.detail}</span>
            {r.id === "documents" && !r.met ? (
              <Link href="/account/documents" className="text-xs font-medium text-nexus-700 hover:underline dark:text-nexus-300">
                Upload in My documents →
              </Link>
            ) : null}
            {["year", "public", "university", "designation"].includes(r.id) && !r.met ? (
              <Link href="/account/profile" className="block text-xs font-medium text-nexus-700 hover:underline dark:text-nexus-300">
                Update My profile →
              </Link>
            ) : null}
          </span>
        </li>
      ))}
    </ul>
  );
}

export function ErrorText({ children }) {
  return children ? (
    <p role="alert" className="rounded-lg bg-red-50 p-2 text-sm text-danger">
      {children}
    </p>
  ) : null;
}

export const period = (from, to) => `${formatDate(from)} – ${formatDate(to)}`;

export function Pct({ value, min }) {
  return <Badge tone={value >= min ? "green" : "red"}>{value}%</Badge>;
}

/** "2026-11-25" for a date input. */
export function today() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}
