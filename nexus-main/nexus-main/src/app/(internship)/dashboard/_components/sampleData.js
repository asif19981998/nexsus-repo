// Sample data for the dashboard's design preview (/dashboard?preview=1, development only).
// Same shapes as the API responses, dates relative to `today`, so every card renders filled in.
// Nothing here is written to the database.
import { addDays, format, parseISO, subDays, subHours } from "date-fns";

export const PREVIEW_ENABLED = process.env.NODE_ENV !== "production";

export function sampleData(today) {
  const t = parseISO(today);
  const d = (n) => format(addDays(t, n), "yyyy-MM-dd");
  const ago = (h) => subHours(new Date(), h).toISOString();
  const month = (n) => format(subDays(t, 30 * n), "yyyy-MM");

  const person = (id, name, university = "DU") => ({ id, name, email: `${name.split(" ")[0].toLowerCase()}@example.com`, university });

  return {
    notes: {
      unread: 3,
      items: [
        { id: 1, title: "New UGC internship application", body: "Tanvir Hasan applied to 2027 Batch 1.", link: "/internship/manage?tab=applications", isRead: false, createdAt: ago(2) },
        { id: 2, title: "Allowance ready to pay", body: "Nusrat Jahan's allowance for last month is evaluated.", link: "/internship/manage?tab=stipend", isRead: false, createdAt: ago(20) },
        { id: 3, title: "Monitoring visit planned", body: "You visit RUET on the next session day.", link: "/softskill/monitoring", isRead: false, createdAt: ago(30) },
        { id: 4, title: "Document verified", body: "Your National ID was verified.", link: "/account/documents", isRead: true, createdAt: ago(50) },
        { id: 5, title: "Session scheduled", body: "Communication skills, Session 4, at CUET.", link: null, isRead: true, createdAt: ago(75) },
        { id: 6, title: "Leave request", body: "Nusrat Jahan asked for leave on Sunday.", link: "/internship/manage?tab=interns", isRead: true, createdAt: ago(96) },
        { id: 7, title: "Trainer registration", body: "Mehedi Hasan (RUET) registered as a trainer.", link: "/softskill/manage?tab=trainers", isRead: true, createdAt: ago(120) },
        { id: 8, title: "Batch opened", body: "2027 Batch 1 is open for applications.", link: "/internship/manage?tab=batches", isRead: true, createdAt: ago(170) },
      ],
    },

    internOverview: {
      openBatches: 2,
      inSelection: 14,
      seatsLeft: 18,
      activeInterns: 27,
      atRisk: [
        { name: "Rafiq Islam", pct: 71 },
        { name: "Sumaiya Akter", pct: 76 },
        { name: "Imran Hossain", pct: 78 },
        { name: "Tasnim Ferdous", pct: 74 },
        { name: "Arif Chowdhury", pct: 69 },
        { name: "Mitu Begum", pct: 79 },
      ],
      actions: [
        { count: 6, label: "applications to screen", tab: "applications" },
        { count: 2, label: "selected candidates to place in a department", tab: "applications" },
        { count: 4, label: "allowance bills ready to pay", tab: "stipend" },
      ],
      funnel: [
        { status: "Submitted", count: 6 },
        { status: "Shortlisted", count: 5 },
        { status: "Waitlisted", count: 1 },
        { status: "Selected", count: 2 },
        { status: "Active", count: 27 },
        { status: "Completed", count: 4 },
        { status: "Certified", count: 19 },
        { status: "Not certified", count: 2 },
        { status: "Rejected", count: 9 },
        { status: "Terminated", count: 0 },
        { status: "Withdrawn", count: 1 },
      ],
    },

    ssOverview: {
      openBatches: 1,
      universityGroups: 6,
      seatsLeft: 42,
      applicationsToScreen: 12,
      activeTrainees: 138,
      trainersPending: 3,
      stipendsReady: 21,
      certified: 64,
      funnel: [
        { status: "Submitted", count: 12 },
        { status: "Shortlisted", count: 8 },
        { status: "Selected", count: 150 },
        { status: "Rejected", count: 17 },
        { status: "Withdrawn", count: 3 },
      ],
    },

    myInterns: [
      { id: 101, student: person(11, "Nusrat Jahan"), batchName: "2026 Batch 2", status: "Active", department: "Research", mentor: null, startDate: d(-60), endDate: d(30), attendancePct: 94, belowMin: false, pendingLeave: 1, reportSubmitted: false, monthlyToEvaluate: 1 },
      { id: 102, student: person(12, "Rafiq Islam", "BUET"), batchName: "2026 Batch 2", status: "Active", department: "Research", mentor: null, startDate: d(-60), endDate: d(30), attendancePct: 71, belowMin: true, pendingLeave: 0, reportSubmitted: false, monthlyToEvaluate: 1 },
      { id: 103, student: person(13, "Farzana Rahman", "JU"), batchName: "2026 Batch 1", status: "Active", department: "Research", mentor: null, startDate: d(-150), endDate: d(-2), attendancePct: 88, belowMin: false, pendingLeave: 0, reportSubmitted: true, monthlyToEvaluate: 0 },
    ],

    reviewQueue: [
      { id: 201, fullName: "Tanvir Hasan", role: "Student", university: "University of Dhaka", documentsToReview: 3, waitingSince: ago(5) },
      { id: 202, fullName: "Mehedi Hasan", role: "Trainer", university: "RUET", documentsToReview: 2, waitingSince: ago(26) },
      { id: 203, fullName: "Sadia Afrin", role: "Student", university: "Jahangirnagar University", documentsToReview: 1, waitingSince: ago(52) },
    ],

    internship: {
      id: 301,
      batchName: "2026 Batch 2",
      batchCode: "UGC-INT-2026-B2",
      status: "Active",
      appliedAt: subDays(t, 120).toISOString(),
      exam: { mode: "Written", date: d(-90), score: 78 },
      rejectionReason: null,
      internship: {
        department: { id: 1, name: "Research" },
        mentor: { id: 5, name: "Dr. Mahfuza Khatun", designation: "Deputy Director" },
        startDate: d(-60),
        endDate: d(30),
        attendance: { required: 40, present: 37, approved: 1, unapproved: 2, pct: 92, belowMin: false },
        today: { date: today, inPeriod: true, record: null },
        bills: [
          { month: month(2), status: "Paid", net: 15000 },
          { month: month(1), status: "Ready to pay", net: 14500 },
          { month: month(0), status: "Accruing", net: null },
        ],
        leaveRequests: [{ date: d(3), reason: "Exam", status: "Pending" }],
        reportDeadline: d(40),
        report: null,
        certificate: null,
        outcome: null,
      },
    },

    softskill: {
      application: { batchName: "2026 Soft Skill Batch 1", university: "University of Dhaka", status: "Selected", appliedAt: subDays(t, 70).toISOString(), batchStatus: "Running", rejectionReason: null },
      trainee: {
        status: "Active",
        startDate: d(-45),
        endDate: d(45),
        attendance: { heldSessions: 10, heldHours: 20, attendedHours: 18, present: 9, leave: 1, absent: 0, pct: 90, belowMin: false },
        sessions: [
          { session: { status: "Held", date: d(-3), startTime: "10:00", topic: "Teamwork" } },
          { session: { status: "Scheduled", date: d(2), startTime: "10:00", topic: "Presentation skills" } },
          { session: { status: "Scheduled", date: d(9), startTime: "10:00", topic: "Negotiation" } },
        ],
        assessments: [{ passed: true }, { passed: true }, { passed: null }],
        stipends: [
          { status: "Paid", amount: 3000 },
          { status: "Ready to pay", amount: 3000 },
          { status: "Awaiting assessment", amount: 3000 },
        ],
        reportDeadline: d(10),
        report: null,
        certificate: null,
      },
    },

    trainer: {
      registration: { status: "Approved", totCompleted: true },
      eligibility: { eligible: true },
      universityGroups: [
        { id: 401, university: { name: "Rajshahi University of Engineering & Technology", shortName: "RUET" } },
        { id: 402, university: { name: "Chittagong University of Engineering & Technology", shortName: "CUET" } },
      ],
      upcomingSessions: [
        { id: 501, universityGroupId: 401, topic: "Communication skills", date: today, startTime: "10:00", endTime: "12:00" },
        { id: 502, universityGroupId: 402, topic: "Critical thinking", date: d(2), startTime: "14:00", endTime: "16:00" },
        { id: 503, universityGroupId: 401, topic: "Time management", date: d(7), startTime: "10:00", endTime: "12:00" },
      ],
    },

    visits: [
      { id: 601, university: "Khulna University", batchCode: "SS-2026-B1", session: "Teamwork", plannedOn: d(-2), status: "Planned" },
      { id: 602, university: "RUET", batchCode: "SS-2026-B1", session: "Presentation skills", plannedOn: d(1), status: "Planned" },
      { id: 603, university: "CUET", batchCode: "SS-2026-B1", session: null, plannedOn: d(8), status: "Planned" },
      { id: 604, university: "University of Dhaka", batchCode: "SS-2026-B1", session: null, plannedOn: d(-20), status: "Submitted" },
    ],

    honoraria: [
      { status: "Paid", amount: 12000 },
      { status: "Paid", amount: 6000 },
      { status: "Due", amount: 9000 },
      { status: "Rate not set", amount: 0 },
    ],

    requirements: [
      {
        module: "internship",
        moduleName: "UGC Internship",
        ready: false,
        rows: [{ required: true, met: true }, { required: true, met: true }, { required: true, met: false }, { required: true, met: false }],
        missing: ["Final-year transcript", "Recommendation letter"],
      },
      { module: "softskill", moduleName: "Soft Skill Training", ready: true, rows: [{ required: true, met: true }, { required: true, met: true }], missing: [] },
    ],


    internsAll: sampleInterns(d),
    bills: sampleBills(t),

    batches: [
      { id: 1, name: "2026 Batch 2", status: "Running", seats: { capacity: 20, taken: 20, left: 0 } },
      { id: 2, name: "2027 Batch 1", status: "Selection", seats: { capacity: 20, taken: 11, left: 9 } },
      { id: 3, name: "2027 Batch 2", status: "Applications open", seats: { capacity: 20, taken: 3, left: 17 } },
      { id: 4, name: "2026 Batch 1", status: "Completed", seats: { capacity: 20, taken: 19, left: 1 } },
    ],

    ssBatches: [
      {
        id: 1,
        status: "Running",
        universityGroups: [
          ["DU", 30, 28],
          ["BUET", 30, 30],
          ["RUET", 30, 22],
          ["CUET", 30, 25],
          ["KU", 30, 17],
          ["JU", 30, 16],
        ].map(([shortName, capacity, taken], i) => ({ id: 700 + i, batchCode: "SS-2026-B1", university: { name: shortName, shortName }, capacity, taken })),
      },
    ],
  };
}

const DEPARTMENTS = ["Research", "Science & Technology", "Finance", "Administration", "Quality Assurance", "Planning"];
const PCTS = [96, 93, 91, 88, 85, 82, 99, 94, 79, 71, 90, 87, 92, 95, 84, 76, 98, 89, 91, 86, 93, 78, 97, 83, 90, 92, 81];

/** 27 active interns spread over the departments, with a realistic attendance mix. */
function sampleInterns(d) {
  return PCTS.map((pct, i) => ({
    id: 800 + i,
    student: { id: 900 + i, name: `Intern ${i + 1}`, email: null, university: "DU" },
    batchName: "2026 Batch 2",
    status: "Active",
    department: DEPARTMENTS[[0, 0, 0, 1, 1, 2, 0, 3, 1, 4, 2, 0, 5, 1, 3, 0, 2, 4, 1, 0, 5, 3, 0, 2, 1, 0, 4][i]],
    mentor: null,
    startDate: d(-60),
    endDate: d(30),
    attendancePct: pct,
    belowMin: pct < 80,
    pendingLeave: 0,
    reportSubmitted: false,
    monthlyToEvaluate: 0,
  }));
}

/** Six months of allowance: older months paid, the last two partly unpaid. */
function sampleBills(t) {
  const plan = [
    [6, 14, 14],
    [5, 18, 18],
    [4, 20, 20],
    [3, 24, 24],
    [2, 27, 19],
    [1, 27, 4],
  ];
  return plan.flatMap(([back, interns, paid]) =>
    Array.from({ length: interns }, (_, i) => ({
      applicationId: 1000 + i,
      month: format(subDays(t, 30 * back), "yyyy-MM"),
      gross: 20000,
      net: i % 5 === 0 ? 18500 : 20000,
      status: i < paid ? "Paid" : "Ready to pay",
    }))
  );
}
