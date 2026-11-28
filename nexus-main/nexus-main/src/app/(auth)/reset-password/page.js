"use client";

import { Suspense, useState } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Eye, EyeOff } from "lucide-react";
import { toast } from "sonner";
import { AuthCard } from "@/components/auth/AuthCard";
import { Button, Input } from "@/components/ui";
import { api } from "@/lib/api";
import { password as validatePassword } from "@/lib/validators";

export default function ResetPasswordPage() {
  return (
    <Suspense>
      <ResetPasswordForm />
    </Suspense>
  );
}

function ResetPasswordForm() {
  const router = useRouter();
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [loading, setLoading] = useState(false);
  const [errors, setErrors] = useState({});

  // The emailed link carries ?email=...&token=...
  const params = useSearchParams();
  const link = { email: params.get("email") || "", token: params.get("token") || "" };
  const resetEmail = link.email;
  const linkMissing = !link.email || !link.token;

  const handleSubmit = async (e) => {
    e.preventDefault();
    const nextErrors = {
      password: validatePassword(password),
      confirm: password !== confirm ? "Passwords do not match" : "",
    };
    setErrors(nextErrors);
    if (Object.values(nextErrors).some(Boolean)) return;

    setLoading(true);
    try {
      await api.resetPassword({ email: link.email, token: link.token, newPassword: password, confirmPassword: confirm });
      toast.success("Password updated", { description: "Sign in with your new password." });
      router.push("/login");
    } catch (err) {
      setErrors({ form: err.message });
    } finally {
      setLoading(false);
    }
  };

  if (linkMissing) {
    return (
      <AuthCard title="Reset link needed" subtitle="Open this page from the link in your password-reset email.">
        <Link href="/forgot-password">
          <Button className="w-full">Send me a reset link</Button>
        </Link>
      </AuthCard>
    );
  }

  return (
    <AuthCard
      title="Set new password"
      subtitle={
        resetEmail
          ? `Create a new password for ${resetEmail}.`
          : "Enter a new password for your account."
      }
    >
      <form onSubmit={handleSubmit} className="space-y-4">
        <div className="relative">
          <Input
            label="New password"
            name="password"
            type={showPassword ? "text" : "password"}
            placeholder="At least 6 characters"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            error={errors.password}
            required
          />
          <button
            type="button"
            className="absolute top-8 right-3 text-slate-400 hover:text-slate-600"
            onClick={() => setShowPassword((v) => !v)}
            aria-label={showPassword ? "Hide password" : "Show password"}
          >
            {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
          </button>
        </div>
        <Input
          label="Confirm password"
          name="confirm"
          type={showPassword ? "text" : "password"}
          value={confirm}
          onChange={(e) => setConfirm(e.target.value)}
          error={errors.confirm}
          required
        />
        {errors.form && (
          <p className="text-sm text-red-600">
            {errors.form}{" "}
            <Link href="/forgot-password" className="font-medium underline">
              Get a new link
            </Link>
          </p>
        )}
        <Button type="submit" className="w-full" loading={loading}>
          Update password
        </Button>
      </form>
      <p className="mt-4 text-center text-sm text-secondary">
        <Link href="/login" className="font-medium text-nexus-700 hover:underline dark:text-nexus-300">
          Back to sign in
        </Link>
      </p>
    </AuthCard>
  );
}
