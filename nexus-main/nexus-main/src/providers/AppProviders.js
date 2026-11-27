"use client";

import { useEffect } from "react";
import { usePathname } from "next/navigation";
import { Toaster } from "sonner";
import { AiAssistant } from "@/components/assistant/AiAssistant";
import { HydrationOverlay, RouteProgress } from "@/components/brand/LuxuryLoader";
import { MotionEffects } from "@/components/motion/MotionEffects";
import { useAppStore } from "@/store/useAppStore";
import { useHydrated } from "@/hooks/useApp";

function ThemeSync() {
  const theme = useAppStore((s) => s.uiPreferences.theme);
  const hydrated = useHydrated();

  useEffect(() => {
    if (!hydrated) return;
    const root = document.documentElement;
    const apply = (mode) => {
      root.classList.toggle("dark", mode === "dark");
    };
    if (theme === "system") {
      const mq = window.matchMedia("(prefers-color-scheme: dark)");
      apply(mq.matches ? "dark" : "light");
      const listener = (e) => apply(e.matches ? "dark" : "light");
      mq.addEventListener("change", listener);
      return () => mq.removeEventListener("change", listener);
    }
    apply(theme);
    return undefined;
  }, [theme, hydrated]);

  return null;
}

export function AppProviders({ children }) {
  const pathname = usePathname();
  // The assistant runs on the prototype's mock data, so keep it out of the API-backed internship portal.
  const showAssistant = !pathname?.startsWith("/internship") && !pathname?.startsWith("/login");
  return (
    <>
      <ThemeSync />
      <HydrationOverlay />
      <RouteProgress />
      {children}
      {showAssistant ? <AiAssistant /> : null}
      <MotionEffects raised={showAssistant} />
      <Toaster
        position="top-right"
        closeButton
        dir="ltr"
        offset={16}
        gap={10}
        toastOptions={{
          duration: 4000,
          classNames: {
            toast: "nexus-toast",
            title: "nexus-toast-title",
            description: "nexus-toast-description",
            success: "nexus-toast-success",
            error: "nexus-toast-error",
            info: "nexus-toast-info",
            warning: "nexus-toast-warning",
            closeButton: "nexus-toast-close",
          },
        }}
      />
    </>
  );
}
