"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { createContext, useContext, useEffect, useState } from "react";
import { Bell, ChevronDown, LogOut, Menu, X, PanelLeftClose, PanelLeft } from "lucide-react";
import { cn } from "@/lib/cn";
import { api } from "@/lib/api";
import { useAuthStore } from "@/store/useAuthStore";
import { useAppStore } from "@/store/useAppStore";
import { useApiQuery, useSession } from "@/hooks/useApi";
import { NAV_SECTIONS, allowed, headerFor, isActive, roleLabel } from "@/lib/internshipPermissions";
import { Avatar, Badge, DropdownMenu, ErrorState, IconButton } from "@/components/ui";
import { Button } from "@/components/ui/Button";
import { NexusLogo, NexusMark } from "@/components/brand/NexusLogo";
import { LuxuryLoader } from "@/components/brand/LuxuryLoader";
import { ThemeToggle } from "@/components/layout/Shell";

const SetupContext = createContext(null);

/** Policy numbers, departments, criteria and statuses loaded from GET /internship/setup. */
export function useSetup() {
  return useContext(SetupContext);
}

const NotificationContext = createContext({ unread: 0, refresh: () => {} });

export function useNotificationBadge() {
  return useContext(NotificationContext);
}

function Sidebar({ collapsed, onNavigate }) {
  const pathname = usePathname();
  const { can } = useSession();
  const sections = NAV_SECTIONS.map((s) => ({ ...s, items: s.items.filter((n) => allowed(can, n.perm)) })).filter((s) => s.items.length > 0);
  return (
    <nav aria-label="Portal" className="space-y-4 p-3">
      {sections.map((section, i) => (
        <div key={section.key} className="space-y-1">
          {section.divider ? (
            i > 0 && <div className="mx-2 mb-3 border-t border-[#d5e3df] dark:border-nexus-800" aria-hidden />
          ) : !section.title ? null : collapsed ? (
            i > 0 && <div className="mx-2 border-t border-[#d5e3df] dark:border-nexus-800" aria-hidden />
          ) : (
            <p className="px-3 pb-1 text-[11px] font-semibold tracking-[0.14em] text-nexus-500 uppercase">{section.title}</p>
          )}
          {section.items.map((item) => {
            const Icon = item.icon;
            const active = isActive(item, pathname);
            return (
              <Link
                key={item.href}
                href={item.href}
                onClick={onNavigate}
                title={item.label}
                className={cn(
                  "group flex items-center gap-3 rounded-lg px-3 py-2 text-sm transition-[color,background-color,box-shadow] duration-200",
                  active
                    ? "bg-ocean font-semibold text-white shadow-[0_4px_12px_-4px_rgba(51,104,160,0.6)]"
                    : "text-nexus-800 hover:bg-ocean hover:text-white dark:text-nexus-200 dark:hover:bg-sky dark:hover:text-nexus-950",
                  collapsed && "justify-center px-2",
                )}
              >
                <Icon className={cn("h-4 w-4 shrink-0 transition-transform duration-200", !active && "group-hover:scale-110")} />
                {!collapsed ? <span className="truncate">{item.label}</span> : null}
              </Link>
            );
          })}
        </div>
      ))}
    </nav>
  );
}

/**
 * Portal frame for the API-backed UGC Internship module. Requires a signed-in session,
 * re-reads the user's permissions from the database on load, and shows only the menu entries those permissions allow.
 */
export function InternshipShell({ children }) {
  const router = useRouter();
  const pathname = usePathname();
  const { user, token, hydrated } = useSession();
  const setUser = useAuthStore((s) => s.setUser);
  const clear = useAuthStore((s) => s.clear);
  const collapsed = useAppStore((s) => s.uiPreferences.sidebarCollapsed);
  const setUiPreferences = useAppStore((s) => s.setUiPreferences);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [fresh, setFresh] = useState(false);

  const setup = useApiQuery(token ? "/internship/setup" : null);
  const notes = useApiQuery(token ? "/notifications" : null);
  const reloadNotes = notes.reload;

  useEffect(() => {
    if (!hydrated) return;
    if (!token) {
      router.replace(`/login?next=${encodeURIComponent(pathname || "/internship")}`);
      return;
    }
    // Permissions come from the database, so refresh them on every visit.
    let cancelled = false;
    api
      .get("/auth/me")
      .then((me) => {
        if (!cancelled) {
          setUser(me);
          setFresh(true);
        }
      })
      .catch(() => {
        if (!cancelled) setFresh(true);
      });
    return () => {
      cancelled = true;
    };
  }, [hydrated, token, router, pathname, setUser]);

  useEffect(() => {
    if (!token) return undefined;
    const t = setInterval(() => reloadNotes(), 60000);
    return () => clearInterval(t);
  }, [token, reloadNotes]);

  if (!hydrated || !token || !user || !fresh) {
    return <LuxuryLoader variant="page" label="Opening your portal" />;
  }

  const unread = notes.data?.unread || 0;

  const signOut = () => {
    clear();
    router.replace("/login");
  };

  return (
    <SetupContext.Provider value={setup.data || null}>
      <NotificationContext.Provider value={{ unread, refresh: reloadNotes }}>
        <div className="min-h-screen bg-cream dark:bg-nexus-950">
          <div className="flex min-h-screen">
            <aside
              className={cn(
                "sticky top-0 hidden h-screen shrink-0 border-r transition-[width] duration-300 ease-out border-[#d5e3df] bg-chrome lg:flex lg:flex-col dark:border-nexus-800 dark:bg-nexus-900",
                collapsed ? "w-[72px]" : "w-64",
              )}
            >
              <div className="flex h-14 items-center border-b border-[#d5e3df] px-3 dark:border-nexus-800">
                {!collapsed ? <NexusLogo /> : <NexusMark size={32} />}
              </div>
              <div className="flex-1 overflow-y-auto">
                <Sidebar collapsed={collapsed} />
              </div>
              <div className="border-t border-[#d5e3df] p-2 dark:border-nexus-800">
                <IconButton label={collapsed ? "Expand sidebar" : "Collapse sidebar"} onClick={() => setUiPreferences({ sidebarCollapsed: !collapsed })}>
                  {collapsed ? <PanelLeft className="h-4 w-4" /> : <PanelLeftClose className="h-4 w-4" />}
                </IconButton>
              </div>
            </aside>

            {mobileOpen ? (
              <div className="fixed inset-0 z-50 lg:hidden">
                <button type="button" className="motion-fade absolute inset-0 bg-slate-950/50 backdrop-blur-[2px]" aria-label="Close navigation" onClick={() => setMobileOpen(false)} />
                <div className="motion-slide-left absolute top-0 left-0 h-full w-72 overflow-y-auto bg-chrome shadow-xl dark:bg-nexus-900">
                  <div className="flex h-14 items-center justify-between border-b border-[#d5e3df] px-3 dark:border-nexus-800">
                    <NexusLogo />
                    <IconButton label="Close" onClick={() => setMobileOpen(false)}>
                      <X className="h-4 w-4" />
                    </IconButton>
                  </div>
                  <Sidebar onNavigate={() => setMobileOpen(false)} />
                </div>
              </div>
            ) : null}

            <div className="flex min-w-0 flex-1 flex-col">
              <header className="sticky top-0 z-30 flex h-14 items-center justify-between gap-3 border-b border-[#d5e3df] bg-chrome/95 px-4 backdrop-blur dark:border-nexus-800 dark:bg-nexus-900/95">
                <div className="flex items-center gap-2">
                  <IconButton label="Open navigation" className="lg:hidden" onClick={() => setMobileOpen(true)}>
                    <Menu className="h-5 w-5" />
                  </IconButton>
                  <p className="text-sm font-semibold">{headerFor(pathname)}</p>
                  <Badge tone="teal">{user.role ? roleLabel(user.role) : "No role"}</Badge>
                </div>
                <div className="flex items-center gap-1">
                  <Link href="/account/notifications">
                    <IconButton label={unread ? `Notifications (${unread} unread)` : "Notifications"}>
                      <span className="relative">
                        <Bell className="h-4 w-4" />
                        {unread > 0 ? <span className="absolute -top-1 -right-1 h-2 w-2 rounded-full bg-danger" /> : null}
                      </span>
                    </IconButton>
                  </Link>
                  <ThemeToggle />
                  <DropdownMenu
                    className="min-w-64"
                    trigger={
                      <button
                        type="button"
                        className="inline-flex items-center gap-2 rounded-full border border-slate-200 py-1 pr-2 pl-1 text-sm hover:border-nexus-400 hover:bg-nexus-50 dark:border-slate-700 dark:hover:border-nexus-500 dark:hover:bg-nexus-800"
                      >
                        <Avatar name={user.fullName} size="sm" />
                        <span className="hidden max-w-[140px] truncate md:inline">{user.fullName}</span>
                        <ChevronDown className="h-3.5 w-3.5" />
                      </button>
                    }
                    items={[
                      {
                        header: true,
                        content: (
                          <div className="min-w-0">
                            <p className="truncate font-semibold text-nexus-900 dark:text-cream">{user.fullName}</p>
                            <p className="truncate text-xs text-secondary">{user.email}</p>
                            <p className="mt-1 text-xs font-medium text-nexus-700 dark:text-nexus-300">
                              {[roleLabel(user.role), user.designation, user.department].filter(Boolean).join(" · ")}
                            </p>
                          </div>
                        ),
                      },
                      { divider: true },
                      { label: "Sign out", icon: <LogOut className="h-4 w-4" />, danger: true, onClick: signOut },
                    ]}
                  />
                </div>
              </header>
              <main className="flex-1 p-4 sm:p-6">
                {setup.error ? (
                  <ErrorState
                    title="Could not load the programme settings"
                    description={setup.error.message}
                    action={<Button onClick={setup.reload}>Try again</Button>}
                  />
                ) : setup.data ? (
                  children
                ) : (
                  <LuxuryLoader variant="compact" label="Loading" />
                )}
              </main>
            </div>
          </div>
        </div>
      </NotificationContext.Provider>
    </SetupContext.Provider>
  );
}

/** Shown inside a page when the user's role lacks the permission for it. */
export function NoAccess({ what = "this page" }) {
  return <ErrorState title="No access" description={`Your role does not have permission to open ${what}. Ask an administrator if you need it.`} />;
}
