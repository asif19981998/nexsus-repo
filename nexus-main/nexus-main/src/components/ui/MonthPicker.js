"use client";

import { useEffect, useId, useRef, useState } from "react";
import { CalendarDays, ChevronLeft, ChevronRight } from "lucide-react";
import { cn } from "@/lib/cn";

const MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
const FULL = [
  "January", "February", "March", "April", "May", "June",
  "July", "August", "September", "October", "November", "December",
];

const parse = (v) => {
  const m = typeof v === "string" && v.match(/^(\d{4})-(\d{2})$/);
  return m ? { year: Number(m[1]), month: Number(m[2]) - 1 } : null;
};
const key = (year, month) => `${year}-${String(month + 1).padStart(2, "0")}`;

/**
 * A month + year picker styled like DateInput. `value`/`min`/`max` are "yyyy-MM"; onChange gets an input-like event.
 * Optional fields (not `required`) offer "Clear", which sends an empty value.
 */
export function MonthInput({
  label,
  value = "",
  onChange,
  name,
  id,
  min,
  max,
  className,
  placeholder = "Select month",
  disabled = false,
  required = false,
  error,
  hint,
}) {
  const autoId = useId();
  const inputId = id || name || autoId;
  const selected = parse(value);
  const minV = parse(min);
  const maxV = parse(max);
  const now = new Date();
  const [open, setOpen] = useState(false);
  const [year, setYear] = useState(selected?.year ?? now.getFullYear());
  const ref = useRef(null);

  useEffect(() => {
    if (!open) return undefined;
    const onDoc = (e) => {
      if (!ref.current?.contains(e.target)) setOpen(false);
    };
    const onKey = (e) => e.key === "Escape" && setOpen(false);
    document.addEventListener("mousedown", onDoc);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onDoc);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  const outside = (y, m) => {
    const k = key(y, m);
    return (min && k < min) || (max && k > max);
  };
  const send = (v) => {
    onChange?.({ target: { name, value: v }, currentTarget: { name, value: v } });
    setOpen(false);
  };
  const pick = (m) => send(key(year, m));
  const canPrev = !minV || year > minV.year;
  const canNext = !maxV || year < maxV.year;
  const arrow = "inline-flex h-8 w-8 items-center justify-center rounded-lg text-ocean hover:bg-ocean hover:text-white disabled:cursor-not-allowed disabled:opacity-30 disabled:hover:bg-transparent disabled:hover:text-ocean dark:text-sky dark:hover:bg-sky dark:hover:text-nexus-950";

  return (
    <div className={cn("relative space-y-1.5", className)} ref={ref}>
      {label ? (
        <label htmlFor={inputId} className="block text-sm font-medium text-slate-800 dark:text-slate-200">
          {label}
          {required ? <span className="ml-0.5 text-danger">*</span> : null}
        </label>
      ) : null}
      <button
        id={inputId}
        type="button"
        disabled={disabled}
        aria-haspopup="dialog"
        aria-expanded={open}
        aria-describedby={error ? `${inputId}-error` : hint ? `${inputId}-hint` : undefined}
        onClick={() => {
          setYear(selected?.year ?? now.getFullYear());
          setOpen((v) => !v);
        }}
        className={cn(
          "flex h-10 w-full items-center justify-between gap-2 rounded-lg border border-[#d5e3df] bg-cream px-3 text-left text-sm text-nexus-900 focus:border-nexus-600 focus:outline-none focus:ring-2 focus:ring-nexus-600/20 disabled:cursor-not-allowed disabled:opacity-60 dark:border-nexus-700 dark:bg-nexus-900 dark:text-cream",
          error && "border-danger focus:border-danger focus:ring-danger/20"
        )}
      >
        <span className={cn("truncate", !selected && "text-slate-400")}>
          {selected ? `${FULL[selected.month]} ${selected.year}` : placeholder}
        </span>
        <CalendarDays className="h-4 w-4 shrink-0 text-slate-400" aria-hidden />
      </button>

      {open ? (
        <div
          role="dialog"
          aria-label={label || "Choose month"}
          className="motion-pop absolute z-50 mt-2 w-[272px] rounded-xl border border-[#d5e3df] bg-cream p-3 shadow-xl dark:border-nexus-700 dark:bg-nexus-900"
        >
          <div className="mb-3 flex items-center justify-between">
            <button type="button" aria-label="Previous year" className={arrow} disabled={!canPrev} onClick={() => setYear(year - 1)}>
              <ChevronLeft className="h-4 w-4" />
            </button>
            <span className="text-sm font-semibold text-nexus-900 dark:text-cream">{year}</span>
            <button type="button" aria-label="Next year" className={arrow} disabled={!canNext} onClick={() => setYear(year + 1)}>
              <ChevronRight className="h-4 w-4" />
            </button>
          </div>
          <div className="grid grid-cols-3 gap-2">
            {MONTHS.map((label, m) => {
              const off = outside(year, m);
              const isSel = selected && selected.year === year && selected.month === m;
              const isNow = now.getFullYear() === year && now.getMonth() === m;
              return (
                <button
                  key={label}
                  type="button"
                  disabled={off}
                  onClick={() => pick(m)}
                  className={cn(
                    "h-10 rounded-lg text-sm transition-colors",
                    off && "cursor-not-allowed text-slate-300 dark:text-slate-600",
                    !off && !isSel && "text-nexus-800 hover:bg-ocean hover:text-white dark:text-nexus-200 dark:hover:bg-sky dark:hover:text-nexus-950",
                    isNow && !isSel && !off && "ring-1 ring-ocean dark:ring-sky",
                    isSel && "bg-ocean font-semibold text-white"
                  )}
                >
                  {label}
                </button>
              );
            })}
          </div>
          {!required && selected ? (
            <div className="mt-3 flex justify-end border-t border-[#d5e3df] pt-2 dark:border-nexus-700">
              <button type="button" className="text-xs font-medium text-ocean hover:underline dark:text-sky" onClick={() => send("")}>
                Clear
              </button>
            </div>
          ) : null}
        </div>
      ) : null}
      {hint && !error ? (
        <p id={`${inputId}-hint`} className="text-xs text-secondary">
          {hint}
        </p>
      ) : null}
      {error ? (
        <p id={`${inputId}-error`} className="text-xs text-danger">
          {error}
        </p>
      ) : null}
    </div>
  );
}
