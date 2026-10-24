import { useState, type FormEvent } from 'react';
import { Link, Navigate, useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import type { AuthResponse, CheckMobileResponse } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import { Button, ErrorBox, Field, Input } from '../components/ui';
import { Logo } from '../components/AppShell';

export default function Login() {
  const { user, signIn } = useAuth();
  const nav = useNavigate();
  const [mobileNo, setMobile] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [needsPassword, setNeedsPassword] = useState(false);

  if (user) return <Navigate to={user.role === 'Admin' ? '/admin' : '/meals'} replace />;

  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    setNeedsPassword(false);
    try {
      const res = await api.post<AuthResponse>('/api/auth/login', { mobileNo, password });
      signIn(res);
      nav(res.user.role === 'Admin' ? '/admin' : '/meals', { replace: true });
    } catch (err) {
      setError((err as Error).message);
      try {
        const chk = await api.post<CheckMobileResponse>('/api/auth/check-mobile', { mobileNo });
        setNeedsPassword(chk.registered && !chk.hasPassword && chk.isActive);
      } catch { /* ignore */ }
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="auth-page">
      <p className="auth-foot">EMPLOYEE MEAL MANAGEMENT</p>
      <div className="auth-card">
        <div className="auth-logo"><Logo size={32} /></div>
        <h1>Sign in</h1>
        <p className="muted">Use your registered mobile number</p>
        <form className="auth-form" onSubmit={submit}>
          {error && <ErrorBox message={error} />}
          <Field label="Mobile No">
            <Input
              type="tel" inputMode="tel" autoComplete="username" placeholder="01XXXXXXXXX" required
              value={mobileNo} onChange={(e) => setMobile(e.target.value)}
            />
          </Field>
          <Field label="Password">
            <Input
              type="password" autoComplete="current-password" required
              value={password} onChange={(e) => setPassword(e.target.value)}
            />
          </Field>
          <Button type="submit" className="btn-block" loading={busy}>Sign in</Button>
          {needsPassword && (
            <Link to={`/create-password?mobile=${encodeURIComponent(mobileNo)}`} className="btn btn-accent btn-block">
              Create your password
            </Link>
          )}
        </form>
        <div className="auth-links">
          First time here? <Link to="/create-password">Create your password</Link>
        </div>
      </div>
    </div>
  );
}
