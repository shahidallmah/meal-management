import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { BadgeCheck, KeyRound, Lock, Phone, TicketCheck } from 'lucide-react';
import { api } from '../api/client';
import type { AuthResponse } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import { Button, ErrorBox, Field, Input } from '../components/ui';
import { Logo } from '../components/AppShell';
import { useBranding } from '../components/Branding';

/**
 * First sign-in and forgotten password.
 * The employee needs a one-time code from the admin: mobile number and employee code are known to
 * colleagues, so they are not enough to prove who you are.
 */
export default function CreatePassword() {
  const [params] = useSearchParams();
  const { signIn } = useAuth();
  const nav = useNavigate();
  const { companyName, logoSrc } = useBranding();
  const [mobileNo, setMobile] = useState(params.get('mobile') ?? '');
  const [code, setCode] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Shows the code as XXXX-XXXX while typing; the server ignores dashes/spaces and case.
  function onCode(v: string) {
    const raw = v.toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, 8);
    setCode(raw.length > 4 ? `${raw.slice(0, 4)}-${raw.slice(4)}` : raw);
  }

  async function submit(e: FormEvent) {
    e.preventDefault();
    if (password.length < 6) return setError('Password must be at least 6 characters.');
    if (password !== confirm) return setError('The two passwords do not match.');
    setBusy(true);
    setError(null);
    try {
      const res = await api.post<AuthResponse>('/api/auth/set-password', { mobileNo, code, password });
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
        <h1>Set password with code</h1>
        <p className="muted">For first sign-in or a forgotten password.</p>
        <div className="code-help">
          <TicketCheck size={18} />
          <span>Ask the <b>admin / HR</b> for a <b>one-time code</b>. They will give it to you in person or by phone. Each code works only once.</span>
        </div>
        <form className="auth-form" onSubmit={submit}>
          {error && <ErrorBox message={error} />}
          <Field label="Mobile No">
            <Input icon={Phone} type="tel" inputMode="tel" autoComplete="username" required placeholder="01XXXXXXXXX"
              value={mobileNo} onChange={(e) => setMobile(e.target.value)} />
          </Field>
          <Field label="One-time code" hint="8 letters/numbers from the admin, e.g. K7PM-49QX">
            <Input icon={TicketCheck} required autoComplete="one-time-code" autoCapitalize="characters" spellCheck={false}
              className="input code-input" placeholder="XXXX-XXXX" value={code} onChange={(e) => onCode(e.target.value)} />
          </Field>
          <Field label="New password" hint="At least 6 characters">
            <Input icon={Lock} type="password" autoComplete="new-password" required value={password} onChange={(e) => setPassword(e.target.value)} />
          </Field>
          <Field label="Confirm password">
            <Input icon={BadgeCheck} type="password" autoComplete="new-password" required value={confirm} onChange={(e) => setConfirm(e.target.value)} />
          </Field>
          <Button type="submit" className="btn-block" loading={busy}>{!busy && <KeyRound size={17} />}Set password & sign in</Button>
        </form>
        <div className="auth-links">Remember your password? <Link to="/login">Sign in</Link></div>
      </div>
    </div>
  );
}
