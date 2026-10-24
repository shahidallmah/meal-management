import { useMemo, useRef, useState, type ChangeEvent } from 'react';
import {
  AlertCircle, Briefcase, Building2, CheckCircle2, Download, FileSpreadsheet, FileUp, RefreshCw, TicketCheck, Upload,
} from 'lucide-react';
import { api } from '../api/client';
import type { EmployeeImportResult, EmployeeImportRow, ImportAction } from '../api/types';
import { Button, Checkbox, ErrorBox, Modal, Pill, useToast } from './ui';

const MAX_BYTES = 5 * 1024 * 1024;
const ACTION_TONE: Record<ImportAction, 'on' | 'info' | 'muted' | 'danger'> = {
  New: 'on', Update: 'info', Unchanged: 'muted', Error: 'danger',
};

/**
 * Excel import: 1) choose file → 2) preview (nothing saved yet) → 3) import valid rows.
 * Missing departments/designations are created by the server.
 */
export default function ImportEmployeesModal({ onClose, onImported }: { onClose: () => void; onImported: () => void }) {
  const toast = useToast();
  const fileRef = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<EmployeeImportResult | null>(null);
  const [done, setDone] = useState<EmployeeImportResult | null>(null);
  const [busy, setBusy] = useState<'template' | 'check' | 'import' | 'codes' | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [onlyIssues, setOnlyIssues] = useState(true);

  async function template() {
    setBusy('template');
    try { await api.download('/api/employees/import/template', {}, 'employee-import-template.xlsx'); }
    catch (e) { toast.error((e as Error).message); }
    finally { setBusy(null); }
  }

  async function check(f: File) {
    setBusy('check');
    setError(null);
    setPreview(null);
    try { setPreview(await api.upload<EmployeeImportResult>('/api/employees/import?commit=false', f, 'POST')); }
    catch (e) { setError((e as Error).message); }
    finally { setBusy(null); }
  }

  function pick(e: ChangeEvent<HTMLInputElement>) {
    const f = e.target.files?.[0];
    e.target.value = '';
    if (!f) return;
    if (!/\.xlsx$/i.test(f.name)) { setError('Please choose an Excel file (.xlsx). Old .xls files: open in Excel and "Save As" .xlsx.'); return; }
    if (f.size > MAX_BYTES) { setError('The file must be 5 MB or smaller.'); return; }
    setFile(f);
    check(f);
  }

  async function commit() {
    if (!file) return;
    setBusy('import');
    setError(null);
    try {
      const res = await api.upload<EmployeeImportResult>('/api/employees/import?commit=true', file, 'POST');
      setDone(res);
      onImported();
      toast.success(`Import finished: ${res.newCount} new, ${res.updateCount} updated.`);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(null);
    }
  }

  async function codes() {
    if (!done?.createdEmployeeIds.length) return;
    setBusy('codes');
    try {
      await api.downloadPost('/api/employees/setup-codes/bulk', { employeeIds: done.createdEmployeeIds }, 'activation-codes.xlsx');
      toast.success('Activation codes downloaded. Hand them out in person or by phone.');
    } catch (e) {
      toast.error((e as Error).message);
    } finally {
      setBusy(null);
    }
  }

  const rows = useMemo(
    () => (preview?.rows ?? []).filter((r) => !onlyIssues || r.action !== 'Unchanged'),
    [preview, onlyIssues],
  );
  const toSave = preview ? preview.newCount + preview.updateCount : 0;
  const masterChanges = preview
    ? preview.newDepartments.length + preview.newDesignations.length + preview.reactivatedDepartments.length + preview.reactivatedDesignations.length
    : 0;

  // ---------------- Result ----------------
  if (done) {
    return (
      <Modal open wide title="Import complete" onClose={onClose}
        footer={<Button onClick={onClose}>Close</Button>}>
        <div className="import-done">
          <CheckCircle2 size={40} />
          <div>
            <h2>{done.newCount} employees added · {done.updateCount} updated</h2>
            <p className="muted">
              {done.newDepartments.length + done.reactivatedDepartments.length} departments and{' '}
              {done.newDesignations.length + done.reactivatedDesignations.length} designations added or reactivated.
              {done.errorCount > 0 && ` ${done.errorCount} row${done.errorCount === 1 ? '' : 's'} with errors ${done.errorCount === 1 ? 'was' : 'were'} skipped.`}
            </p>
          </div>
        </div>
        {done.createdEmployeeIds.length > 0 && (
          <div className="import-next">
            <TicketCheck size={20} />
            <div className="grow">
              <b>New employees need an activation code to sign in.</b>
              <p className="muted">Download one-time codes for the {done.createdEmployeeIds.length} new employees as Excel and hand them out in person or by phone.</p>
            </div>
            <Button onClick={codes} loading={busy === 'codes'}>{busy !== 'codes' && <Download size={16} />} Activation codes</Button>
          </div>
        )}
      </Modal>
    );
  }

  // ---------------- Choose file / preview ----------------
  return (
    <Modal
      open wide title="Import employees from Excel" onClose={onClose}
      footer={<>
        <Button variant="ghost" onClick={onClose}>Cancel</Button>
        {preview && (
          <Button onClick={commit} loading={busy === 'import'} disabled={toSave + masterChanges === 0}>
            {busy !== 'import' && <Upload size={16} />} Import {toSave} employee{toSave === 1 ? '' : 's'}
          </Button>
        )}
      </>}
    >
      <div className="stack">
        <ol className="import-steps">
          <li>
            <span>Download the template. It lists the departments and designations already in the system.</span>
            <Button variant="ghost" size="sm" onClick={template} loading={busy === 'template'}>
              {busy !== 'template' && <FileSpreadsheet size={15} />} Template
            </Button>
          </li>
          <li>
            <span>Fill in the <b>Employees</b> sheet. Existing employee codes are updated, new codes are added. New departments and designations are created automatically.</span>
          </li>
          <li>
            <span>Choose the file. You’ll see a preview — nothing is saved until you click Import.</span>
            <input ref={fileRef} type="file" className="file-input" accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" onChange={pick} />
            <Button size="sm" onClick={() => fileRef.current?.click()} loading={busy === 'check'}>
              {busy !== 'check' && (file ? <RefreshCw size={15} /> : <FileUp size={15} />)} {file ? 'Choose another file' : 'Choose file'}
            </Button>
          </li>
        </ol>
        {file && <div className="muted"><FileSpreadsheet size={14} style={{ verticalAlign: -2 }} /> {file.name}</div>}
        {error && <ErrorBox message={error} />}

        {preview && (
          <>
            <div className="import-summary">
              <span className="pill pill-on">{preview.newCount} new</span>
              <span className="pill pill-info">{preview.updateCount} to update</span>
              <span className="pill pill-muted">{preview.unchangedCount} unchanged</span>
              {preview.errorCount > 0 && <span className="pill pill-danger">{preview.errorCount} with errors — will be skipped</span>}
            </div>

            {masterChanges > 0 && (
              <div className="import-master">
                <MasterList icon={Building2} title="New departments" items={preview.newDepartments} />
                <MasterList icon={Briefcase} title="New designations" items={preview.newDesignations} />
                <MasterList icon={Building2} title="Departments to reactivate" items={preview.reactivatedDepartments} />
                <MasterList icon={Briefcase} title="Designations to reactivate" items={preview.reactivatedDesignations} />
              </div>
            )}

            <div className="row" style={{ justifyContent: 'space-between' }}>
              <b>Rows ({preview.totalRows})</b>
              <Checkbox label="Hide unchanged rows" checked={onlyIssues} onChange={setOnlyIssues} />
            </div>
            <div className="table-wrap import-table">
              <table className="table">
                <thead>
                  <tr><th>Row</th><th>Employee</th><th>Department / Designation</th><th>Mobile</th><th>Result</th></tr>
                </thead>
                <tbody>
                  {rows.length === 0 && <tr><td colSpan={5} className="muted">No changes in this file.</td></tr>}
                  {rows.map((r) => <ImportRow key={r.rowNo} r={r} />)}
                </tbody>
              </table>
            </div>
          </>
        )}
      </div>
    </Modal>
  );
}

function ImportRow({ r }: { r: EmployeeImportRow }) {
  return (
    <tr className={r.action === 'Error' ? 'row-error' : ''}>
      <td className="num">{r.rowNo}</td>
      <td><b>{r.employeeName || '—'}</b><span className="cell-sub">{r.employeeCode || '—'}</span></td>
      <td>{r.department || '—'}<span className="cell-sub">{r.designation || '—'}</span></td>
      <td className="nowrap">{r.mobileNo || '—'}</td>
      <td>
        <Pill tone={ACTION_TONE[r.action]}>{r.action === 'Update' ? 'Update' : r.action}</Pill>
        {r.messages.map((m, i) => (
          <span key={i} className={`cell-sub ${m.startsWith('Error:') ? 'msg-error' : ''}`}>
            {m.startsWith('Error:') && <AlertCircle size={12} />} {m.replace(/^Error:\s*/, '')}
          </span>
        ))}
      </td>
    </tr>
  );
}

function MasterList({ icon: Icon, title, items }: { icon: typeof Building2; title: string; items: string[] }) {
  if (!items.length) return null;
  return (
    <div className="import-master-box">
      <div className="import-master-title"><Icon size={15} /> {title} ({items.length})</div>
      <div className="import-chips">{items.map((x) => <span key={x}>{x}</span>)}</div>
    </div>
  );
}
