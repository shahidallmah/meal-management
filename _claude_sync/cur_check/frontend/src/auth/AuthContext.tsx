import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { api, auth } from '../api/client';
import type { AuthResponse, User } from '../api/types';

const USER_KEY = 'mm.user';

interface AuthState {
  user: User | null;
  isAdmin: boolean;
  ready: boolean;
  signIn: (res: AuthResponse) => void;
  signOut: () => void;
  refresh: () => Promise<void>;
}

const Ctx = createContext<AuthState | null>(null);

function readUser(): User | null {
  try {
    const raw = localStorage.getItem(USER_KEY);
    return raw ? (JSON.parse(raw) as User) : null;
  } catch {
    return null;
  }
}
function writeUser(u: User | null) {
  try {
    if (u) localStorage.setItem(USER_KEY, JSON.stringify(u));
    else localStorage.removeItem(USER_KEY);
  } catch { /* ignore */ }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(() => (auth.token ? readUser() : null));
  const [ready, setReady] = useState(false);

  const signOut = useCallback(() => {
    auth.set(null);
    writeUser(null);
    setUser(null);
  }, []);

  const signIn = useCallback((res: AuthResponse) => {
    auth.set(res.token);
    writeUser(res.user);
    setUser(res.user);
  }, []);

  const refresh = useCallback(async () => {
    if (!auth.token) return;
    const me = await api.get<User>('/api/auth/me');
    writeUser(me);
    setUser(me);
  }, []);

  useEffect(() => {
    auth.onUnauthorized(signOut);
    if (auth.token) refresh().catch(() => { /* handled by onUnauthorized / offline */ }).finally(() => setReady(true));
    else setReady(true);
  }, [refresh, signOut]);

  const value = useMemo<AuthState>(
    () => ({ user, isAdmin: user?.role === 'Admin', ready, signIn, signOut, refresh }),
    [user, ready, signIn, signOut, refresh],
  );
  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

export function useAuth(): AuthState {
  const v = useContext(Ctx);
  if (!v) throw new Error('useAuth must be used inside AuthProvider');
  return v;
}
