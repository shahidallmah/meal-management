import { useState, type FormEvent } from 'react';
import { CalendarHeart, CalendarOff, Plus, Soup } from 'lucide-react';
import { api } from '../../api/client';
import type { MealType, Occasion } from '../../api/types';
import {
  Button, Card, Checkbox, Empty, ErrorBox, Field, Input, Modal, PageHeader, Pill, Select, Spinner,
  useAsync, useConfirm, useToast,
} from '../../components/ui';
import { addDays, fmtDate, fmtMoney, parseDate, todayIso } from '../../utils/format';

type TypeForm = Omit<MealType, 'mealTypeId'> & { mealTypeId: number | null };
type OccForm = { id: number | null; mealTypeId: number; occasionDate: string; title: string; autoOnForAll: boolean; isActive: boolean };

export default function MealTypes() {
  const toast = useToast();
  const confirm = useConfirm();
  const types = useAsync(() => api.get<MealType[]>('/api/meal-types'), []);
  const occasions = useAsync(() => api.get<Occasion[]>('/api/occasions', { from: addDays(todayIso(), -60) }), []);
  const [typeForm, setTypeForm] = useState<TypeForm | null>(null);
  const [occForm, setOccForm] = useState<OccForm | null>(null);
  const [busy, setBusy] = useState(false);
  const occasional = (types.data ?? []).filter((t) => !t.isRegular && t.isActive);

  async function saveType(e: FormEvent) {
    e.preventDefault();
    if (!typeForm) return;
    setBusy(true);
    try {
      const { mealTypeId, ...body } = typeForm;
      if (mealTypeId) await api.put(`/api/meal-types/${mealTypeId}`, body);
      else await api.post('/api/meal-types', body);
      toast.success('Meal type saved');
      setTypeForm(null);
      types.reload();
    } catch (err) {
      toast.error((err as Error).message);
    } finally {
      setBusy(false);
    }
  }

  async function saveOcc(e: FormEvent) {
    e.preventDefault();
    if (!occForm) return;
    setBusy(true);
    try {
      const { id, ...body } = occForm;
      if (id) await api.put(`/api/occasions/${id}`, body);
      else await api.post('/api/occasions', body);
      toast.success('Occasion saved');
      setOccForm(null);
      occasions.reload();
    } catch (err) {
      toast.error((err as Error).message);
    } finally {
      setBusy(false);
    }
  }

  async function deleteOcc(o: Occasion) {
    const ok = await confirm({ title: 'Delete occasion?', message: `${o.title} on ${fmtDate(o.occasionDate)}`, confirmText: 'Delete', danger: true });
    if (!ok) return;
    try {
      await api.del(`/api/occasions/${o.mealOccasionId}`);
      toast.success('Deleted');
      occasions.reload();
    } catch (err) {
      toast.error((err as Error).message);
    }
  }

  return (
    <>
      <PageHeader icon={Soup} tone="warm" title="Meals & Occasions" subtitle="Regular meals are served every working day. Occasional meals only on scheduled dates." />
      <div className="stack">
        <Card
          title="Meal types"
          icon={Soup}
          actions={<Button size="sm" onClick={() => setTypeForm({ mealTypeId: null, code: '', name: '', isRegular: false, defaultTotalCost: 0, defaultEmployeeCost: 0, sortOrder: 10, isActive: true })}><Plus size={15} /> Add meal type</Button>}
        >
          {types.error && <ErrorBox message={types.error} />}
          {!types.data ? <Spinner /> : (
            <div className="table-wrap">
              <table className="table">
                <thead><tr><th>Meal</th><th>Kind</th><th className="num">Default cost</th><th className="num">Employee</th><th className="num">Company</th><th>Status</th><th className="actions" /></tr></thead>
                <tbody>
                  {types.data.map((t) => (
                    <tr key={t.mealTypeId}>
                      <td><b>{t.name}</b><span className="cell-sub">{t.code}</span></td>
                      <td>{t.isRegular ? <Pill tone="info">Regular · daily</Pill> : <Pill tone="muted">Occasional</Pill>}</td>
                      <td className="num">{fmtMoney(t.defaultTotalCost)}</td>
                      <td className="num">{fmtMoney(t.defaultEmployeeCost)}</td>
                      <td className="num">{fmtMoney(t.defaultTotalCost - t.defaultEmployeeCost)}</td>
                      <td>{t.isActive ? <Pill tone="on">Active</Pill> : <Pill tone="off">Inactive</Pill>}</td>
                      <td className="actions"><Button size="sm" variant="text" onClick={() => setTypeForm({ ...t })}>Edit</Button></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Card>

        <Card
          title="Occasions"
          icon={CalendarHeart}
          actions={<Button size="sm" disabled={!occasional.length}
            onClick={() => setOccForm({ id: null, mealTypeId: occasional[0]?.mealTypeId ?? 0, occasionDate: addDays(todayIso(), 1), title: '', autoOnForAll: false, isActive: true })}>
            <Plus size={15} /> Schedule occasion</Button>}
        >
          <p className="muted" style={{ marginBottom: 12 }}>
            Schedule an occasional meal (e.g. Dinner for a year-end event) on a date. Choose whether every active employee is
            marked ON automatically, or employees opt in themselves.
          </p>
          {!occasional.length && <div className="alert alert-info">Activate an occasional meal type (e.g. Dinner) first.</div>}
          {occasions.error && <ErrorBox message={occasions.error} />}
          {!occasions.data ? <Spinner /> : occasions.data.length === 0 ? <Empty title="No occasions scheduled" icon={CalendarOff} /> : (
            <div className="table-wrap">
              <table className="table">
                <thead><tr><th>Date</th><th>Meal</th><th>Title</th><th>Attendance</th><th>Status</th><th className="actions" /></tr></thead>
                <tbody>
                  {occasions.data.map((o) => (
                    <tr key={o.mealOccasionId}>
                      <td className="nowrap">{fmtDate(o.occasionDate)}<span className="cell-sub">{parseDate(o.occasionDate).toLocaleDateString('en-US', { weekday: 'long' })}</span></td>
                      <td>{o.mealTypeName}</td>
                      <td>{o.title}</td>
                      <td>{o.autoOnForAll ? <Pill tone="on">All employees ON</Pill> : <Pill tone="muted">Opt-in</Pill>}</td>
                      <td>{o.isActive ? <Pill tone="info">Active</Pill> : <Pill tone="off">Cancelled</Pill>}</td>
                      <td className="actions">
                        <Button size="sm" variant="text" onClick={() => setOccForm({ id: o.mealOccasionId, mealTypeId: o.mealTypeId, occasionDate: o.occasionDate, title: o.title, autoOnForAll: o.autoOnForAll, isActive: o.isActive })}>Edit</Button>
                        <Button size="sm" variant="text" onClick={() => deleteOcc(o)}>Delete</Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Card>
      </div>

      {typeForm && (
        <Modal open title={typeForm.mealTypeId ? 'Edit meal type' : 'Add meal type'} onClose={() => setTypeForm(null)}
          footer={<><Button variant="ghost" onClick={() => setTypeForm(null)}>Cancel</Button><Button type="submit" form="type-form" loading={busy}>Save</Button></>}>
          <form id="type-form" className="stack" onSubmit={saveType}>
            <div className="form-grid">
              <Field label="Code"><Input required value={typeForm.code} onChange={(e) => setTypeForm({ ...typeForm, code: e.target.value })} /></Field>
              <Field label="Name"><Input required value={typeForm.name} onChange={(e) => setTypeForm({ ...typeForm, name: e.target.value })} /></Field>
              <Field label="Default cost per meal (৳)"><Input type="number" min={0} step="0.01" value={typeForm.defaultTotalCost} onChange={(e) => setTypeForm({ ...typeForm, defaultTotalCost: Number(e.target.value) })} /></Field>
              <Field label="Default employee portion (৳)"><Input type="number" min={0} step="0.01" value={typeForm.defaultEmployeeCost} onChange={(e) => setTypeForm({ ...typeForm, defaultEmployeeCost: Number(e.target.value) })} /></Field>
              <Field label="Sort order"><Input type="number" value={typeForm.sortOrder} onChange={(e) => setTypeForm({ ...typeForm, sortOrder: Number(e.target.value) })} /></Field>
            </div>
            <Checkbox label="Regular meal (served every working day)" checked={typeForm.isRegular} onChange={(v) => setTypeForm({ ...typeForm, isRegular: v })} />
            <Checkbox label="Active" checked={typeForm.isActive} onChange={(v) => setTypeForm({ ...typeForm, isActive: v })} />
            <p className="muted">Default costs are used on days without a daily cost entry.</p>
          </form>
        </Modal>
      )}

      {occForm && (
        <Modal open title={occForm.id ? 'Edit occasion' : 'Schedule occasion'} onClose={() => setOccForm(null)}
          footer={<><Button variant="ghost" onClick={() => setOccForm(null)}>Cancel</Button><Button type="submit" form="occ-form" loading={busy}>Save</Button></>}>
          <form id="occ-form" className="stack" onSubmit={saveOcc}>
            <div className="form-grid">
              <Field label="Meal">
                <Select value={occForm.mealTypeId} onChange={(e) => setOccForm({ ...occForm, mealTypeId: Number(e.target.value) })}>
                  {occasional.map((t) => <option key={t.mealTypeId} value={t.mealTypeId}>{t.name}</option>)}
                </Select>
              </Field>
              <Field label="Date"><Input type="date" required value={occForm.occasionDate} onChange={(e) => setOccForm({ ...occForm, occasionDate: e.target.value })} /></Field>
              <Field label="Title" className="span-2"><Input required placeholder="e.g. Annual dinner" value={occForm.title} onChange={(e) => setOccForm({ ...occForm, title: e.target.value })} /></Field>
            </div>
            <Checkbox label="Mark every active employee ON automatically" checked={occForm.autoOnForAll} onChange={(v) => setOccForm({ ...occForm, autoOnForAll: v })} />
            <Checkbox label="Active" checked={occForm.isActive} onChange={(v) => setOccForm({ ...occForm, isActive: v })} />
          </form>
        </Modal>
      )}
    </>
  );
}
