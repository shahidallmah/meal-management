import { useState, type FormEvent } from 'react';
import { CalendarDays, CalendarX2, Plus } from 'lucide-react';
import { api } from '../../api/client';
import type { Holiday } from '../../api/types';
import { Button, Card, Empty, ErrorBox, Field, Input, Modal, PageHeader, Select, Spinner, useAsync, useConfirm, useToast } from '../../components/ui';
import { fmtDate, todayIso } from '../../utils/format';

export default function Holidays() {
  const toast = useToast();
  const confirm = useConfirm();
  const thisYear = Number(todayIso().slice(0, 4));
  const [year, setYear] = useState(thisYear);
  const list = useAsync(() => api.get<Holiday[]>('/api/holidays', { year }), [year]);
  const [form, setForm] = useState<{ id: number | null; holidayDate: string; description: string } | null>(null);
  const [busy, setBusy] = useState(false);

  async function save(e: FormEvent) {
    e.preventDefault();
    if (!form) return;
    setBusy(true);
    try {
      const body = { holidayDate: form.holidayDate, description: form.description };
      if (form.id) await api.put(`/api/holidays/${form.id}`, body);
      else await api.post('/api/holidays', body);
      toast.success('Holiday saved. Upcoming meals were updated.');
      setForm(null);
      list.reload();
    } catch (err) {
      toast.error((err as Error).message);
    } finally {
      setBusy(false);
    }
  }

  async function remove(h: Holiday) {
    if (!(await confirm({ title: 'Delete holiday?', message: `${h.description} · ${fmtDate(h.holidayDate)}`, confirmText: 'Delete', danger: true }))) return;
    try {
      await api.del(`/api/holidays/${h.holidayId}`);
      toast.success('Deleted');
      list.reload();
    } catch (err) {
      toast.error((err as Error).message);
    }
  }

  return (
    <>
      <PageHeader icon={CalendarDays} tone="rose" title="Holidays" subtitle="No regular meal is served on holidays or weekly off days (set in Settings)."
        actions={<Button onClick={() => setForm({ id: null, holidayDate: todayIso(), description: '' })}><Plus size={16} /> Add holiday</Button>} />
      <div className="filters">
        <Field label="Year">
          <Select value={year} onChange={(e) => setYear(Number(e.target.value))}>
            {[thisYear - 1, thisYear, thisYear + 1].map((y) => <option key={y} value={y}>{y}</option>)}
          </Select>
        </Field>
      </div>
      {list.error && <ErrorBox message={list.error} onRetry={list.reload} />}
      <Card>
        {!list.data ? <Spinner /> : list.data.length === 0 ? <Empty title={`No holidays for ${year}`} icon={CalendarX2} /> : (
          <div className="table-wrap">
            <table className="table">
              <thead><tr><th>Date</th><th>Day</th><th>Holiday</th><th className="actions" /></tr></thead>
              <tbody>
                {list.data.map((h) => (
                  <tr key={h.holidayId}>
                    <td className="nowrap">{fmtDate(h.holidayDate)}</td>
                    <td>{h.dayName}</td>
                    <td>{h.description}</td>
                    <td className="actions">
                      <Button size="sm" variant="text" onClick={() => setForm({ id: h.holidayId, holidayDate: h.holidayDate, description: h.description })}>Edit</Button>
                      <Button size="sm" variant="text" onClick={() => remove(h)}>Delete</Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>
      {form && (
        <Modal open title={form.id ? 'Edit holiday' : 'Add holiday'} onClose={() => setForm(null)}
          footer={<><Button variant="ghost" onClick={() => setForm(null)}>Cancel</Button><Button type="submit" form="hol-form" loading={busy}>Save</Button></>}>
          <form id="hol-form" className="stack" onSubmit={save}>
            <Field label="Date"><Input type="date" required value={form.holidayDate} onChange={(e) => setForm({ ...form, holidayDate: e.target.value })} /></Field>
            <Field label="Description"><Input required placeholder="e.g. Victory Day" value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} /></Field>
          </form>
        </Modal>
      )}
    </>
  );
}
