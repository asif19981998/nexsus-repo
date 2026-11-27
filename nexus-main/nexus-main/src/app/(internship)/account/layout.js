import { InternshipShell } from "@/components/layout/InternshipShell";

/** Account pages (profile, documents, notifications) belong to the person, not to a module; they share the portal frame. */
export default function AccountLayout({ children }) {
  return <InternshipShell>{children}</InternshipShell>;
}
