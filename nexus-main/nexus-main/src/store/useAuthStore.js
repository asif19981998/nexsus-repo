"use client";

// Signed-in session for the API-backed UGC Internship module.
// Holds the JWT and the user (role + permission codes) returned by the backend.
import { create } from "zustand";
import { persist, createJSONStorage } from "zustand/middleware";

export const useAuthStore = create(
  persist(
    (set, get) => ({
      token: null,
      expiresAt: null,
      user: null,
      hydrated: false,

      setSession: ({ token, expiresAt, user }) => set({ token, expiresAt, user }),
      setUser: (user) => set({ user }),
      clear: () => set({ token: null, expiresAt: null, user: null }),
      setHydrated: () => set({ hydrated: true }),

      /** True when the user's role holds the permission (or any of them, given an array). */
      can: (perm) => {
        const perms = get().user?.permissions || [];
        return Array.isArray(perm) ? perm.some((p) => perms.includes(p)) : perms.includes(perm);
      },
    }),
    {
      name: "nexus-auth-v1",
      storage: createJSONStorage(() => localStorage),
      partialize: (s) => ({ token: s.token, expiresAt: s.expiresAt, user: s.user }),
      onRehydrateStorage: () => (state) => {
        if (state?.expiresAt && new Date(state.expiresAt) <= new Date()) state.clear();
        state?.setHydrated();
      },
    }
  )
);
