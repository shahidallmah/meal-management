import { useState, type FormEvent } from 'react';
import { Plus } from 'lucide-react';
import { api } from '../../api/client';
import type { NamedItem } from '../../api/types';
import { Button, Card, Checkbox, ErrorBox, Field, Input, Modal, PageHeader, Pill, Spinner, useAsync, useToast } from '../../components/ui';

function NamedList({ title, endpoint }: { title: string; endpoint: string }) {
  const toast = useToast();
  const list = useAsync(() => api.get<NamedItem[]>(endpoint), [endpoint]);
  const [form, setForm] = useState<{ id: number | null; name: string; isActive: boolean } | null>(null);
  const [busy, setBusy] = useState(false);

  async function save(e: FormEvent) {
    e.preventDefault();
    if (!form) return;
    setBusy(true);
    try {
      const body = { name: form.name, isActive: form.isActive };
      if (form.id) await api.put(`${endpoint}/${form.id}`, body);
      else await api.post(endpoint, body);
      toast.success('Saved');
      setForm(null);
      list.reload();
    } catch (err) {
      toast.error((err as Error).message);
    } finally {
      setBusy(false);
    }
  }

  const singular = title.replace(/s$/, '');
  return (
    <Card title={title} actions={<Button size="sm" onClick={() => setForm({ id: null, name: '', isActive: true })}><Plus size={15} /> Add</Button>}>
      {list.error && <ErrorBox message={list.error} />}
      {!list.data ? <Spinner /> : (
        <div className="table-wrap">
          <table className="table">
            <tbody>
              {list.data.map((d) => (
                <tr key={d.id}>
                  <td>{d.name}</td>
                  <td>{d.isActive ? <Pill tone="on">Active</Pill> : <Pill tone="off">Inactive</Pill>}</td>
                  <td className="actions"><Button size="sm" variant="text" onClick={() => setForm({ id: d.id, name: d.name, isActive: d.isActive })}>Edit</Button></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {form && (
        <Modal open title={form.id ? `Edit ${singular.toLowerCase()}` : `Add ${singular.toLowerCase()}`} onClose={() => setForm(null)}
          footer={<><Button variant="ghost" onClick={() => setForm(null)}>Cancel</Button><Button type="submit" form={`f-${endpoint}`} loading={busy}>Save</Button></>}>
          <form id={`f-${endpoint}`} className="stack" onSubmit={save}>
            <Field label="Name"><Input required value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></Field>
            <Checkbox label="Active" checked={form.isActive} onChange={(v) => setForm({ ...form, isActive: v })} />
          </form>
        </Modal>
      )}
    </Card>
  );
}

export default function MasterData() {
  return (
    <>
      <PageHeader title="Departments & Designations" subtitle="Lists used when registering employees" />
      <div className="grid grid-2">
        <NamedList title="Departments" endpoint="/api/departments" />
        <NamedList title="Designations" endpoint="/api/designations" />
      </div>
    </>
  );
}
