"use client";

import { useAction } from "@/hooks/useApi";
import { api } from "@/lib/api";

/**
 * Opens a private document in a new tab. Documents are not linked directly: the file is fetched with the
 * signed-in user's token and shown from a temporary blob URL.
 */
export function useOpenDocument() {
  const { run, busy } = useAction();
  const open = async (doc) => {
    // Open the tab while the click still counts as the user's, then point it at the file.
    const tab = window.open("", "_blank");
    const blob = await run(() => api.download(`/documents/${doc.id}/file`));
    if (!blob) {
      tab?.close();
      return;
    }
    const url = URL.createObjectURL(blob);
    if (tab) tab.location.href = url;
    else window.location.href = url;
    setTimeout(() => URL.revokeObjectURL(url), 60000);
  };
  return { open, busy };
}
