import { useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { api } from '../../api/client';
import type { MealBoard, MealType, NamedItem } from '../../api/types';
import {
  Button, Card, Checkbox, Empty, ErrorBox, Field, Input, MealPill, PageHeader, Pill, Select, Spinner, Stat,
  useAsync, useConfirm, useToast,
} from '../../components/ui';
import { addDays, fmtDate, fmtDateTime, fmtMoney, sourceLabel, todayIso } from '../../utils/format';

export default function MealBoardPage() {
  const [params, setParams] = useSearchParams();
  const toast = useToast();
  const confirm = useConfirm();
  const date = params.get('date') ?? addDays(todayIso(), 1);
  const [mealTypeId, setMealTypeId] = useState<number>(0);
  const [departmentId, setDept] = useState('');
  const [search, setSearch] = useState('');
  const [selected, setSelected] = useState<Set<number>>(new Set());
  const [busy, setBusy] = useState(false);

  const types = useAsync(() => api.get<MealType[]>('/api/meal-types'), []);
  const depts = useAsync(() => api.get<NamedItem[]>('/api/departments'), []);
  const activeTypes = (types.data ?? []).filter((t) => t.isActive);
  const typeId = mealTypeId || activeTypes.find((t) => t.isRegular)?.mealTypeId || activeTypes[0]?.mealTypeId || 0;

  const board = useAsync(
    () => (typeId ? api.get<MealBoard>('/api/meal-board', { date, mealTypeId: typeId, departmentId }) : Promise.resolve(null)),
    [date, typeId, departmentId],
  );

  const rows = useMemo(() => {
    const s = search.trim().toLowerCase();
    const all = board.data?.rows ?? [];
    return s ? all.filter((r) => `${r.employeeCode} ${r.employeeName} ${r.mobileNo}`.toLowerCase().includes(s)) : all;
  }, [board.data, search]);

  const setDate = (d: string) => { setParams({ date: d }); setSelected(new Set()); };
  const toggleSel = (id: number, v: boolean) => setSelected((s) => { const n = new Set(s); if (v) n.add(id); else n.delete(id); return n; });
  const allSelected = rows.length > 0 && rows.every((r) => selected.has(r.employeeId));

  async function setMeal(ids: number[], isOn: boolean) {
    if (!board.data || ids.length === 0) return;
    if (ids.length > 1) {
      const ok = await confirm({
        title: `Turn ${isOn ? 'ON' : 'OFF'} for ${ids.length} employees?`,
        message: `${board.data.mealTypeName} on ${fmtDate(date)} will be marked ${isOn ? 'ON' : 'OFF'} by admin.`,
        confirmText: isOn ? 'Turn ON' : 'Turn OFF',
      });
      if (!ok) return;
    }
    setBusy(true);
    try {
      const res = await api.post<{ updated: number }>('/api/meal-board/set', { mealDate: date, mealTypeId: typeId, isOn, employeeIds: ids });
      toast.success(`${res.updated} updated`);
      setSelected(new Set());
      board.reload();
    } catch (e) {
      toast.error((e as Error).message);
    } finally {
      setBusy(false);
    }
  }

  const b = board.data;
  return (
    <>
      <PageHeader title="Meal Board" subtitle="Who is eating on a date. Admin changes override the employee's choice." />
      <div className="filters">
        <Field label="Date">
          <div className="row" style={{ flexWrap: 'nowrap', gap: 4 }}>
            <button className="icon-btn" aria-label="Previous day" onClick={() => setDate(addDays(date, -1))}><ChevronLeft size={18} /></button>
            <Input type="date" value={date} onChange={(e) => e.target.value && setDate(e.target.value)} />
            <button className="icon-btn" aria-label="Next day" onClick={() => setDate(addDays(date, 1))}><ChevronRight size={18} /></button>
          </div>
        </Field>
        <Field label="Meal">
          <Select value={typeId} onChange={(e) => { setMealTypeId(Number(e.target.value)); setSelected(new Set()); }}>
            {activeTypes.map((t) => <option key={t.mealTypeId} value={t.mealTypeId}>{t.name}</option>)}
          </Select>
        </Field>
        <Field label="Department">
          <Select value={departmentId} onChange={(e) => setDept(e.target.value)}>
            <option value="">All departments</option>
            {(depts.data ?? []).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
          </Select>
        </Field>
        <Field label="Search">
          <Input placeholder="Code, name or mobile" value={search} onChange={(e) => setSearch(e.target.value)} />
        </Field>
      </div>

      {board.error && <ErrorBox message={board.error} onRetry={board.reload} />}
      {board.loading && !b && <Spinner />}
      {b && (
        <div className="stack">
          <div className="grid grid-4">
            <Stat label={`${b.mealTypeName} ON`} value={b.onCount} tone="accent" sub={fmtDate(b.date)} />
            <Stat label="OFF" value={b.offCount} />
            <Stat label="Cost per meal" value={fmtMoney(b.costPerMeal)} sub={`Employee ${fmtMoney(b.employeeCostPerMeal)}`} />
            <Stat label="Total cost" value={fmtMoney(b.totalCost)} sub={b.isOpenForEmployees ? `Employees can change until ${fmtDateTime(b.lockTime)}` : 'Closed for employees'} />
          </div>

          {!b.isServed && <div className="alert alert-warn">{b.mealTypeName} is not served on this date ({b.notServedReason}).</div>}

          <Card
            title={`${rows.length} employees`}
            actions={b.isServed && (
              <div className="row">
                <span className="muted">{selected.size} selected</span>
                <Button size="sm" variant="accent" disabled={!selected.size} loading={busy} onClick={() => setMeal([...selected], true)}>Turn ON</Button>
                <Button size="sm" variant="ghost" disabled={!selected.size} loading={busy} onClick={() => setMeal([...selected], false)}>Turn OFF</Button>
              </div>
            )}
          >
            {rows.length === 0 ? <Empty title="No employees found" /> : (
              <div className="table-wrap">
                <table className="table">
                  <thead>
                    <tr>
                      <th style={{ width: 36 }}>
                        <Checkbox label="" checked={allSelected} disabled={!b.isServed}
                          onChange={(v) => setSelected(v ? new Set(rows.map((r) => r.employeeId)) : new Set())} />
                      </th>
                      <th>Employee</th><th>Department</th><th>Type</th><th>Meal</th><th>Marked by</th><th className="actions">Action</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.map((r) => (
                      <tr key={r.employeeId} className={selected.has(r.employeeId) ? 'selected' : ''}>
                        <td><Checkbox label="" checked={selected.has(r.employeeId)} disabled={!b.isServed} onChange={(v) => toggleSel(r.employeeId, v)} /></td>
                        <td><b>{r.employeeName}</b><span className="cell-sub">{r.employeeCode} · {r.mobileNo}</span></td>
                        <td>{r.department}<span className="cell-sub">{r.designation}</span></td>
                        <td>{r.isEligible ? <Pill tone="info">Default</Pill> : <Pill tone="muted">Opt-in</Pill>}</td>
                        <td><MealPill on={r.isOn} /></td>
                        <td>{sourceLabel(r.source)}</td>
                        <td className="actions">
                          {b.isServed && (
                            <Button size="sm" variant={r.isOn ? 'ghost' : 'accent'} disabled={busy} onClick={() => setMeal([r.employeeId], !r.isOn)}>
                              {r.isOn ? 'Turn OFF' : 'Turn ON'}
                            </Button>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Card>
        </div>
      )}
    </>
  );
}
