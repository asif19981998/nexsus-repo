"use client";

import { useEffect, useId, useMemo, useRef, useState } from "react";
import { Check, ChevronDown } from "lucide-react";
import { cn } from "@/lib/cn";

/**
 * A dropdown you can also type in. options: [{ value, label, sub?, search? }].
 * Typing filters by label, sub and `search`; picking an option calls onChange(value).
 */
export function SearchSelect({ label, value, onChange, options, placeholder = "Search…", emptyText = "No match", className, id }) {
  const autoId = useId();
  const inputId = id || autoId;
  const selected = options.find((o) => o.value === value);
  const [open, setOpen] = useState(false);
  const [text, setText] = useState("");
  const [active, setActive] = useState(0);
  const ref = useRef(null);
  const listRef = useRef(null);

  const shown = useMemo(() => {
    const q = text.trim().toLowerCase();
    if (!q) return options;
    return options.filter((o) => `${o.label} ${o.sub || ""} ${o.search || ""}`.toLowerCase().includes(q));
  }, [options, text]);

  useEffect(() => {
    if (!open) return undefined;
    const onDoc = (e) => {
      if (!ref.current?.contains(e.target)) setOpen(false);
    };
    document.addEventListener("mousedown", onDoc);
    return () => document.removeEventListener("mousedown", onDoc);
  }, [open]);

  useEffect(() => {
    listRef.current?.children[active]?.scrollIntoView({ block: "nearest" });
  }, [active, open]);

  const choose = (o) => {
    onChange(o.value);
    setText("");
    setOpen(false);
  };

  const onKeyDown = (e) => {
    if (e.key === "ArrowDown") {
      e.preventDefault();
      setOpen(true);
      setActive((i) => Math.min(i + 1, shown.length - 1));
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setActive((i) => Math.max(i - 1, 0));
    } else if (e.key === "Enter" && open && shown[active]) {
      e.preventDefault();
      choose(shown[active]);
    } else if (e.key === "Escape") {
      setOpen(false);
      setText("");
    }
  };

  return (
    <div className={cn("relative space-y-1.5", className)} ref={ref}>
      {label ? (
        <label htmlFor={inputId} className="block text-sm font-medium text-slate-800 dark:text-slate-200">
          {label}
        </label>
      ) : null}
      <div className="relative">
        <input
          id={inputId}
          role="combobox"
          aria-controls={`${inputId}-list`}
          aria-expanded={open}
          aria-autocomplete="list"
          autoComplete="off"
          value={open ? text : selected?.label || ""}
          placeholder={open && selected ? selected.label : placeholder}
          onFocus={(e) => {
            setOpen(true);
            setActive(0);
            e.target.select();
          }}
          onChange={(e) => {
            setText(e.target.value);
            setActive(0);
            setOpen(true);
          }}
          onKeyDown={onKeyDown}
          className="h-10 w-full rounded-lg border border-[#d5e3df] bg-cream pr-9 pl-3 text-sm text-nexus-900 placeholder:text-slate-400 focus:border-nexus-600 focus:outline-none focus:ring-2 focus:ring-nexus-600/20 dark:border-nexus-700 dark:bg-nexus-900 dark:text-cream"
        />
        <ChevronDown
          className={cn("pointer-events-none absolute top-1/2 right-3 h-4 w-4 -translate-y-1/2 text-slate-400 transition-transform", open && "rotate-180")}
          aria-hidden
        />
      </div>
      {open ? (
        <ul
          id={`${inputId}-list`}
          ref={listRef}
          role="listbox"
          className="motion-pop absolute z-50 mt-1 max-h-64 w-full overflow-auto rounded-xl border border-[#d5e3df] bg-cream py-1 shadow-xl dark:border-nexus-700 dark:bg-nexus-900"
        >
          {shown.length ? (
            shown.map((o, i) => (
              <li key={o.value} role="option" aria-selected={o.value === value}>
                <button
                  type="button"
                  onMouseEnter={() => setActive(i)}
                  onClick={() => choose(o)}
                  className={cn(
                    "flex w-full items-center justify-between gap-2 px-3 py-2 text-left text-sm",
                    i === active ? "bg-ocean text-white dark:bg-sky dark:text-nexus-950" : "text-nexus-800 dark:text-nexus-200"
                  )}
                >
                  <span className="min-w-0">
                    <span className="block truncate font-medium">{o.label}</span>
                    {o.sub ? <span className={cn("block truncate text-xs", i === active ? "opacity-80" : "text-secondary")}>{o.sub}</span> : null}
                  </span>
                  {o.value === value ? <Check className="h-4 w-4 shrink-0" /> : null}
                </button>
              </li>
            ))
          ) : (
            <li className="px-3 py-3 text-sm text-secondary">{emptyText}</li>
          )}
        </ul>
      ) : null}
    </div>
  );
}
