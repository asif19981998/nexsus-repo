import { InternshipShell } from "@/components/layout/InternshipShell";

/** UGC Soft Skill Training: the same portal frame as the internship; menu entries follow the user's permissions. */
export default function SoftSkillLayout({ children }) {
  return <InternshipShell>{children}</InternshipShell>;
}
