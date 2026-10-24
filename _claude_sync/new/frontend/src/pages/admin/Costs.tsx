import { useState } from 'react';
import { Building2, CalendarRange, ChefHat, CalendarOff, Receipt, UserRound, Wallet } from 'lucide-react';
import { api } from '../../api/client';
import type { DailyCost, MealType } from '../../api/types';
import {
  Button, Card, Checkbox, Empty, ErrorBox, Field, Input, Modal, PageHeader, Pill, Select, Spinner, Stat,
  useAsync, useToast,
} from '../../components/ui';
import { fmtDayShort, fmtMoney, monthEndIso, monthStartIso, todayIso } from '../../utils/format';

type Draft = { total: string; employee: string; remarks: string };

export default function Costs() {
  const toast = useToast();
  const [month, setMonth] = useState(todayIso().slice(0, 7));
  const [mealTypeId, setMealTypeId] = useState('');
  const [drafts, setDrafts] = useState<Record<string, Draft>>({});
  const [saving, setSaving] = useState<string | null>(null);
  const [bulkOpen, setBulkOpen] = useState(false);

  const from = monthStartIso(`${month}-01`);
  const to = monthEndIso(`${month}-01`);
  const types = useAsync(() => api.get<MealType[]>('/api/meal-types'), []);
  const list = useAsync(() => api.get<DailyCost[]>('/api/costs', { from, to, mealTypeId }), [from, to, mealTypeId]);
  const rows = list.data ?? [];

  const key = (r: DailyCost) => `${r.mealDate}-${r.mealTypeId}`;
  const draftOf = (r: DailyCost): Draft => drafts[key(r)] ?? { total: String(r.totalCost), employee: String(r.employeeCost), remarks: r.remarks ?? '' };
  const isDirty = (r: DailyCost) => {
    const d = drafts[key(r)];
    return !!d && (Number(d.total) !== r.totalCost || Number(d.employee) !== r.employeeCost || d.remarks !== (r.remarks ?? '') || r.isDefault);
  };
  const setDraft = (r: DailyCost, patch: Partial<Draft>) => setDrafts((all) => ({ ...all, [key(r)]: { ...draftOf(r), ...patch } }));

  async function save(r: DailyCost) {
    const d = draftOf(r);
    const total = Number(d.total);
    const employee = Number(d.employee);
    if (isNaN(total) || isNaN(employee) || total < 0 || employee < 0) return toast.error('Enter valid amounts');
    if (employee > total) return toast.error('Employee portion cannot be more than the total cost');
    setSaving(key(r));
    try {
      await api.put('/api/costs', { mealDate: r.mealDate, mealTypeId: r.mealTypeId, totalCost: total, employeeCost: employee, remarks: d.remarks || null });
      setDrafts((all) => { const n = { ...all }; delete n[key(r)]; return n; });
      toast.success(`Cost saved for ${fmtDayShort(r.mealDate)}`);
      list.reload();
    } catch (e) {
      toast.error((e as Error).message);
    } finally {
      setSaving(null);
    }
  }

  async function revert(r: DailyCost) {
    if (!r.mealDailyCostId) return;
    try {
      await api.del(`/api/costs/${r.mealDailyCostId}`);
      toast.success('Reverted to default cost');
      list.reload();
    } catch (e) {
      toast.error((e as Error).message);
    }
  }

  const sum = (f: (r: DailyCost) => number) => rows.reduce((a, r) => a + f(r), 0);
  const missing = rows.filter((r) => r.isServed && r.isDefault && r.totalCost === 0).length;

  return (
    <>
      <PageHeader
        title="Daily Cost"
        icon={Wallet}
        tone="mint"
        subtitle="Cost per meal for each day, split into employee and company contribution"
        actions={<Button onClick={() => setBulkOpen(true)}><CalendarRange size={17} /> Apply cost to a period</Button>}
      />
      <div className="filters">
        <Field label="Month"><Input type="month" value={month} onChange={(e) => e.target.value && setMonth(e.target.value)} /></Field>
        <Field label="Meal">
          <Select value={mealTypeId} onChange={(e) => setMealTypeId(e.target.value)}>
            <option value="">All meals</option>
            {(types.data ?? []).map((t) => <option key={t.mealTypeId} value={t.mealTypeId}>{t.name}</option>)}
          </Select>
        </Field>
      </div>

      {list.error && <ErrorBox message={list.error} onRetry={list.reload} />}
      {list.loading && !list.data ? <Spinner /> : (
        <div className="stack">
          <div className="grid grid-4">
            <Stat label="Meals served" icon={ChefHat} chip="warm" value={sum((r) => r.mealCount)} tone="accent" />
            <Stat label="Total cost" icon={Receipt} chip="violet" value={fmtMoney(sum((r) => r.dayTotal))} />
            <Stat label="Employee contribution" icon={UserRound} chip="sky" value={fmtMoney(sum((r) => r.dayEmployeeTotal))} tone="primary" />
            <Stat label="Company contribution" icon={Building2} chip="mint" value={fmtMoney(sum((r) => r.dayCompanyTotal))} />
          </div>
          {missing > 0 && <div className="alert alert-warn">{missing} served day(s) have no cost set (৳0). Enter the cost or set a default on the meal type.</div>}
          <Card>
            {rows.length === 0 ? <Empty title="No meal days in this month" icon={CalendarOff} /> : (
              <div className="table-wrap">
                <table className="table">
                  <thead>
                    <tr>
                      <th>Date</th><th>Meal</th><th className="num">Meals</th><th>Cost / meal</th><th>Employee / meal</th>
                      <th className="num">Company / meal</th><th className="num">Day total</th><th>Remarks</th><th className="actions" />
                    </tr>
                  </thead>
                  <tbody>
                    {rows.map((r) => {
                      const d = draftOf(r);
                      const company = Math.max(0, Number(d.total || 0) - Number(d.employee || 0));
                      return (
                        <tr key={key(r)}>
                          <td className="nowrap">
                            {fmtDayShort(r.mealDate)}
                            {!r.isServed && <span className="cell-sub">{r.notServedReason}</span>}
                          </td>
                          <td>{r.mealTypeName}{r.isDefault && <> <Pill tone="muted">default</Pill></>}</td>
                          <td className="num">{r.mealCount}</td>
                          <td style={{ minWidth: 110 }}>
                            <Input type="number" min={0} step="0.01" value={d.total} aria-label="Cost per meal"
                              onChange={(e) => setDraft(r, { total: e.target.value })} />
                          </td>
                          <td style={{ minWidth: 110 }}>
                            <Input type="number" min={0} step="0.01" value={d.employee} aria-label="Employee portion"
                              onChange={(e) => setDraft(r, { employee: e.target.value })} />
                          </td>
                          <td className="num">{fmtMoney(company)}</td>
                          <td className="num">{fmtMoney(r.dayTotal)}</td>
                          <td style={{ minWidth: 140 }}>
                            <Input value={d.remarks} aria-label="Remarks" onChange={(e) => setDraft(r, { remarks: e.target.value })} />
                          </td>
                          <td className="actions">
                            <Button size="sm" variant={isDirty(r) ? 'primary' : 'ghost'} disabled={!isDirty(r)} loading={saving === key(r)} onClick={() => save(r)}>Save</Button>
                            {r.mealDailyCostId && <Button size="sm" variant="text" onClick={() => revert(r)}>Use default</Button>}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                  <tfoot>
                    <tr>
                      <td colSpan={2}>Total</td>
                      <td className="num">{sum((r) => r.mealCount)}</td>
                      <td colSpan={3} />
                      <td className="num">{fmtMoney(sum((r) => r.dayTotal))}</td>
                      <td colSpan={2} />
                    </tr>
                  </tfoot>
                </table>
              </div>
            )}
          </Card>
        </div>
      )}
      {bulkOpen && (
        <BulkCostModal
          types={types.data ?? []} defaultFrom={from} defaultTo={to}
          onClose={() => setBulkOpen(false)} onDone={() => { setBulkOpen(false); setDrafts({}); list.reload(); }}
        />
      )}
    </>
  );
}

function BulkCostModal({ types, defaultFrom, defaultTo, onClose, onDone }: {
  types: MealType[]; defaultFrom: string; defaultTo: string; onClose: () => void; onDone: () => void;
}) {
  const toast = useToast();
  const [mealTypeId, setType] = useState(types.find((t) => t.isRegular)?.mealTypeId ?? types[0]?.mealTypeId ?? 0);
  const [from, setFrom] = useState(defaultFrom);
  const [to, setTo] = useState(defaultTo);
  const [total, setTotal] = useState('');
  const [employee, setEmployee] = useState('');
  const [servedOnly, setServedOnly] = useState(true);
  const [overwrite, setOverwrite] = useState(false);
  const [busy, setBusy] = useState(false);

  async function submit() {
    const t = Number(total), e = Number(employee);
    if (!total || isNaN(t) || isNaN(e) || e > t) return toast.error('Enter a total cost and an employee portion not more than the total');
    setBusy(true);
    try {
      const res = await api.post<{ saved: number }>('/api/costs/bulk', {
        from, to, mealTypeId, totalCost: t, employeeCost: e, servedDaysOnly: servedOnly, overwriteExisting: overwrite,
      });
      toast.success(`Cost set for ${res.saved} day(s)`);
      onDone();
    } catch (err) {
      toast.error((err as Error).message);
    } finally {
      setBusy(false);
    }
  }

  const company = Math.max(0, Number(total || 0) - Number(employee || 0));
  return (
    <Modal open title="Apply cost to a period" onClose={onClose}
      footer={<><Button variant="ghost" onClick={onClose}>Cancel</Button><Button loading={busy} onClick={submit}>Apply</Button></>}>
      <div className="stack">
        <Field label="Meal">
          <Select value={mealTypeId} onChange={(e) => setType(Number(e.target.value))}>
            {types.map((t) => <option key={t.mealTypeId} value={t.mealTypeId}>{t.name}</option>)}
          </Select>
        </Field>
        <div className="form-grid">
          <Field label="From"><Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} /></Field>
          <Field label="To"><Input type="date" value={to} onChange={(e) => setTo(e.target.value)} /></Field>
          <Field label="Cost per meal (৳)"><Input type="number" min={0} step="0.01" value={total} onChange={(e) => setTotal(e.target.value)} /></Field>
          <Field label="Employee portion (৳)" hint={`Company pays ${fmtMoney(company)}`}><Input type="number" min={0} step="0.01" value={employee} onChange={(e) => setEmployee(e.target.value)} /></Field>
        </div>
        <Checkbox label="Only days the meal is served" checked={servedOnly} onChange={setServedOnly} />
        <Checkbox label="Overwrite days that already have a cost" checked={overwrite} onChange={setOverwrite} />
      </div>
    </Modal>
  );
}
