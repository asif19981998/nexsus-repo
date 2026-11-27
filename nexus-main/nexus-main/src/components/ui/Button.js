"use client";

import { cn } from "@/lib/cn";
import { LuxurySpinner } from "@/components/brand/LuxuryLoader";

const variants = {
  primary:
    "bg-nexus-600 text-white hover:bg-nexus-800 hover:shadow-[0_6px_16px_-6px_rgba(51,104,160,0.55)] focus-visible:outline-nexus-600 disabled:bg-nexus-400",
  secondary:
    "bg-nexus-400 text-white hover:bg-nexus-600 hover:shadow-[0_6px_16px_-6px_rgba(51,104,160,0.55)] focus-visible:outline-nexus-400 disabled:bg-nexus-300",
  outline:
    "border-2 border-nexus-600 text-nexus-700 hover:bg-nexus-600 hover:text-white dark:border-nexus-400 dark:text-nexus-300 dark:hover:bg-nexus-400 dark:hover:text-nexus-950",
  ghost:
    "text-nexus-800 hover:bg-nexus-600 hover:text-white dark:text-nexus-200 dark:hover:bg-nexus-400 dark:hover:text-nexus-950",
  danger: "bg-danger text-white hover:bg-red-900 hover:shadow-[0_6px_16px_-6px_rgba(185,28,28,0.5)]",
  soft: "bg-nexus-600 text-white hover:bg-nexus-800 hover:shadow-[0_6px_16px_-6px_rgba(51,104,160,0.55)]",
  ugc: "bg-ugc text-white hover:bg-ugc-dark hover:shadow-[0_6px_16px_-6px_rgba(51,104,160,0.55)]",
  institutional: "bg-institutional text-white hover:bg-institutional-dark hover:shadow-[0_6px_16px_-6px_rgba(51,104,160,0.55)]",
};

const sizes = {
  sm: "h-8 px-3 text-xs gap-1.5",
  md: "h-10 px-4 text-sm gap-2",
  lg: "h-11 px-5 text-base gap-2",
  icon: "h-10 w-10 p-0",
};

const base =
  "inline-flex cursor-pointer items-center justify-center rounded-lg font-semibold tracking-tight transition-[color,background-color,border-color,box-shadow,transform,scale] duration-200 ease-out not-disabled:active:scale-[0.97] disabled:cursor-not-allowed disabled:opacity-60 disabled:shadow-none";

/** Button look for a link (<Link className={buttonClass(...)}>), so navigation stays a real link. */
export function buttonClass({ variant = "primary", size = "md", className } = {}) {
  return cn(base, variants[variant], sizes[size], className);
}

export function Button({
  className,
  variant = "primary",
  size = "md",
  loading = false,
  disabled,
  children,
  type = "button",
  ...props
}) {
  return (
    <button
      type={type}
      disabled={disabled || loading}
      className={cn(
        base,
        variants[variant],
        sizes[size],
        className
      )}
      {...props}
    >
      {loading ? <LuxurySpinner size={size === "sm" ? 14 : 16} /> : null}
      {children}
    </button>
  );
}

export function IconButton({ className, label, children, ...props }) {
  return (
    <Button
      size="icon"
      variant="ghost"
      className={cn("rounded-full text-nexus-800 dark:text-nexus-100", className)}
      aria-label={label}
      {...props}
    >
      {children}
    </Button>
  );
}
