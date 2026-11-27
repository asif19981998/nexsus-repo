"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Eye, EyeOff, GraduationCap, Scale, UserCheck } from "lucide-react";
import { toast } from "sonner";
import { AuthCard } from "@/components/auth/AuthCard";
import { Button, Input } from "@/components/ui";
import { api } from "@/lib/api";
import { homeFor } from "@/lib/internshipPermissions";
import { useAuthStore } from "@/store/useAuthStore";
import { cn } from "@/lib/cn";

// Accounts created by the backend seeder (Seed:DemoData). Remove once real accounts exist.
const DEMO_ACCOUNTS = [
  { email: "admin@nexus.demo", role: "Admin", name: "Farhana Akter", icon: Scale },
  { email: "mentor@nexus.demo", role: "Mentor", name: "Dr. Mahfuza Khatun", icon: UserCheck },
  { email: "student@nexus.demo", role: "Student", name: "Ayesha Rahman", icon: GraduationCap },
];
const DEMO_PASSWORD = "demo123";

function canFrom(permissions) {
  return (perm) => (Array.isArray(perm) ? perm.some((p) => permissions.includes(p)) : permissions.includes(perm));
}

export default function LoginPage() {
  const router = useRouter();
  const setSession = useAuthStore((s) => s.setSession);
  const session = useAuthStore((s) => s.user);
  const hydrated = useAuthStore((s) => s.hydrated);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [show, setShow] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  // Already signed in: go straight to the portal.
  useEffect(() => {
    if (hydrated && session) router.replace(homeFor(canFrom(session.permissions || [])));
  }, [hydrated, session, router]);

  const signIn = async (e, override) => {
    e?.preventDefault();
    const creds = override || { email, password };
    if (!creds.email || !creds.password) {
      setError("Enter your email and password.");
      return;
    }
    setError("");
    setLoading(true);
    try {
      const res = await api.login(creds.email.trim(), creds.password);
      setSession(res);
      toast.success(`Welcome, ${res.user.fullName}`);
      const next = new URLSearchParams(window.location.search).get("next");
      router.replace(["/dashboard", "/internship", "/account", "/softskill", "/programmes"].some((p) => next?.startsWith(p)) ? next : homeFor(canFrom(res.user.permissions)));
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="flex w-full justify-center py-6">
      <AuthCard title="Sign in" subtitle="UGC Internship Programme" badge="Nexus">
        <form onSubmit={signIn} className="space-y-4" noValidate>
          <Input label="Email" type="email" autoComplete="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
          <div className="relative">
            <Input
              label="Password"
              type={show ? "text" : "password"}
              autoComplete="current-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
            />
            <button
              type="button"
              onClick={() => setShow((v) => !v)}
              className="absolute right-3 top-[34px] text-secondary hover:text-nexus-700"
              aria-label={show ? "Hide password" : "Show password"}
            >
              {show ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
            </button>
          </div>
          {error ? (
            <p role="alert" className="rounded-lg bg-red-50 p-2 text-sm text-danger">
              {error}
            </p>
          ) : null}
          <Button type="submit" className="w-full" disabled={loading}>
            {loading ? "Signing in…" : "Sign in"}
          </Button>
        </form>

        <p className="mt-4 text-center text-sm text-secondary">
          New applicant?{" "}
          <Link href="/register" className="font-medium text-nexus-700 hover:underline dark:text-nexus-300">
            Create an account
          </Link>
        </p>

        <div className="mt-6 border-t border-[#d5e3df] pt-4 dark:border-nexus-800">
          <p className="text-[11px] font-semibold tracking-[0.16em] text-nexus-500 uppercase">Demo accounts</p>
          <p className="mt-1 text-xs text-secondary">
            Seeded by the backend. Password <code className="rounded bg-chrome px-1 py-0.5 dark:bg-nexus-800">{DEMO_PASSWORD}</code>
          </p>
          <div className="mt-3 grid gap-2 sm:grid-cols-3">
            {DEMO_ACCOUNTS.map((acc) => {
              const Icon = acc.icon;
              return (
                <button
                  key={acc.email}
                  type="button"
                  disabled={loading}
                  onClick={() => {
                    setEmail(acc.email);
                    setPassword(DEMO_PASSWORD);
                    signIn(null, { email: acc.email, password: DEMO_PASSWORD });
                  }}
                  className={cn(
                    "flex items-center gap-2 rounded-xl border border-[#d5e3df] bg-cream px-2.5 py-2 text-left transition-colors",
                    "hover:border-nexus-400 hover:bg-sage/40 disabled:opacity-60 dark:border-nexus-800 dark:bg-nexus-950/50",
                  )}
                >
                  <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-lg bg-nexus-600 text-white">
                    <Icon className="h-3.5 w-3.5" />
                  </span>
                  <span className="min-w-0">
                    <span className="block text-xs font-semibold">{acc.role}</span>
                    <span className="block truncate text-[11px] text-secondary">{acc.name}</span>
                  </span>
                </button>
              );
            })}
          </div>
        </div>
      </AuthCard>
    </div>
  );
}
