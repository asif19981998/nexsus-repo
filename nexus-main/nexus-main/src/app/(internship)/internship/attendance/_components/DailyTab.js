"use client";

import { useMemo, useState } from "react";
import { PencilLine } from "lucide-react";
import { Badge, Button, DataTable, ErrorState, Input, Modal, Select, Tooltip } from "@/components/ui";
import { useSetup } from "@/components/layout/InternshipShell";
import { useAction, useApiQuery, useSession } from "@/hooks/useApi";
import { API_URL, api, qs } from "@/lib/api";
import { DAY_TONE, P } from "@/lib/internshipPermissions";
import { formatDate } from "@/lib/formatters";

const TONE = { ...DAY_TONE, off: "slate" };

const todayIso = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
};

export const minutes = (m) => (m == null ? "—" : `${Math.floor(m / 60)}h ${String(m % 60).padStart(2, "0")}m`);

/** A short duration for lateness: "45m", "2h", "12h 55m". */
export const duration = (m) => {
  const h = Math.floor(m / 60);
  const rest = m % 60;
  return h === 0 ? `${rest}m` : rest === 0 ? `${h}h` : `${h}h ${rest}m`;
};

/** Shown on a day an admin set by hand (times typed in, or marked absent), so it is not mistaken for a real punch. */
export function ManualTag({ row }) {
  if (!row.manual) return null;
  const who = row.editedBy ? `Edited by ${row.editedBy}` : "Edited by admin";
  const why = row.reason || row.note;
  return (
    <Tooltip
      content={
        <span className="block">
          <span className="block font-semibold">{who}</span>
          {why ? <span className="block max-w-56 whitespace-normal opacity-80">{why}</span> : null}
        </span>
      }
    >
      <span tabIndex={0} className="ml-1.5 inline-flex align-middle text-amber-600">
        <PencilLine className="h-3.5 w-3.5" aria-hidden />
        <span className="sr-only">{who}</span>
      </span>
    </Tooltip>
  );
}

export function Photo({ path, label }) {
  if (!path) return null;
  const src = `${API_URL}${path}`;
  return (
    <a href={src} target="_blank" rel="noreferrer" title={label}>
      {/* eslint-disable-next-line @next/next/no-img-element */}
      <img src={src} alt={label} className="h-10 w-10 rounded-lg object-cover ring-1 ring-slate-200" />
    </a>
  );
}

/** What the caller may change in the register. Times are admin-only on the API; absences and leave need attendance.manage. */
export function useAttendanceRights() {
  const { can, user } = useSession();
  const manage = can(P.attendanceManage);
  return { editTimes: manage && user?.role === "Admin", markAbsent: manage };
}

export function DailyTab() {
  const { departments } = useSetup();
  const rights = useAttendanceRights();
  const [date, setDate] = useState(todayIso());
  const [departmentId, setDepartmentId] = useState("all");
  const [search, setSearch] = useState("");
  const [editing, setEditing] = useState(null); // { row, mode: "times" | "absent" }
  const day = useApiQuery(`/attendance/admin/day${qs({ date, departmentId })}`);

  const rows = useMemo(() => {
    const q = search.trim().toLowerCase();
    // DataTable keys its rows by `id`.
    return (day.data?.rows || [])
      .map((r) => ({ ...r, id: r.applicationId }))
      .filter((r) => !q || r.name.toLowerCase().includes(q) || r.email.toLowerCase().includes(q));
  }, [day.data, search]);

  if (day.error) return <ErrorState title="Could not load attendance" description={day.error.message} action={<Button onClick={day.reload}>Try again</Button>} />;
  const d = day.data;
  const working = d?.dayType === "WORKING";

  return (
    <div className="space-y-4">
      <div className="grid gap-3 sm:grid-cols-[1fr_1fr_2fr]">
        <Input label="Date" type="date" value={date} onChange={(e) => e.target.value && setDate(e.target.value)} />
        <Select
          label="Department"
          value={departmentId}
          onChange={(e) => setDepartmentId(e.target.value)}
          options={[{ value: "all", label: "All departments" }, ...departments.map((x) => ({ value: String(x.id), label: x.name }))]}
        />
        <Input label="Search" placeholder="Name or email" value={search} onChange={(e) => setSearch(e.target.value)} />
      </div>

      {d ? (
        <div className="flex flex-wrap items-center gap-2 text-sm">
          <span className="font-medium">{formatDate(d.date, "EEEE, dd MMM yyyy")}</span>
          {!working ? <Badge tone="blue">{d.holidayName || "Weekly off"}</Badge> : null}
          <Badge tone="green">Present {d.present}</Badge>
          <Badge tone="red">Absent {d.absent}</Badge>
          <Badge tone="blue">Leave {d.leave}</Badge>
          {d.leaveRequested ? <Badge tone="amber">Leave requests {d.leaveRequested}</Badge> : null}
          {working ? <Badge tone="slate">Not signed in {d.notSignedIn}</Badge> : null}
          <span className="text-secondary">of {d.total} active interns</span>
        </div>
      ) : null}

      <DataTable
        emptyMessage={d ? "No active interns for this day." : "Loading…"}
        rows={rows}
        columns={[
          {
            key: "who",
            label: "Intern",
            render: (r) => (
              <div>
                <p className="font-medium">{r.name}</p>
                <p className="text-xs text-secondary">{r.department}</p>
              </div>
            ),
          },
          {
            key: "status",
            label: "Status",
            render: (r) => (
              <div className="flex items-center">
                <Badge tone={TONE[r.kind] || "slate"}>{r.label}</Badge>
                <ManualTag row={r} />
              </div>
            ),
          },
          {
            key: "in",
            label: "Check in",
            render: (r) => (
              <div>
                <p>{r.checkIn || "—"}</p>
                {r.lateMinutes > 0 ? <p className="text-xs text-danger">{duration(r.lateMinutes)} late</p> : r.checkIn ? <p className="text-xs text-secondary">On time</p> : null}
              </div>
            ),
          },
          { key: "out", label: "Check out", render: (r) => r.checkOut || "—" },
          { key: "worked", label: "Worked", render: (r) => minutes(r.workedMinutes) },
          {
            key: "photos",
            label: "Photos",
            render: (r) =>
              r.checkInPhoto || r.checkOutPhoto ? (
                <div className="flex gap-1.5">
                  <Photo path={r.checkInPhoto} label={`${r.name} check in`} />
                  <Photo path={r.checkOutPhoto} label={`${r.name} check out`} />
                </div>
              ) : (
                "—"
              ),
          },
          {
            key: "act",
            label: "",
            render: (r) =>
              (working || r.kind !== "off") && rights.markAbsent ? (
                <div className="flex flex-wrap gap-2">
                  {r.kind === "leave-requested" ? <LeaveButtons row={r} date={d.date} onDone={day.reload} /> : null}
                  {rights.editTimes ? (
                    <Button size="sm" variant="outline" onClick={() => setEditing({ row: r, mode: "times" })}>
                      Edit times
                    </Button>
                  ) : null}
                  <Button size="sm" variant="ghost" onClick={() => setEditing({ row: r, mode: "absent" })}>
                    Mark absent
                  </Button>
                </div>
              ) : null,
          },
        ]}
      />

      {editing?.mode === "times" ? <TimesModal row={editing.row} date={d.date} onClose={() => setEditing(null)} onDone={day.reload} /> : null}
      {editing?.mode === "absent" ? <AbsentModal row={editing.row} date={d.date} onClose={() => setEditing(null)} onDone={day.reload} /> : null}
    </div>
  );
}

function LeaveButtons({ row, date, onDone }) {
  const { run, busy } = useAction();
  const decide = async (approve) => {
    const ok = await run(() => api.post(`/internship/interns/${row.applicationId}/leave/decide`, { date, approve }), approve ? "Leave approved" : "Leave rejected");
    if (ok) onDone();
  };
  return (
    <>
      <Button size="sm" disabled={busy} onClick={() => decide(true)}>
        Approve leave
      </Button>
      <Button size="sm" variant="danger" disabled={busy} onClick={() => decide(false)}>
        Reject
      </Button>
    </>
  );
}

export function TimesModal({ row, date, onClose, onDone }) {
  const { run, busy } = useAction();
  const [f, setF] = useState({ checkIn: row.checkIn || "", checkOut: row.checkOut || "", reason: "" });
  const save = async () => {
    const ok = await run(
      () => api.post(`/internship/interns/${row.applicationId}/attendance/times`, { date, checkIn: f.checkIn, checkOut: f.checkOut || null, reason: f.reason }),
      "Times saved"
    );
    if (ok) {
      onDone();
      onClose();
    }
  };
  return (
    <Modal open onClose={onClose} title={`Edit times – ${row.name}`} description={`${formatDate(date)} · Bangladesh time`}>
      <div className="space-y-3">
        <div className="grid gap-3 sm:grid-cols-2">
          <Input label="Check in" type="time" value={f.checkIn} onChange={(e) => setF({ ...f, checkIn: e.target.value })} />
          <Input label="Check out" type="time" value={f.checkOut} onChange={(e) => setF({ ...f, checkOut: e.target.value })} hint="Leave empty if not signed out" />
        </div>
        <Input label="Reason" value={f.reason} onChange={(e) => setF({ ...f, reason: e.target.value })} hint="Recorded in the audit log" />
        <div className="flex justify-end gap-2">
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button disabled={busy || !f.checkIn || f.reason.trim().length < 5} onClick={save}>
            Save
          </Button>
        </div>
      </div>
    </Modal>
  );
}

export function AbsentModal({ row, date, onClose, onDone }) {
  const { run, busy } = useAction();
  const [f, setF] = useState({ approved: "no", reason: "" });
  const save = async () => {
    const ok = await run(
      () => api.post(`/internship/interns/${row.applicationId}/absence`, { date, approved: f.approved === "yes", reason: f.reason }),
      "Absence recorded"
    );
    if (ok) {
      onDone();
      onClose();
    }
  };
  return (
    <Modal open onClose={onClose} title={`Mark absent – ${row.name}`} description={formatDate(date)}>
      <div className="space-y-3">
        <Select
          label="Absence"
          value={f.approved}
          onChange={(e) => setF({ ...f, approved: e.target.value })}
          options={[
            { value: "no", label: "Unapproved (deduct)" },
            { value: "yes", label: "Approved (no deduction)" },
          ]}
        />
        <Input label="Reason" value={f.reason} onChange={(e) => setF({ ...f, reason: e.target.value })} />
        <div className="flex justify-end gap-2">
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="danger" disabled={busy} onClick={save}>
            Mark absent
          </Button>
        </div>
      </div>
    </Modal>
  );
}
