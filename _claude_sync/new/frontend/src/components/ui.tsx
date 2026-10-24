import {
  createContext, useCallback, useContext, useEffect, useRef, useState,
  type ButtonHTMLAttributes, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes,
} from 'react';
import { X, Loader2, CheckCircle2, AlertCircle, Inbox, type LucideIcon } from 'lucide-react';

// ---------------- Button ----------------
type Variant = 'primary' | 'accent' | 'ghost' | 'danger' | 'text';
export function Button({
  variant = 'primary', size, loading, children, className = '', ...rest
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: Variant; size?: 'sm'; loading?: boolean }) {
  return (
    <button
      className={`btn btn-${variant} ${size === 'sm' ? 'btn-sm' : ''} ${className}`}
      disabled={loading || rest.disabled}
      {...rest}
    >
      {loading && <Loader2 className="spin" size={16} />}
      {children}
    </button>
  );
}

// ---------------- Form fields ----------------
export function Field({ label, hint, error, children, className = '' }: {
  label: string; hint?: ReactNode; error?: string | null; children: ReactNode; className?: string;
}) {
  return (
    <label className={`field ${className}`}>
      <span className="field-label">{label}</span>
      {children}
      {error ? <span className="field-error">{error}</span> : hint ? <span className="field-hint">{hint}</span> : null}
    </label>
  );
}

export function Input({ icon: Icon, ...props }: InputHTMLAttributes<HTMLInputElement> & { icon?: LucideIcon }) {
  if (!Icon) return <input className="input" {...props} />;
  return (
    <span className="input-wrap">
      <Icon size={17} aria-hidden="true" />
      <input className="input" {...props} />
    </span>
  );
}

export function Select({ children, ...props }: SelectHTMLAttributes<HTMLSelectElement>) {
  return <select className="input" {...props}>{children}</select>;
}

export function Checkbox({ label, checked, onChange, disabled }: {
  label: ReactNode; checked: boolean; onChange: (v: boolean) => void; disabled?: boolean;
}) {
  return (
    <label className="checkbox">
      <input type="checkbox" checked={checked} disabled={disabled} onChange={(e) => onChange(e.target.checked)} />
      <span>{label}</span>
    </label>
  );
}

// ---------------- Toggle ----------------
export function Toggle({ on, onChange, disabled, label }: {
  on: boolean; onChange: (v: boolean) => void; disabled?: boolean; label: string;
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={on}
      aria-label={label}
      className={`toggle ${on ? 'is-on' : 'is-off'}`}
      disabled={disabled}
      onClick={() => onChange(!on)}
    />
  );
}

// ---------------- Pills ----------------
export function Pill({ tone, children }: { tone: 'on' | 'off' | 'info' | 'warn' | 'danger' | 'muted'; children: ReactNode }) {
  return <span className={`pill pill-${tone}`}>{children}</span>;
}

export function MealPill({ on }: { on: boolean }) {
  return <Pill tone={on ? 'on' : 'off'}>{on ? 'ON' : 'OFF'}</Pill>;
}

// ---------------- Icon chip ----------------
export type ChipTone = 'mint' | 'sky' | 'warm' | 'rose' | 'violet' | 'muted';
export function IconChip({ icon: Icon, tone = 'sky', size }: { icon: LucideIcon; tone?: ChipTone; size?: 'sm' | 'lg' }) {
  const px = size === 'sm' ? 16 : size === 'lg' ? 22 : 19;
  return <span className={`icon-chip ${tone} ${size ?? ''}`} aria-hidden="true"><Icon size={px} /></span>;
}

// ---------------- Layout helpers ----------------
export function PageHeader({ title, subtitle, actions, icon, tone = 'sky' }: {
  title: string; subtitle?: ReactNode; actions?: ReactNode; icon?: LucideIcon; tone?: ChipTone;
}) {
  return (
    <div className="page-header">
      <div className="page-title">
        {icon && <IconChip icon={icon} tone={tone} size="lg" />}
        <div>
          <h1>{title}</h1>
          {subtitle && <p className="muted">{subtitle}</p>}
        </div>
      </div>
      {actions && <div className="page-actions">{actions}</div>}
    </div>
  );
}

export function Card({ children, className = '', title, actions, icon: Icon }: {
  children: ReactNode; className?: string; title?: ReactNode; actions?: ReactNode; icon?: LucideIcon;
}) {
  return (
    <section className={`card ${className}`}>
      {(title || actions) && (
        <div className="card-head">
          {title && <h2 className="card-title">{Icon && <Icon size={18} />}{title}</h2>}
          {actions}
        </div>
      )}
      {children}
    </section>
  );
}

export function Stat({ label, value, sub, tone, icon, chip = 'sky' }: {
  label: string; value: ReactNode; sub?: ReactNode; tone?: 'accent' | 'primary'; icon?: LucideIcon; chip?: ChipTone;
}) {
  return (
    <div className="stat">
      <div className="stat-top">
        <span className="stat-label">{label}</span>
        {icon && <IconChip icon={icon} tone={chip} size="sm" />}
      </div>
      <span className={`stat-value ${tone ? `tone-${tone}` : ''}`}>{value}</span>
      {sub && <span className="stat-sub">{sub}</span>}
    </div>
  );
}

export function Spinner({ label = 'Loading…' }: { label?: string }) {
  return <div className="spinner"><Loader2 className="spin" size={20} /> {label}</div>;
}

export function Empty({ title, children, icon = Inbox }: { title: string; children?: ReactNode; icon?: LucideIcon }) {
  return (
    <div className="empty">
      <IconChip icon={icon} tone="muted" size="lg" />
      <strong>{title}</strong>
      {children && <p className="muted">{children}</p>}
    </div>
  );
}

export function ErrorBox({ message, onRetry }: { message: string; onRetry?: () => void }) {
  return (
    <div className="error-box">
      <AlertCircle size={18} />
      <span>{message}</span>
      {onRetry && <Button variant="text" size="sm" onClick={onRetry}>Try again</Button>}
    </div>
  );
}

// ---------------- Modal ----------------
export function Modal({ title, open, onClose, children, footer, wide }: {
  title: string; open: boolean; onClose: () => void; children: ReactNode; footer?: ReactNode; wide?: boolean;
}) {
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose();
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [open, onClose]);
  if (!open) return null;
  return (
    <div className="modal-backdrop" onMouseDown={(e) => e.target === e.currentTarget && onClose()}>
      <div className={`modal ${wide ? 'modal-wide' : ''}`} role="dialog" aria-modal="true" aria-label={title}>
        <div className="modal-head">
          <h2>{title}</h2>
          <button className="icon-btn" onClick={onClose} aria-label="Close"><X size={20} /></button>
        </div>
        <div className="modal-body">{children}</div>
        {footer && <div className="modal-foot">{footer}</div>}
      </div>
    </div>
  );
}

// ---------------- Confirm ----------------
interface ConfirmOpts { title: string; message: ReactNode; confirmText?: string; danger?: boolean; }
const ConfirmCtx = createContext<(o: ConfirmOpts) => Promise<boolean>>(async () => false);

export function ConfirmProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<(ConfirmOpts & { resolve: (v: boolean) => void }) | null>(null);
  const confirm = useCallback((o: ConfirmOpts) => new Promise<boolean>((resolve) => setState({ ...o, resolve })), []);
  const close = (v: boolean) => { state?.resolve(v); setState(null); };
  return (
    <ConfirmCtx.Provider value={confirm}>
      {children}
      <Modal
        open={!!state}
        title={state?.title ?? ''}
        onClose={() => close(false)}
        footer={<>
          <Button variant="ghost" onClick={() => close(false)}>Cancel</Button>
          <Button variant={state?.danger ? 'danger' : 'primary'} onClick={() => close(true)}>{state?.confirmText ?? 'Confirm'}</Button>
        </>}
      >
        <div className="confirm-msg">{state?.message}</div>
      </Modal>
    </ConfirmCtx.Provider>
  );
}
export const useConfirm = () => useContext(ConfirmCtx);

// ---------------- Toasts ----------------
interface Toast { id: number; kind: 'success' | 'error'; text: string; }
const ToastCtx = createContext<{ success: (t: string) => void; error: (t: string) => void }>({ success: () => {}, error: () => {} });

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([]);
  const nextId = useRef(1);
  const push = useCallback((kind: Toast['kind'], text: string) => {
    const id = nextId.current++;
    setToasts((t) => [...t, { id, kind, text }]);
    setTimeout(() => setToasts((t) => t.filter((x) => x.id !== id)), kind === 'error' ? 6000 : 3000);
  }, []);
  const value = useRef({ success: (t: string) => push('success', t), error: (t: string) => push('error', t) });
  return (
    <ToastCtx.Provider value={value.current}>
      {children}
      <div className="toasts" aria-live="polite">
        {toasts.map((t) => (
          <div key={t.id} className={`toast toast-${t.kind}`}>
            {t.kind === 'success' ? <CheckCircle2 size={18} /> : <AlertCircle size={18} />}
            <span>{t.text}</span>
          </div>
        ))}
      </div>
    </ToastCtx.Provider>
  );
}
export const useToast = () => useContext(ToastCtx);

// ---------------- Data hook ----------------
export function useAsync<T>(fn: () => Promise<T>, deps: unknown[]) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [tick, setTick] = useState(0);
  useEffect(() => {
    let alive = true;
    setLoading(true);
    setError(null);
    fn()
      .then((d) => alive && setData(d))
      .catch((e: Error) => alive && setError(e.message))
      .finally(() => alive && setLoading(false));
    return () => { alive = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, tick]);
  return { data, setData, error, loading, reload: () => setTick((t) => t + 1) };
}
