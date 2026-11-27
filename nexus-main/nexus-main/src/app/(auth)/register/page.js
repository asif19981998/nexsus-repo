"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Eye, EyeOff } from "lucide-react";
import { toast } from "sonner";
import { AuthCard } from "@/components/auth/AuthCard";
import { Button, Checkbox, Input, RadioGroup } from "@/components/ui";
import { api } from "@/lib/api";
import { homeFor } from "@/lib/internshipPermissions";
import { useAuthStore } from "@/store/useAuthStore";

const EMPTY = { accountType: "Student", fullName: "", email: "", phone: "", password: "", confirmPassword: "", declaration: false };

// The two roles open to the public. Staff accounts are made by the administrator.
const ACCOUNT_TYPES = [
  { value: "Student", label: "Student — apply for the UGC Internship or the Soft Skill Training" },
  { value: "Trainer", label: "University teacher — register as a Soft Skill trainer" },
];

function canFrom(permissions) {
  return (perm) => (Array.isArray(perm) ? perm.some((p) => permissions.includes(p)) : permissions.includes(perm));
}

/** "01712345678" from "+880 1712-345678"; null when it is not a Bangladeshi mobile number. Same rule as the API. */
function normalizePhone(value) {
  let digits = value.replace(/\D/g, "");
  if (digits.startsWith("880")) digits = digits.slice(2);
  return /^01[3-9]\d{8}$/.test(digits) ? digits : null;
}

function validate(f) {
  const e = {};
  if (!f.fullName.trim()) e.fullName = "Enter your full name.";
  else if (f.fullName.trim().length > 150) e.fullName = "At most 150 characters.";
  if (!f.email.trim()) e.email = "Enter your email address.";
  else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(f.email.trim())) e.email = "Enter a valid email address.";
  if (!f.phone.trim()) e.phone = "Enter your mobile number.";
  else if (!normalizePhone(f.phone)) e.phone = "Enter a Bangladeshi mobile number, e.g. 01712345678.";
  if (!f.password) e.password = "Choose a password.";
  else if (f.password.length < 6 || !/\d/.test(f.password)) e.password = "At least 6 characters, with at least one number.";
  if (!f.confirmPassword) e.confirmPassword = "Type the password again.";
  else if (f.password !== f.confirmPassword) e.confirmPassword = "The two passwords do not match.";
  if (!f.declaration) e.declaration = "Confirm that the information is correct.";
  return e;
}

export default function RegisterPage() {
  const router = useRouter();
  const setSession = useAuthStore((s) => s.setSession);
  const session = useAuthStore((s) => s.user);
  const hydrated = useAuthStore((s) => s.hydrated);
  const [form, setForm] = useState(EMPTY);
  const [errors, setErrors] = useState({});
  const [error, setError] = useState("");
  const [show, setShow] = useState(false);
  const [loading, setLoading] = useState(false);

  // Already signed in: go straight to the portal.
  useEffect(() => {
    if (hydrated && session) router.replace(homeFor(canFrom(session.permissions || [])));
  }, [hydrated, session, router]);

  const set = (key) => (e) => {
    const value = e.target.type === "checkbox" ? e.target.checked : e.target.value;
    setForm((f) => ({ ...f, [key]: value }));
    if (errors[key]) setErrors((x) => ({ ...x, [key]: undefined }));
  };

  const submit = async (e) => {
    e.preventDefault();
    const found = validate(form);
    setErrors(found);
    setError("");
    if (Object.keys(found).length > 0) return;

    setLoading(true);
    try {
      const res = await api.register({
        fullName: form.fullName.trim(),
        email: form.email.trim(),
        phone: normalizePhone(form.phone),
        password: form.password,
        confirmPassword: form.confirmPassword,
        declaration: form.declaration,
        accountType: form.accountType,
      });
      setSession(res);
      toast.success(`Welcome, ${res.user.fullName}. Your account is ready.`);
      router.replace(homeFor(canFrom(res.user.permissions)));
    } catch (err) {
      // A taken email or number belongs next to its field; anything else goes in the banner.
      if (err.status === 409 && /email/i.test(err.message)) setErrors({ email: err.message });
      else if (err.status === 409 && /mobile/i.test(err.message)) setErrors({ phone: err.message });
      else setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const toggle = (
    <button
      type="button"
      onClick={() => setShow((v) => !v)}
      className="absolute right-3 top-[34px] text-secondary hover:text-nexus-700"
      aria-label={show ? "Hide password" : "Show password"}
    >
      {show ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
    </button>
  );

  return (
    <div className="flex w-full justify-center py-6">
      <AuthCard title="Create your account" subtitle="Join UGC programmes as a student or as a university teacher" badge="Nexus">
        <form onSubmit={submit} className="space-y-4" noValidate>
          <RadioGroup
            label="I am joining as"
            name="accountType"
            options={ACCOUNT_TYPES}
            value={form.accountType}
            onChange={(value) => setForm((f) => ({ ...f, accountType: value }))}
          />
          <Input
            label="Full name"
            name="fullName"
            autoComplete="name"
            value={form.fullName}
            onChange={set("fullName")}
            error={errors.fullName}
            hint="As written on your academic certificates."
            required
          />
          <Input
            label="Email"
            name="email"
            type="email"
            autoComplete="email"
            value={form.email}
            onChange={set("email")}
            error={errors.email}
            hint="You will sign in with this email."
            required
          />
          <Input
            label="Mobile number"
            name="phone"
            type="tel"
            inputMode="tel"
            autoComplete="tel"
            placeholder="01XXXXXXXXX"
            value={form.phone}
            onChange={set("phone")}
            error={errors.phone}
            required
          />
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="relative">
              <Input
                label="Password"
                name="password"
                type={show ? "text" : "password"}
                autoComplete="new-password"
                value={form.password}
                onChange={set("password")}
                error={errors.password}
                hint="6+ characters, with a number."
                required
              />
              {toggle}
            </div>
            <Input
              label="Confirm password"
              name="confirmPassword"
              type={show ? "text" : "password"}
              autoComplete="new-password"
              value={form.confirmPassword}
              onChange={set("confirmPassword")}
              error={errors.confirmPassword}
              required
            />
          </div>

          <div>
            <Checkbox
              name="declaration"
              checked={form.declaration}
              onChange={set("declaration")}
              label="I confirm that the information I have given is correct."
            />
            {errors.declaration ? <p className="mt-1 text-xs text-danger">{errors.declaration}</p> : null}
          </div>

          {error ? (
            <p role="alert" className="rounded-lg bg-red-50 p-2 text-sm text-danger">
              {error}
            </p>
          ) : null}
          <Button type="submit" className="w-full" disabled={loading}>
            {loading ? "Creating account…" : "Create account"}
          </Button>
        </form>

        <p className="mt-6 text-center text-sm text-secondary">
          Already have an account?{" "}
          <Link href="/login" className="font-medium text-nexus-700 hover:underline dark:text-nexus-300">
            Sign in
          </Link>
        </p>
      </AuthCard>
    </div>
  );
}
