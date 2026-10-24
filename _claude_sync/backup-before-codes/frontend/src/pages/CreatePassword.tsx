import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { BadgeCheck, IdCard, KeyRound, Lock, Phone } from 'lucide-react';
import { Logo } from '../components/AppShell';
import { useBranding } from '../components/Branding';
import { api } from '../api/client';
import type { AuthResponse } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import { Button, ErrorBox, Field, Input } from '../components/ui';

export default function CreatePassword() {
  const [params] = useSearchParams();
  const { signIn } = useAuth();
  const nav = useNavigate();
  const { companyName, logoSrc } = useBranding();
  const [mobileNo, setMobile] = useState(params.get('mobile') ?? '');
  const [employeeCode, setCode] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(e: FormEvent) {
    e.preventDefault();
    if (password.length < 6) return setError('Password must be at least 6 characters.');
    if (password !== confirm) return setError('The two passwords do not match.');
    setBusy(true);
    setError(null);
    try {
      const res = await api.post<AuthResponse>('/api/auth/create-password', { mobileNo, employeeCode, password });
      signIn(res);
      nav(res.user.role === 'Admin' ? '/admin' : '/meals', { replace: true });
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="auth-page">
      <p className="auth-foot">EMPLOYEE MEAL MANAGEMENT</p>
      <div className="auth-card">
        <div className={`auth-logo ${logoSrc ? 'has-logo' : ''}`}>{logoSrc ? <Logo /> : <KeyRound size={30} />}</div>
        {companyName && <div className="auth-company">{companyName}</div>}
        <h1>Create password</h1>
        <p className="muted">For employees registered by the admin. Confirm your mobile number and employee code.</p>
        <form className="auth-form" onSubmit={submit}>
          {error && <ErrorBox message={error} />}
          <Field label="Mobile No">
            <Input icon={Phone} type="tel" inputMode="tel" required placeholder="01XXXXXXXXX" value={mobileNo} onChange={(e) => setMobile(e.target.value)} />
          </Field>
          <Field label="Employee Code">
            <Input icon={IdCard} required value={employeeCode} onChange={(e) => setCode(e.target.value)} placeholder="e.g. E-1021" />
          </Field>
          <Field label="New password" hint="At least 6 characters">
            <Input icon={Lock} type="password" autoComplete="new-password" required value={password} onChange={(e) => setPassword(e.target.value)} />
          </Field>
          <Field label="Confirm password">
            <Input icon={BadgeCheck} type="password" autoComplete="new-password" required value={confirm} onChange={(e) => setConfirm(e.target.value)} />
          </Field>
          <Button type="submit" className="btn-block" loading={busy}>{!busy && <KeyRound size={17} />}Create password & sign in</Button>
        </form>
        <div className="auth-links">Already have a password? <Link to="/login">Sign in</Link></div>
      </div>
    </div>
  );
}
