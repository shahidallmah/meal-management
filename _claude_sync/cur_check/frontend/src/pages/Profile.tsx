import { useState, type FormEvent } from 'react';
import { IdCard, KeyRound, LogOut, UserRound } from 'lucide-react';
import { api } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import { Button, Card, ErrorBox, Field, Input, PageHeader, useToast } from '../components/ui';

export default function Profile() {
  const { user, signOut } = useAuth();
  const toast = useToast();
  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [confirm, setConfirm] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(e: FormEvent) {
    e.preventDefault();
    if (next.length < 6) return setError('New password must be at least 6 characters.');
    if (next !== confirm) return setError('The two new passwords do not match.');
    setBusy(true);
    setError(null);
    try {
      await api.post('/api/auth/change-password', { currentPassword: current, newPassword: next });
      setCurrent(''); setNext(''); setConfirm('');
      toast.success('Password changed');
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  }

  if (!user) return null;
  const rows: [string, string][] = [
    ['Employee Code', user.employeeCode],
    ['Name', user.employeeName],
    ['Department', user.department],
    ['Designation', user.designation],
    ['Mobile No', user.mobileNo],
    ['Role', user.role],
  ];

  return (
    <>
      <PageHeader icon={UserRound} title="Profile" actions={<Button variant="ghost" onClick={signOut} className="only-mobile"><LogOut size={16} /> Sign out</Button>} />
      <div className="grid grid-2">
        <Card title="My details" icon={IdCard}>
          <table className="table">
            <tbody>
              {rows.map(([k, v]) => (
                <tr key={k}><td className="muted" style={{ width: 140 }}>{k}</td><td>{v}</td></tr>
              ))}
            </tbody>
          </table>
          <p className="muted" style={{ marginTop: 12 }}>To correct these details, contact the admin.</p>
        </Card>
        <Card title="Change password" icon={KeyRound}>
          <form className="stack" onSubmit={submit}>
            {error && <ErrorBox message={error} />}
            <Field label="Current password"><Input type="password" autoComplete="current-password" required value={current} onChange={(e) => setCurrent(e.target.value)} /></Field>
            <Field label="New password" hint="At least 6 characters"><Input type="password" autoComplete="new-password" required value={next} onChange={(e) => setNext(e.target.value)} /></Field>
            <Field label="Confirm new password"><Input type="password" autoComplete="new-password" required value={confirm} onChange={(e) => setConfirm(e.target.value)} /></Field>
            <div><Button type="submit" loading={busy}>Update password</Button></div>
          </form>
        </Card>
      </div>
    </>
  );
}
