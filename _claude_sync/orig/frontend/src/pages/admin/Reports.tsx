import { useEffect, useState } from 'react';
import { Download, FileSpreadsheet } from 'lucide-react';
import { api } from '../../api/client';
import type { Employee, MealType, NamedItem, PagedResult, ReportDefinition, ReportTable } from '../../api/types';
import { Button, Card, Empty, ErrorBox, Field, Input, PageHeader, Select, Spinner, useAsync, useToast } from '../../components/ui';
import { addDays, fmtDate, fmtMoney, fmtNumber, monthStartIso, todayIso } from '../../utils/format';

function cell(v: unknown, type: string) {
  if (v === null || v === undefined || v === '') return '';
  if (type === 'money' && typeof v === 'number') return fmtMoney(v);
  if (type === 'number' && typeof v === 'number') return fmtNumber(v);
  if (type === 'date' && typeof v === 'string' && /^\d{4}-\d{2}-\d{2}/.test(v)) return fmtDate(v);
  return String(v);
}

export default function Reports() {
  const toast = useToast();
  const defs = useAsync(() => api.get<ReportDefinition[]>('/api/reports'), []);
  const types = useAsync(() => api.get<MealType[]>('/api/meal-types'), []);
  const depts = useAsync(() => api.get<NamedItem[]>('/api/departments'), []);
  const emps = useAsync(() => api.get<PagedResult<Employee>>('/api/employees', { pageSize: 500, status: '' }), []);

  const [code, setCode] = useState('daily-summary');
  const [from, setFrom] = useState(monthStartIso());
  const [to, setTo] = useState(todayIso());
  const [date, setDate] = useState(addDays(todayIso(), 1));
  const [mealTypeId, setMealTypeId] = useState('');
  const [departmentId, setDept] = useState('');
  const [employeeId, setEmployee] = useState('');
  const [table, setTable] = useState<ReportTable | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [downloading, setDownloading] = useState<string | null>(null);

  const def = defs.data?.find((d) => d.code === code);
  const params = () => ({
    from: def?.needsSingleDate ? undefined : from,
    to: def?.needsSingleDate ? undefined : to,
    date: def?.needsSingleDate ? date : undefined,
    mealTypeId, departmentId,
    employeeId: code === 'employee-detail' ? employeeId : undefined,
  });

  useEffect(() => { setTable(null); setError(null); }, [code]);

  async function preview() {
    if (code === 'employee-detail' && !employeeId) return setError('Please choose an employee.');
    setLoading(true);
    setError(null);
    try {
      setTable(await api.get<ReportTable>(`/api/reports/${code}`, params()));
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setLoading(false);
    }
  }

  async function download(format: 'xlsx' | 'csv') {
    if (code === 'employee-detail' && !employeeId) return setError('Please choose an employee.');
    setDownloading(format);
    try {
      await api.download(`/api/reports/${code}/export`, { ...params(), format }, `${code}.${format}`);
    } catch (e) {
      toast.error((e as Error).message);
    } finally {
      setDownloading(null);
    }
  }

  return (
    <>
      <PageHeader title="Reports" subtitle="Preview on screen or download as Excel / CSV" />
      <div className="grid grid-2" style={{ marginBottom: 16 }}>
        {(defs.data ?? []).map((d) => (
          <button key={d.code} type="button" className="card" onClick={() => setCode(d.code)}
            style={{ textAlign: 'left', cursor: 'pointer', font: 'inherit', borderColor: d.code === code ? 'var(--brand-green)' : undefined, boxShadow: d.code === code ? 'var(--focus-ring)' : undefined }}>
            <div className="row" style={{ flexWrap: 'nowrap' }}>
              <FileSpreadsheet size={20} color="var(--color-primary)" />
              <div><b>{d.name}</b><p className="muted">{d.description}</p></div>
            </div>
          </button>
        ))}
      </div>
      {defs.error && <ErrorBox message={defs.error} />}

      <Card title={def?.name ?? 'Report'}>
        <div className="filters">
          {def?.needsSingleDate ? (
            <Field label="Date"><Input type="date" value={date} onChange={(e) => setDate(e.target.value)} /></Field>
          ) : (
            <>
              <Field label="From"><Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} /></Field>
              <Field label="To"><Input type="date" value={to} onChange={(e) => setTo(e.target.value)} /></Field>
            </>
          )}
          <Field label="Meal">
            <Select value={mealTypeId} onChange={(e) => setMealTypeId(e.target.value)}>
              {!def?.needsSingleDate && <option value="">All meals</option>}
              {(types.data ?? []).map((t) => <option key={t.mealTypeId} value={t.mealTypeId}>{t.name}</option>)}
            </Select>
          </Field>
          {code === 'employee-detail' ? (
            <Field label="Employee">
              <Select value={employeeId} onChange={(e) => setEmployee(e.target.value)}>
                <option value="">Select…</option>
                {(emps.data?.items ?? []).map((e) => <option key={e.employeeId} value={e.employeeId}>{e.employeeCode} · {e.employeeName}</option>)}
              </Select>
            </Field>
          ) : (
            <Field label="Department">
              <Select value={departmentId} onChange={(e) => setDept(e.target.value)}>
                <option value="">All departments</option>
                {(depts.data ?? []).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
              </Select>
            </Field>
          )}
          <div className="row">
            <Button onClick={preview} loading={loading}>Preview</Button>
            <Button variant="ghost" onClick={() => download('xlsx')} loading={downloading === 'xlsx'}><Download size={16} /> Excel</Button>
            <Button variant="ghost" onClick={() => download('csv')} loading={downloading === 'csv'}><Download size={16} /> CSV</Button>
          </div>
        </div>

        {error && <ErrorBox message={error} />}
        {loading && <Spinner />}
        {table && !loading && (
          <>
            <div style={{ marginBottom: 10 }}>
              <h3>{table.title}</h3>
              <p className="muted">{table.subtitle} · {table.rows.length} rows</p>
            </div>
            {table.rows.length === 0 ? <Empty title="No data for these filters" /> : (
              <div className="table-wrap">
                <table className="table">
                  <thead><tr>{table.columns.map((c) => <th key={c.key} className={c.type === 'money' || c.type === 'number' ? 'num' : ''}>{c.header}</th>)}</tr></thead>
                  <tbody>
                    {table.rows.map((r, i) => (
                      <tr key={i}>{table.columns.map((c) => <td key={c.key} className={c.type === 'money' || c.type === 'number' ? 'num' : c.type === 'date' ? 'nowrap' : ''}>{cell(r[c.key], c.type)}</td>)}</tr>
                    ))}
                  </tbody>
                  {Object.keys(table.totals).length > 0 && (
                    <tfoot><tr>{table.columns.map((c) => <td key={c.key} className={c.type === 'money' || c.type === 'number' ? 'num' : ''}>{cell(table.totals[c.key], typeof table.totals[c.key] === 'string' ? 'text' : c.type)}</td>)}</tr></tfoot>
                  )}
                </table>
              </div>
            )}
          </>
        )}
      </Card>
    </>
  );
}
