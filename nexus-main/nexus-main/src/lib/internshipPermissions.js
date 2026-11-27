// Permission codes checked by the UGC Internship screens. They mirror
// backend/Nexus.Application/Security/Permissions.cs; the backend enforces them, the UI only hides what a role cannot do.
import { Award, CalendarCheck, ClipboardList, Eye, Gauge, LayoutGrid, Presentation, Sparkles, Users, UserRound } from "lucide-react";
import { SP, SS_MANAGE_PERMS } from "@/lib/softskillPermissions";

export const P = {
  programmeView: "internship.programme.view",
  apply: "internship.application.apply",
  selfView: "internship.self.view",
  selfAttendance: "internship.self.attendance",
  selfBill: "internship.self.bill",
  selfReport: "internship.self.report",

  overviewView: "internship.overview.view",
  batchView: "internship.batch.view",
  batchManage: "internship.batch.manage",
  batchOverride: "internship.batch.override",
  applicationView: "internship.application.view",
  applicationScreen: "internship.application.screen",
  applicationExam: "internship.application.exam",
  applicationSelect: "internship.application.select",
  internPlace: "internship.intern.place",
  internViewAll: "internship.intern.view_all",
  internViewAssigned: "internship.intern.view_assigned",
  internTransfer: "internship.intern.transfer",
  attendanceManage: "internship.attendance.manage",
  reportReview: "internship.report.review",
  evaluationSubmit: "internship.evaluation.submit",
  internComplete: "internship.intern.complete",
  certificateIssue: "internship.certificate.issue",
  internTerminate: "internship.intern.terminate",
  billView: "internship.bill.view",
  billPay: "internship.bill.pay",

  usersView: "users.view",
  usersManage: "users.manage",
};

/** Admin-side tabs and who sees them. */
export const MANAGE_TABS = [
  { value: "overview", label: "Overview", perm: P.overviewView },
  { value: "batches", label: "Batches", perm: P.batchView },
  { value: "applications", label: "Applications", perm: P.applicationView },
  { value: "interns", label: "Interns", perm: [P.internViewAll, P.internViewAssigned] },
  { value: "stipend", label: "Allowance", perm: P.billView },
];

/**
 * Sidebar, in sections: programmes (untitled) for applicants, "Administration" for staff, then a rule (`divider`)
 * before the person's own pages.
 * "My profile" belongs to the person, not to a module, so every signed-in user has it (perm null); its documents are a
 * tab inside it (`match`), and notifications open from the bell in the header.
 * New modules (skill training, jobs…) add their entries to "programmes"; staff screens go under "Administration".
 */
export const NAV_SECTIONS = [
  {
    key: "programmes",
    title: null,
    items: [
      // Everyone's landing page: what is waiting on them across every module they work in.
      { href: "/dashboard", label: "Dashboard", icon: Gauge, perm: null },
      { href: "/programmes", label: "All programmes", icon: LayoutGrid, perm: [P.programmeView, SP.programmeView, SP.trainerApply] },
      { href: "/internship/programme", label: "UGC Internship", icon: Award, perm: [P.programmeView, P.selfView] },
      { href: "/softskill/programme", label: "Soft Skill Training", icon: Sparkles, perm: [SP.programmeView, SP.selfView] },
      {
        href: "/softskill/trainer",
        match: ["/softskill/trainer", "/softskill/university-groups"],
        label: "My training work",
        icon: Presentation,
        perm: SP.trainerApply,
      },
    ],
  },
  {
    key: "admin",
    title: "Administration",
    items: [
      { href: "/internship/users", label: "Users", icon: Users, perm: P.usersView },
      {
        href: "/internship/manage",
        label: "UGC Internship",
        icon: ClipboardList,
        perm: MANAGE_TABS.flatMap((t) => (Array.isArray(t.perm) ? t.perm : [t.perm])),
      },
      { href: "/softskill/manage", label: "Soft Skill Training", icon: Sparkles, perm: SS_MANAGE_PERMS },
      // Experts the Commission engages (cl.14 ঘ): their visits and reports.
      { href: "/softskill/monitoring", label: "Monitoring visits", icon: Eye, perm: SP.monitorVisit },
      // Interns' attendance, so it sits with the programme screens it belongs to.
      { href: "/internship/attendance", label: "Attendance", icon: CalendarCheck, perm: [P.internViewAll, P.internViewAssigned] },
    ],
  },
  {
    key: "me",
    title: null,
    divider: true,
    items: [
      { href: "/account/profile", match: ["/account/profile", "/account/documents"], label: "My profile", icon: UserRound, perm: null },
      { href: "/internship/my-attendance", label: "My attendance", icon: CalendarCheck, perm: P.selfAttendance },
    ],
  },
];

/** Every sidebar entry, in order. */
export const INTERNSHIP_NAV = NAV_SECTIONS.flatMap((s) => s.items);

export function allowed(can, perm) {
  return perm == null || can(perm);
}

/**
 * Whether this person applies to programmes (a student or intern). Staff do not, so their profile has no education
 * or documents. Each new programme adds its own apply permission here.
 */
export function isApplicant(can) {
  return can([P.apply, P.selfView, SP.apply, SP.selfView, SP.trainerApply]);
}

/** A university teacher's account (Soft Skill trainer): a workplace instead of education on the profile. */
export function isTrainer(can) {
  return can(SP.trainerApply);
}

/**
 * Where the user lands after sign-in: the dashboard. Every signed-in user can open it, and it shows only the blocks
 * their permissions allow, so the answer no longer depends on the role.
 */
export function homeFor() {
  return "/dashboard";
}

const under = (pathname, href) => pathname === href || !!pathname?.startsWith(href + "/");

/** Whether a sidebar entry is the one for this path. */
export function isActive(item, pathname) {
  return (item.match || [item.href]).some((h) => under(pathname, h));
}

/** The page header title: account pages name themselves, module and staff pages say "UGC Internship". */
export function headerFor(pathname) {
  if (under(pathname, "/account/notifications")) return "Notifications";
  if (under(pathname, "/account")) return "My profile";
  if (under(pathname, "/dashboard")) return "Dashboard";
  if (under(pathname, "/programmes")) return "Programmes";
  if (under(pathname, "/softskill")) return "UGC Soft Skill Training";
  return "UGC Internship";
}

/** Badge colour for a day kind returned by the API. */
export const DAY_TONE = {
  present: "green",
  "leave-requested": "amber",
  leave: "blue",
  "absent-approved": "blue",
  absent: "red",
  "absent-no-signin": "red",
  open: "slate",
};

export const LEAVE_TONE = { Pending: "amber", Approved: "green", Rejected: "red" };

/** Display names for roles. The role keys ("Student", …) stay as they are in the database and permission checks. */
export const ROLE_LABEL = { Student: "Student / Intern", Trainer: "Trainer (university teacher)", Monitor: "Monitor (expert)" };
export const roleLabel = (role) => ROLE_LABEL[role] || role;
