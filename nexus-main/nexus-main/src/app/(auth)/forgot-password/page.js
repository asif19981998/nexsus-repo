"use client";

import { useState } from "react";
import Link from "next/link";
import { toast } from "sonner";
import { AuthCard } from "@/components/auth/AuthCard";
import { Button, Input } from "@/components/ui";
import { api } from "@/lib/api";
import { email as validateEmail } from "@/lib/validators";

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [sent, setSent] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    const err = validateEmail(email);
    setError(err);
    if (err) return;

    setLoading(true);
    try {
      await api.forgotPassword(email.trim());
      setSent(true);
    } catch (err) {
      toast.error(err.message);
    } finally {
      setLoading(false);
    }
  };

  if (sent) {
    return (
      <AuthCard
        title="Check your email"
        subtitle={`If an account exists for ${email}, you'll receive password reset instructions shortly.`}
      >
        <p className="text-sm text-secondary">
          The link works for 30 minutes. Didn&apos;t receive it? Check your spam folder, or try again with the email
          you use to sign in.
        </p>
        <div className="mt-6 flex flex-col gap-3">
          <Button variant="secondary" onClick={() => setSent(false)}>
            Try another email
          </Button>
          <Link href="/login">
            <Button variant="ghost" className="w-full">
              Back to sign in
            </Button>
          </Link>
        </div>
      </AuthCard>
    );
  }

  return (
    <AuthCard
      title="Forgot password?"
      subtitle="Enter the email linked to your Nexus account. We'll send a reset link valid for 30 minutes."
    >
      <form onSubmit={handleSubmit} className="space-y-4">
        <Input
          label="Email address"
          name="email"
          type="email"
          placeholder="you@university.ac.bd"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          error={error}
          required
        />
        <Button type="submit" className="w-full" loading={loading}>
          Send reset link
        </Button>
      </form>
      <p className="mt-4 text-center text-sm text-secondary">
        Remember your password?{" "}
        <Link href="/login" className="font-medium text-nexus-700 hover:underline dark:text-nexus-300">
          Sign in
        </Link>
      </p>
    </AuthCard>
  );
}
