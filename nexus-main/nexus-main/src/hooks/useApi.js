"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { toast } from "sonner";
import { api } from "@/lib/api";
import { useAuthStore } from "@/store/useAuthStore";

/**
 * Loads `path` from the API and reloads when it changes. Pass null to skip.
 * Returns { data, error, loading, reload, setData }.
 */
export function useApiQuery(path) {
  const [state, setState] = useState({ data: undefined, error: null, loading: Boolean(path) });
  const seq = useRef(0);

  const load = useCallback(async () => {
    if (!path) return;
    const mine = ++seq.current;
    setState((s) => ({ ...s, loading: true, error: null }));
    try {
      const data = await api.get(path);
      if (mine === seq.current) setState({ data, error: null, loading: false });
    } catch (error) {
      if (mine === seq.current) setState((s) => ({ ...s, error, loading: false }));
    }
  }, [path]);

  useEffect(() => {
    load();
  }, [load]);

  const setData = useCallback((data) => setState((s) => ({ ...s, data })), []);
  return { ...state, reload: load, setData };
}

/**
 * Wraps a mutation: shows a toast for success or failure and returns the result, or null on failure.
 * `run(fn, okMessage)` — fn is an async function calling the API.
 */
export function useAction() {
  const [busy, setBusy] = useState(false);
  const run = useCallback(async (fn, okMessage, { silentError = false } = {}) => {
    setBusy(true);
    try {
      const result = await fn();
      if (okMessage) toast.success(okMessage);
      return result ?? true;
    } catch (error) {
      if (!silentError) toast.error(error.message || "Action failed");
      if (silentError) throw error;
      return null;
    } finally {
      setBusy(false);
    }
  }, []);
  return { run, busy };
}

/** The signed-in user and a permission check. */
export function useSession() {
  const user = useAuthStore((s) => s.user);
  const token = useAuthStore((s) => s.token);
  const hydrated = useAuthStore((s) => s.hydrated);
  const permissions = user?.permissions;
  const can = useCallback(
    (perm) => {
      const list = permissions || [];
      return Array.isArray(perm) ? perm.some((p) => list.includes(p)) : list.includes(perm);
    },
    [permissions]
  );
  return { user, token, hydrated, can };
}
