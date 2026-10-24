import { useEffect, useState, type FormEvent } from 'react';
import { Plus, Pencil, CalendarCheck, KeyRound } from 'lucide-react';
import { api } from '../../api/client';
import type {
  Eligibility, Employee, EmployeeDetail, EmployeeSave, MealType, NamedItem, PagedResult,
} from '../../api/types';
import {
  Button, Card, Checkbox, Empty, ErrorBox, Field, Input, Modal, PageHeader, Pill, Select, Spinner,
  useAsync, useConfirm, useToast,
} from '../../components/ui';
import { addDays, fmtDate, fmtUtc, todayIso } from '../../utils/format';

const emptyForm: EmployeeSave = {
  employeeCode: '', employeeName: '', departmentId: 0, designationId: 0, mobileNo: '', email: '',
  status: 'Active', role: 'Employee', defaultEligible: true, eligibleFrom: addDays(todayIso(), 1), eligibleTo: '',
};

export default function Employees() {
  const toast = useToast();
  const confirm = useConfirm();
  const [search, setSearch] = useState('');
  const [q, setQ] = useState('');
  const [departmentId, setDept] = useState('');
  const [status, setStatus] = useState('Active');
  const [eligible, setEligible] = useState('');
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<{ id: number | null; form: EmployeeSave } | null>(null);
  const [detailId, setDetailId] = useState<number | null>(null);
  const [selected, setSelected] = useState<Set<number>>(new Set());
  const [bulkOpen, setBulkOpen] = useState(false);

  useEffect(() => { const t = setTimeout(() => { setQ(search); setPage(1); }, 300); return () => clearTimeout(t); }, [search]);

  const depts = useAsync(() => api.get<NamedItem[]>('/api/departments'), []);
  const desigs = useAsync(() => api.get<NamedItem[]>('/api/designations'), []);
  const types = useAsync(() => api.get<MealType[]>('/api/meal-types'), []);
  const list = useAsync(
    () => api.get<PagedResult<Employee>>('/api/employees', { search: q, departmentId, status, eligible, page, pageSize: 25 }),
    [q, departmentId, status, eligible, page],
  );

  const pages = list.data ? Math.max(1, Math.ceil(list.data.total / list.data.pageSize)) : 1;
  const items = list.data?.items ?? [];

  function openEdit(e: Employee) {
    setEditing({
      id: e.employeeId,
      form: {
        employeeCode: e.employeeCode, employeeName: e.employeeName, departmentId: e.departmentId,
        designationId: e.designationId, mobileNo: e.mobileNo, email: e.email ?? '', status: e.status, role: e.role,
      },
    });
  }

  async function resetPassword(e: Employee) {
    const ok = await confirm({
      title: 'Reset password?',
      message: `${e.employeeName} will need to create a new password using mobile no ${e.mobileNo} and employee code ${e.employeeCode}.`,
      confirmText: 'Reset password', danger: true,
    });
    if (!ok) return;
    try {
      await api.post(`/api/employees/${e.employeeId}/reset-password`);
      toast.success('Password reset');
      list.reload();
    } catch (err) {
      toast.error((err as Error).message);
    }
  }

  return (
    <>
      <PageHeader
        title="Employees"
        subtitle="Register employees, set default meal eligibility, manage access"
        actions={<>
          <Button variant="ghost" disabled={!selected.size} onClick={() => setBulkOpen(true)}>
            <CalendarCheck size={16} /> Set eligibility ({selected.size})
          </Button>
          <Button onClick={() => setEditing({ id: null, form: { ...emptyForm } })}><Plus size={16} /> Add employee</Button>
        </>}
      />
      <div className="filters">
        <Field label="Search"><Input placeholder="Code, name or mobile" value={search} onChange={(e) => setSearch(e.target.value)} /></Field>
        <Field label="Department">
          <Select value={departmentId} onChange={(e) => { setDept(e.target.value); setPage(1); }}>
            <option value="">All</option>
            {(depts.data ?? []).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
          </Select>
        </Field>
        <Field label="Status">
          <Select value={status} onChange={(e) => { setStatus(e.target.value); setPage(1); }}>
            <option value="">All</option><option value="Active">Active</option><option value="Inactive">Inactive</option>
          </Select>
        </Field>
        <Field label="Meal eligibility">
          <Select value={eligible} onChange={(e) => { setEligible(e.target.value); setPage(1); }}>
            <option value="">All</option><option value="true">Default eligible</option><option value="false">Opt-in only</option>
          </Select>
        </Field>
      </div>

      {list.error && <ErrorBox message={list.error} onRetry={list.reload} />}
      <Card>
        {list.loading && !list.data ? <Spinner /> : items.length === 0 ? (
          <Empty title="No employees found">Add an employee or change the filters.</Empty>
        ) : (
          <>
            <div className="table-wrap">
              <table className="table">
                <thead>
                  <tr>
                    <th style={{ width: 36 }}>
                      <Checkbox label="" checked={items.every((i) => selected.has(i.employeeId))}
                        onChange={(v) => setSelected((s) => { const n = new Set(s); items.forEach((i) => (v ? n.add(i.employeeId) : n.delete(i.employeeId))); return n; })} />
                    </th>
                    <th>Employee</th><th>Department</th><th>Mobile No</th><th>Meal</th><th>Status</th><th>Login</th><th className="actions" />
                  </tr>
                </thead>
                <tbody>
                  {items.map((e) => (
                    <tr key={e.employeeId} className={selected.has(e.employeeId) ? 'selected' : ''}>
                      <td>
                        <Checkbox label="" checked={selected.has(e.employeeId)}
                          onChange={(v) => setSelected((s) => { const n = new Set(s); if (v) n.add(e.employeeId); else n.delete(e.employeeId); return n; })} />
                      </td>
                      <td>
                        <b>{e.employeeName}</b>{e.role === 'Admin' && <> <Pill tone="info">Admin</Pill></>}
                        <span className="cell-sub">{e.employeeCode}</span>
                      </td>
                      <td>{e.department}<span className="cell-sub">{e.designation}</span></td>
                      <td className="nowrap">{e.mobileNo}</td>
                      <td>{e.isDefaultEligible ? <Pill tone="on">Default</Pill> : <Pill tone="muted">Opt-in</Pill>}</td>
                      <td>{e.status === 'Active' ? <Pill tone="info">Active</Pill> : <Pill tone="danger">Inactive</Pill>}</td>
                      <td>
                        {e.hasPassword ? <span className="cell-sub">{e.lastLoginAt ? fmtUtc(e.lastLoginAt) : 'Password set'}</span>
                          : <Pill tone="warn">No password</Pill>}
                      </td>
                      <td className="actions">
                        <Button size="sm" variant="text" onClick={() => setDetailId(e.employeeId)}><CalendarCheck size={15} /> Eligibility</Button>
                        <Button size="sm" variant="text" onClick={() => openEdit(e)}><Pencil size={15} /> Edit</Button>
                        {e.hasPassword && <Button size="sm" variant="text" onClick={() => resetPassword(e)}><KeyRound size={15} /> Reset</Button>}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="pager">
              <span className="muted">{list.data?.total ?? 0} employees</span>
              <div className="row">
                <Button size="sm" variant="ghost" disabled={page <= 1} onClick={() => setPage(page - 1)}>Previous</Button>
                <span className="muted">Page {page} of {pages}</span>
                <Button size="sm" variant="ghost" disabled={page >= pages} onClick={() => setPage(page + 1)}>Next</Button>
              </div>
            </div>
          </>
        )}
      </Card>

      {editing && (
        <EmployeeForm
          id={editing.id} initial={editing.form} departments={depts.data ?? []} designations={desigs.data ?? []}
          onClose={() => setEditing(null)}
          onSaved={() => { setEditing(null); list.reload(); }}
        />
      )}
      {detailId !== null && (
        <EligibilityModal id={detailId} mealTypes={types.data ?? []} onClose={() => { setDetailId(null); list.reload(); }} />
      )}
      {bulkOpen && (
        <BulkEligibilityModal
          ids={[...selected]} mealTypes={types.data ?? []}
          onClose={() => setBulkOpen(false)}
          onDone={() => { setBulkOpen(false); setSelected(new Set()); list.reload(); }}
        />
      )}
    </>
  );
}

// ---------------------------------------------------------------------
function EmployeeForm({ id, initial, departments, designations, onClose, onSaved }: {
  id: number | null; initial: EmployeeSave; departments: NamedItem[]; designations: NamedItem[];
  onClose: () => void; onSaved: () => void;
}) {
  const toast = useToast();
  const [f, setF] = useState<EmployeeSave>(initial);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const set = <K extends keyof EmployeeSave>(k: K, v: EmployeeSave[K]) => setF((x) => ({ ...x, [k]: v }));

  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    const body = { ...f, email: f.email || null, eligibleTo: f.eligibleTo || null, eligibleFrom: f.eligibleFrom || null };
    try {
      if (id) await api.put(`/api/employees/${id}`, body);
      else await api.post('/api/employees', body);
      toast.success(id ? 'Employee updated' : 'Employee registered. They can now create a password.');
      onSaved();
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal
      open title={id ? 'Edit employee' : 'Register employee'} onClose={onClose} wide
      footer={<>
        <Button variant="ghost" onClick={onClose}>Cancel</Button>
        <Button type="submit" form="emp-form" loading={busy}>{id ? 'Save changes' : 'Register'}</Button>
      </>}
    >
      <form id="emp-form" onSubmit={submit} className="stack">
        {error && <ErrorBox message={error} />}
        <div className="form-grid">
          <Field label="Employee Code *"><Input required value={f.employeeCode} onChange={(e) => set('employeeCode', e.target.value)} /></Field>
          <Field label="Employee Name *"><Input required value={f.employeeName} onChange={(e) => set('employeeName', e.target.value)} /></Field>
          <Field label="Department *">
            <Select required value={f.departmentId || ''} onChange={(e) => set('departmentId', Number(e.target.value))}>
              <option value="">Select…</option>
              {departments.filter((d) => d.isActive || d.id === f.departmentId).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
            </Select>
          </Field>
          <Field label="Designation *">
            <Select required value={f.designationId || ''} onChange={(e) => set('designationId', Number(e.target.value))}>
              <option value="">Select…</option>
              {designations.filter((d) => d.isActive || d.id === f.designationId).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
            </Select>
          </Field>
          <Field label="Mobile No *" hint="Used to sign in"><Input type="tel" required value={f.mobileNo} onChange={(e) => set('mobileNo', e.target.value)} placeholder="01XXXXXXXXX" /></Field>
          <Field label="Email"><Input type="email" value={f.email ?? ''} onChange={(e) => set('email', e.target.value)} /></Field>
          <Field label="Status">
            <Select value={f.status} onChange={(e) => set('status', e.target.value as EmployeeSave['status'])}>
              <option value="Active">Active</option><option value="Inactive">InActive</option>
            </Select>
          </Field>
          <Field label="Role">
            <Select value={f.role} onChange={(e) => set('role', e.target.value as EmployeeSave['role'])}>
              <option value="Employee">Employee</option><option value="Admin">Admin</option>
            </Select>
          </Field>
        </div>
        {!id && (
          <Card className="span-2">
            <Checkbox label={<b>Default eligible for meal</b>} checked={!!f.defaultEligible} onChange={(v) => set('defaultEligible', v)} />
            <p className="muted" style={{ margin: '6px 0 12px' }}>
              The system marks the regular meal ON every working day in this period. Others can turn their own meal ON/OFF.
            </p>
            {f.defaultEligible && (
              <div className="form-grid">
                <Field label="Effective date"><Input type="date" required value={f.eligibleFrom ?? ''} onChange={(e) => set('eligibleFrom', e.target.value)} /></Field>
                <Field label="Expire date" hint="Leave empty for no end date"><Input type="date" value={f.eligibleTo ?? ''} onChange={(e) => set('eligibleTo', e.target.value)} /></Field>
              </div>
            )}
          </Card>
        )}
        {id && <p className="muted">Manage meal eligibility periods from the Eligibility button in the list.</p>}
      </form>
    </Modal>
  );
}

// ---------------------------------------------------------------------
function EligibilityModal({ id, mealTypes, onClose }: { id: number; mealTypes: MealType[]; onClose: () => void }) {
  const toast = useToast();
  const confirm = useConfirm();
  const detail = useAsync(() => api.get<EmployeeDetail>(`/api/employees/${id}`), [id]);
  const regular = mealTypes.filter((t) => t.isRegular);
  const [edit, setEdit] = useState<{ id: number | null; mealTypeId: number; effectiveDate: string; expireDate: string; remarks: string } | null>(null);
  const [busy, setBusy] = useState(false);

  function startNew() {
    setEdit({ id: null, mealTypeId: regular[0]?.mealTypeId ?? 0, effectiveDate: addDays(todayIso(), 1), expireDate: '', remarks: '' });
  }
  function startEdit(e: Eligibility) {
    setEdit({ id: e.mealEligibilityId, mealTypeId: e.mealTypeId, effectiveDate: e.effectiveDate, expireDate: e.expireDate ?? '', remarks: e.remarks ?? '' });
  }

  async function save(ev: FormEvent) {
    ev.preventDefault();
    if (!edit) return;
    setBusy(true);
    const body = { mealTypeId: edit.mealTypeId, effectiveDate: edit.effectiveDate, expireDate: edit.expireDate || null, remarks: edit.remarks || null };
    try {
      const res = edit.id
        ? await api.put<EmployeeDetail>(`/api/employees/eligibilities/${edit.id}`, body)
        : await api.post<EmployeeDetail>(`/api/employees/${id}/eligibilities`, body);
      detail.setData(res);
      setEdit(null);
      toast.success('Eligibility saved');
    } catch (err) {
      toast.error((err as Error).message);
    } finally {
      setBusy(false);
    }
  }

  async function remove(e: Eligibility) {
    const ok = await confirm({
      title: 'Delete eligibility period?',
      message: `${fmtDate(e.effectiveDate)} – ${e.expireDate ? fmtDate(e.expireDate) : 'no end'}. Upcoming automatic meals in this period will be removed. Past meals are kept.`,
      confirmText: 'Delete', danger: true,
    });
    if (!ok) return;
    try {
      detail.setData(await api.del<EmployeeDetail>(`/api/employees/eligibilities/${e.mealEligibilityId}`));
      toast.success('Deleted');
    } catch (err) {
      toast.error((err as Error).message);
    }
  }

  const emp = detail.data?.employee;
  return (
    <Modal open wide onClose={onClose} title={emp ? `Meal eligibility · ${emp.employeeName}` : 'Meal eligibility'}
      footer={<Button variant="ghost" onClick={onClose}>Close</Button>}>
      {detail.error && <ErrorBox message={detail.error} />}
      {!detail.data ? <Spinner /> : (
        <div className="stack">
          <p className="muted">
            While a period is current, the regular meal is marked ON automatically on working days. To stop it, set an
            expire date. Changes apply from tomorrow.
          </p>
          <div className="table-wrap">
            <table className="table">
              <thead><tr><th>Meal</th><th>Effective</th><th>Expire</th><th>Remarks</th><th /><th className="actions" /></tr></thead>
              <tbody>
                {detail.data.eligibilities.length === 0 && (
                  <tr><td colSpan={6} className="muted">Not default eligible. The employee can turn meals ON/OFF themselves.</td></tr>
                )}
                {detail.data.eligibilities.map((e) => (
                  <tr key={e.mealEligibilityId}>
                    <td>{e.mealTypeName}</td>
                    <td className="nowrap">{fmtDate(e.effectiveDate)}</td>
                    <td className="nowrap">{e.expireDate ? fmtDate(e.expireDate) : 'No end'}</td>
                    <td>{e.remarks}</td>
                    <td>{e.isCurrent && <Pill tone="on">Current</Pill>}</td>
                    <td className="actions">
                      <Button size="sm" variant="text" onClick={() => startEdit(e)}>Edit</Button>
                      <Button size="sm" variant="text" onClick={() => remove(e)}>Delete</Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {!edit && <div><Button variant="ghost" onClick={startNew}><Plus size={16} /> Add period</Button></div>}
          {edit && (
            <Card title={edit.id ? 'Edit period' : 'New period'}>
              <form className="stack" onSubmit={save}>
                <div className="form-grid">
                  <Field label="Meal">
                    <Select value={edit.mealTypeId} onChange={(e) => setEdit({ ...edit, mealTypeId: Number(e.target.value) })}>
                      {regular.map((t) => <option key={t.mealTypeId} value={t.mealTypeId}>{t.name}</option>)}
                    </Select>
                  </Field>
                  <Field label="Remarks"><Input value={edit.remarks} onChange={(e) => setEdit({ ...edit, remarks: e.target.value })} /></Field>
                  <Field label="Effective date"><Input type="date" required value={edit.effectiveDate} onChange={(e) => setEdit({ ...edit, effectiveDate: e.target.value })} /></Field>
                  <Field label="Expire date" hint="Empty = no end"><Input type="date" value={edit.expireDate} onChange={(e) => setEdit({ ...edit, expireDate: e.target.value })} /></Field>
                </div>
                <div className="row">
                  <Button type="submit" loading={busy}>Save period</Button>
                  <Button variant="ghost" onClick={() => setEdit(null)}>Cancel</Button>
                </div>
              </form>
            </Card>
          )}
        </div>
      )}
    </Modal>
  );
}

// ---------------------------------------------------------------------
function BulkEligibilityModal({ ids, mealTypes, onClose, onDone }: {
  ids: number[]; mealTypes: MealType[]; onClose: () => void; onDone: () => void;
}) {
  const toast = useToast();
  const regular = mealTypes.filter((t) => t.isRegular);
  const [mealTypeId, setType] = useState(regular[0]?.mealTypeId ?? 0);
  const [from, setFrom] = useState(addDays(todayIso(), 1));
  const [to, setTo] = useState('');
  const [busy, setBusy] = useState(false);

  async function submit() {
    setBusy(true);
    try {
      const res = await api.post<{ added: number; skipped: number }>('/api/employees/eligibilities/bulk', {
        employeeIds: ids, mealTypeId, effectiveDate: from, expireDate: to || null,
      });
      toast.success(`${res.added} employees made eligible${res.skipped ? `, ${res.skipped} skipped (overlapping period)` : ''}`);
      onDone();
    } catch (err) {
      toast.error((err as Error).message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal open title={`Default eligibility for ${ids.length} employees`} onClose={onClose}
      footer={<><Button variant="ghost" onClick={onClose}>Cancel</Button><Button loading={busy} onClick={submit}>Apply</Button></>}>
      <div className="stack">
        <Field label="Meal">
          <Select value={mealTypeId} onChange={(e) => setType(Number(e.target.value))}>
            {regular.map((t) => <option key={t.mealTypeId} value={t.mealTypeId}>{t.name}</option>)}
          </Select>
        </Field>
        <div className="form-grid">
          <Field label="Effective date"><Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} /></Field>
          <Field label="Expire date" hint="Empty = no end"><Input type="date" value={to} onChange={(e) => setTo(e.target.value)} /></Field>
        </div>
        <p className="muted">Employees who already have an overlapping period are skipped.</p>
      </div>
    </Modal>
  );
}
