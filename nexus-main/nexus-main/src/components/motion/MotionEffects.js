"use client";

import { useEffect, useRef, useState } from "react";
import { usePathname } from "next/navigation";
import { ArrowUp } from "lucide-react";
import { cn } from "@/lib/cn";

/** Blocks that fade up as they scroll into view. Add data-reveal to opt any element in, data-no-reveal to opt a subtree out. */
const REVEAL_SELECTOR = ["section", ".card-surface", ".card-interactive", "[data-reveal]"].map((s) => `main ${s}`).join(", ");
const EASE = "cubic-bezier(0.16, 1, 0.3, 1)";

function prefersReducedMotion() {
  return typeof window !== "undefined" && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}

function finishReveal(el) {
  el.classList.remove("reveal-in");
  el.style.transitionDelay = "";
}

/**
 * Marks content blocks that start below the fold and fades them in when they enter the viewport.
 * Blocks already on screen are never touched, so server-rendered content never blinks.
 */
function useScrollReveal(pathname) {
  useEffect(() => {
    if (prefersReducedMotion() || !("IntersectionObserver" in window)) return undefined;
    // Shells mount <main> after their own loaders, so watch the whole body rather than one <main>.
    const root = document.body;

    const io = new IntersectionObserver(
      (entries) => {
        const visible = entries.filter((e) => e.isIntersecting);
        visible.forEach((entry, i) => {
          const el = entry.target;
          io.unobserve(el);
          el.style.transitionDelay = `${Math.min(i, 4) * 70}ms`;
          el.classList.remove("reveal-pending");
          el.classList.add("reveal-in");
          el.dataset.revealState = "done";
          const done = () => finishReveal(el);
          el.addEventListener("transitionend", done, { once: true });
          setTimeout(done, 1100);
        });
      },
      { rootMargin: "0px 0px -6% 0px", threshold: 0.01 },
    );

    const scan = () => {
      const fold = window.innerHeight;
      root.querySelectorAll(REVEAL_SELECTOR).forEach((el) => {
        if (el.dataset.revealState) return;
        // Cards inside a section that is still waiting to fade in ride along with it.
        if (el.parentElement?.closest("[data-reveal-state='pending'], [data-no-reveal]")) {
          el.dataset.revealState = "skip";
          return;
        }
        if (el.closest("[data-no-reveal], [role='dialog'], .fixed")) {
          el.dataset.revealState = "skip";
          return;
        }
        const rect = el.getBoundingClientRect();
        if (rect.height === 0 || rect.top < fold) {
          el.dataset.revealState = "done";
          return;
        }
        el.dataset.revealState = "pending";
        el.classList.add("reveal-pending");
        io.observe(el);
      });
    };

    let frame = 0;
    const schedule = () => {
      cancelAnimationFrame(frame);
      frame = requestAnimationFrame(scan);
    };
    scan();
    // Pages that load data after mount add their cards later.
    const mo = new MutationObserver(schedule);
    mo.observe(root, { childList: true, subtree: true });

    return () => {
      cancelAnimationFrame(frame);
      mo.disconnect();
      io.disconnect();
      // Never leave content hidden if the page changes mid-animation.
      root.querySelectorAll(".reveal-pending, .reveal-in").forEach((el) => {
        el.classList.remove("reveal-pending", "reveal-in");
        el.style.transitionDelay = "";
      });
    };
  }, [pathname]);
}

/** Soft fade-up of the main content on every client-side navigation. */
function useRouteEnter(pathname) {
  const first = useRef(true);
  useEffect(() => {
    if (first.current) {
      first.current = false;
      return;
    }
    if (prefersReducedMotion()) return;
    const main = document.querySelector("main");
    if (!main?.animate) return;
    main.animate(
      [
        { opacity: 0, transform: "translateY(8px)" },
        { opacity: 1, transform: "none" },
      ],
      { duration: 320, easing: EASE },
    );
  }, [pathname]);
}

/** Flags html[data-scrolled] for the sticky-header shadow, and reports whether the back-to-top button should show. */
function useScrollState() {
  const [showTop, setShowTop] = useState(false);
  useEffect(() => {
    let frame = 0;
    const update = () => {
      frame = 0;
      const y = window.scrollY;
      document.documentElement.toggleAttribute("data-scrolled", y > 8);
      setShowTop(y > 520);
    };
    const onScroll = () => {
      if (!frame) frame = requestAnimationFrame(update);
    };
    update();
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => {
      window.removeEventListener("scroll", onScroll);
      cancelAnimationFrame(frame);
    };
  }, []);
  return showTop;
}

export function MotionEffects({ raised = false }) {
  const pathname = usePathname();
  useScrollReveal(pathname);
  useRouteEnter(pathname);
  const showTop = useScrollState();

  return (
    <button
      type="button"
      aria-label="Back to top"
      tabIndex={showTop ? 0 : -1}
      onClick={() => window.scrollTo({ top: 0, behavior: prefersReducedMotion() ? "auto" : "smooth" })}
      className={cn(
        "fixed right-4 z-[55] inline-flex h-11 w-11 items-center justify-center rounded-full border border-[#d5e3df] bg-cream text-nexus-700 shadow-[0_8px_24px_rgba(51,104,160,0.18)] sm:right-6",
        "transition-[opacity,translate,background-color,color] duration-300 ease-out hover:-translate-y-0.5 hover:bg-ocean hover:text-white",
        "dark:border-nexus-700 dark:bg-nexus-900 dark:text-nexus-200 dark:hover:bg-sky dark:hover:text-nexus-950",
        raised ? "bottom-24 sm:bottom-[6.5rem]" : "bottom-4 sm:bottom-6",
        showTop ? "translate-y-0 opacity-100" : "pointer-events-none translate-y-3 opacity-0",
      )}
    >
      <ArrowUp className="h-4 w-4" />
    </button>
  );
}
