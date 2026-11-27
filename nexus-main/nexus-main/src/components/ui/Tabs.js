"use client";

import { createContext, useContext, useState } from "react";
import { cn } from "@/lib/cn";

const TabsContext = createContext(null);

export function Tabs({ defaultValue, value: controlled, onChange, children, className }) {
  const [internal, setInternal] = useState(defaultValue);
  const value = controlled ?? internal;
  const setValue = onChange ?? setInternal;
  return (
    <TabsContext.Provider value={{ value, setValue }}>
      <div className={className}>{children}</div>
    </TabsContext.Provider>
  );
}

const TabListContext = createContext("pills");

/**
 * The row of tabs. "pills" (default) is a segmented control: the chosen tab is a solid ocean (sky in dark mode)
 * button, so the row reads as a menu at a glance. "line" is for a second row of tabs inside a tab: underlined, so
 * the two levels don't look alike.
 */
export function TabList({ children, className, variant = "pills" }) {
  return (
    <TabListContext.Provider value={variant}>
      <div
        role="tablist"
        className={cn(
          variant === "line"
            ? "flex flex-wrap gap-1 border-b border-slate-200 dark:border-nexus-700"
            : "flex w-fit max-w-full flex-wrap gap-1 rounded-xl border border-slate-200 bg-slate-100 p-1 dark:border-nexus-700 dark:bg-nexus-950",
          className
        )}
      >
        {children}
      </div>
    </TabListContext.Provider>
  );
}

export function Tab({ value, children, className }) {
  const ctx = useContext(TabsContext);
  const variant = useContext(TabListContext);
  const selected = ctx.value === value;
  return (
    <button
      type="button"
      role="tab"
      aria-selected={selected}
      onClick={() => ctx.setValue(value)}
      className={cn(
        "text-sm font-medium transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ocean dark:focus-visible:outline-sky",
        variant === "line"
          ? cn(
              "-mb-px rounded-t-lg border-b-[3px] px-4 py-2.5",
              selected
                ? "border-ocean bg-ocean/[0.06] font-semibold text-ocean dark:border-sky dark:bg-sky/10 dark:text-sky"
                : "border-transparent text-slate-600 hover:border-slate-300 hover:bg-slate-100 hover:text-slate-900 dark:text-slate-300 dark:hover:border-nexus-600 dark:hover:bg-nexus-800 dark:hover:text-white"
            )
          : cn(
              "rounded-lg px-4 py-2",
              selected
                ? "bg-ocean font-semibold text-white shadow-sm dark:bg-sky dark:text-nexus-950"
                : "text-slate-600 hover:bg-white hover:text-slate-900 dark:text-slate-300 dark:hover:bg-nexus-800 dark:hover:text-white"
            ),
        className
      )}
    >
      {children}
    </button>
  );
}

export function TabPanel({ value, children, className }) {
  const ctx = useContext(TabsContext);
  if (ctx.value !== value) return null;
  return (
    <div role="tabpanel" className={cn("motion-panel pt-4", className)}>
      {children}
    </div>
  );
}

export function Accordion({ items = [], className }) {
  const [open, setOpen] = useState(items[0]?.id ?? null);
  return (
    <div className={cn("space-y-2", className)}>
      {items.map((item) => {
        const isOpen = open === item.id;
        return (
          <div key={item.id} className={cn("rounded-xl border border-slate-200 transition-[border-color,box-shadow] duration-200 dark:border-slate-700", isOpen ? "border-nexus-300 shadow-[0_4px_14px_rgba(51,104,160,0.08)] dark:border-nexus-600" : "hover:border-nexus-300 dark:hover:border-nexus-600")}>
            <button
              type="button"
              className="flex w-full items-center justify-between rounded-xl px-4 py-3 text-left text-sm font-medium hover:text-nexus-700 dark:hover:text-nexus-300"
              aria-expanded={isOpen}
              onClick={() => setOpen(isOpen ? null : item.id)}
            >
              {item.title}
              <span aria-hidden className={cn("text-lg leading-none text-slate-400 transition-transform duration-300", isOpen && "rotate-45 text-nexus-600")}>+</span>
            </button>
            {isOpen ? <div className="motion-panel border-t border-slate-200 px-4 py-3 text-sm text-secondary dark:border-slate-700">{item.content}</div> : null}
          </div>
        );
      })}
    </div>
  );
}
