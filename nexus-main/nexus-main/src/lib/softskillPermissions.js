// Permission codes checked by the UGC Soft Skill Training screens. They mirror
// backend/Nexus.Application/Security/Permissions.cs (Permissions.SoftSkill); the backend enforces them,
// the UI only hides what a role cannot do. University group actions are also checked per university group on the server.

export const SP = {
  programmeView: "softskill.programme.view",
  apply: "softskill.application.apply",
  selfView: "softskill.self.view",
  selfLeave: "softskill.self.leave",
  selfReport: "softskill.self.report",

  trainerApply: "softskill.trainer.apply",
  universityGroupViewAssigned: "softskill.university_group.view_assigned",
  sessionAttendance: "softskill.session.attendance",
  assessmentEnter: "softskill.assessment.enter",
  universityGroupCoordinate: "softskill.university_group.coordinate",

  overviewView: "softskill.overview.view",
  batchView: "softskill.batch.view",
  batchManage: "softskill.batch.manage",
  batchOverride: "softskill.batch.override",
  applicationView: "softskill.application.view",
  applicationScreen: "softskill.application.screen",
  applicationSelect: "softskill.application.select",
  trainerReview: "softskill.trainer.review",
  universityGroupViewAll: "softskill.university_group.view_all",
  universityGroupManageAll: "softskill.university_group.manage_all",
  traineeCancel: "softskill.trainee.cancel",
  certificateIssue: "softskill.certificate.issue",
  stipendView: "softskill.stipend.view",
  stipendPay: "softskill.stipend.pay",

  monitoringManage: "softskill.monitoring.manage",
  monitorVisit: "softskill.monitor.visit",
  honorariumView: "softskill.honorarium.view",
  honorariumPay: "softskill.honorarium.pay",
  honorariumRates: "softskill.honorarium.rates",
};

/** The Commission's Soft Skill tabs and who sees them. */
export const SS_MANAGE_TABS = [
  { value: "overview", label: "Overview", perm: SP.overviewView },
  { value: "batches", label: "Batches & universities", perm: SP.batchView },
  { value: "applications", label: "Applications", perm: SP.applicationView },
  { value: "trainers", label: "Trainers", perm: SP.trainerReview },
  { value: "trainees", label: "Trainees", perm: SP.universityGroupViewAll },
  { value: "stipends", label: "Stipends", perm: SP.stipendView },
  { value: "monitoring", label: "Monitoring", perm: SP.monitoringManage },
  { value: "honorarium", label: "Honorarium", perm: SP.honorariumView },
];

export const SS_MANAGE_PERMS = SS_MANAGE_TABS.map((t) => t.perm);

/** Badge colour per stipend status (cl.10). */
export const STIPEND_TONE = {
  "Awaiting assessment": "slate",
  "Not eligible": "red",
  Withheld: "amber",
  "Ready to pay": "blue",
  Paid: "green",
};

export const TRAINER_TONE = { Submitted: "amber", Approved: "green", Rejected: "red", Suspended: "slate" };
export const SESSION_TONE = { Scheduled: "blue", Held: "green", Cancelled: "slate" };
export const ATTENDANCE_TONE = { Present: "green", Leave: "blue", "Leave requested": "amber", Absent: "red" };
export const VISIT_TONE = { Planned: "blue", Submitted: "green", Cancelled: "slate" };
export const HONORARIUM_TONE = { "Rate not set": "amber", Due: "blue", Paid: "green" };
export const HONORARIUM_KIND = { Session: "Session (trainer)", Coordination: "Coordination", MonitoringVisit: "Monitoring visit" };
