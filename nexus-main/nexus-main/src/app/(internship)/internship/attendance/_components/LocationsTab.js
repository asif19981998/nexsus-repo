"use client";

import { useState } from "react";
import { Button, ErrorState, Input } from "@/components/ui";
import { useAction, useApiQuery } from "@/hooks/useApi";
import { api } from "@/lib/api";

export function LocationsTab() {
  const list = useApiQuery("/attendance/admin/departments");
  if (list.error) return <ErrorState title="Could not load departments" description={list.error.message} action={<Button onClick={list.reload}>Try again</Button>} />;
  if (!list.data) return <p className="text-sm text-secondary">Loading…</p>;

  return (
    <div className="space-y-4">
      <p className="text-sm text-secondary">
        An intern&apos;s check-in and check-out count only within the radius of their department&apos;s location. Clear both coordinates to switch the location check off for a department.
      </p>
      <div className="space-y-3">
        {list.data.map((d) => (
          <DepartmentRow key={d.id} dept={d} onSaved={list.reload} />
        ))}
      </div>
    </div>
  );
}

function DepartmentRow({ dept, onSaved }) {
  const { run, busy } = useAction();
  const [f, setF] = useState({
    latitude: dept.latitude ?? "",
    longitude: dept.longitude ?? "",
    radiusMeters: dept.radiusMeters ?? 200,
  });
  const num = (v) => (v === "" || v == null ? null : Number(v));
  const save = async () => {
    const ok = await run(
      () => api.put(`/attendance/admin/departments/${dept.id}/location`, { latitude: num(f.latitude), longitude: num(f.longitude), radiusMeters: num(f.radiusMeters) }),
      `${dept.name} saved`
    );
    if (ok) onSaved();
  };
  return (
    <div className="grid gap-3 rounded-xl border border-slate-200 p-3 sm:grid-cols-[1.4fr_1fr_1fr_1fr_auto] sm:items-end dark:border-slate-700">
      <p className="font-medium sm:pb-2">{dept.name}</p>
      <Input label="Latitude" type="number" step="any" value={f.latitude} onChange={(e) => setF({ ...f, latitude: e.target.value })} />
      <Input label="Longitude" type="number" step="any" value={f.longitude} onChange={(e) => setF({ ...f, longitude: e.target.value })} />
      <Input label="Radius (m)" type="number" min="20" max="5000" value={f.radiusMeters} onChange={(e) => setF({ ...f, radiusMeters: e.target.value })} />
      <Button variant="outline" disabled={busy} onClick={save}>
        Save
      </Button>
    </div>
  );
}
