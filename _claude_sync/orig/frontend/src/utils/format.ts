const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const DAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

/** Parse "yyyy-MM-dd" as a local date (no timezone shift). */
export function parseDate(s: string): Date {
  const [y, m, d] = s.slice(0, 10).split('-').map(Number);
  return new Date(y, m - 1, d);
}

export function toIso(d: Date): string {
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
}

export function addDays(iso: string, days: number): string {
  const d = parseDate(iso);
  d.setDate(d.getDate() + days);
  return toIso(d);
}

export function todayIso(): string { return toIso(new Date()); }
export function monthStartIso(iso = todayIso()): string { return iso.slice(0, 8) + '01'; }
export function monthEndIso(iso = todayIso()): string {
  const d = parseDate(iso);
  return toIso(new Date(d.getFullYear(), d.getMonth() + 1, 0));
}

export function fmtDate(iso?: string | null): string {
  if (!iso) return '—';
  const d = parseDate(iso);
  return `${String(d.getDate()).padStart(2, '0')} ${MONTHS[d.getMonth()]} ${d.getFullYear()}`;
}

export function fmtDayShort(iso: string): string {
  const d = parseDate(iso);
  return `${DAYS[d.getDay()]} ${d.getDate()} ${MONTHS[d.getMonth()]}`;
}

/** Server local date-time ("2026-10-19T17:00:00") → "19 Oct, 5:00 PM". */
export function fmtDateTime(s?: string | null): string {
  if (!s) return '—';
  const [datePart, timePart = '00:00'] = s.split('T');
  const d = parseDate(datePart);
  const [hh, mm] = timePart.split(':').map(Number);
  const h12 = ((hh + 11) % 12) + 1;
  return `${d.getDate()} ${MONTHS[d.getMonth()]}, ${h12}:${String(mm).padStart(2, '0')} ${hh < 12 ? 'AM' : 'PM'}`;
}

/** UTC timestamp from the server → local readable. */
export function fmtUtc(s?: string | null): string {
  if (!s) return '—';
  const d = new Date(s.endsWith('Z') ? s : s + 'Z');
  if (isNaN(d.getTime())) return '—';
  return `${d.getDate()} ${MONTHS[d.getMonth()]} ${d.getFullYear()}, ${d.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })}`;
}

const money = new Intl.NumberFormat('en-IN', { minimumFractionDigits: 0, maximumFractionDigits: 2 });
export function fmtMoney(n?: number | null): string {
  return `৳${money.format(n ?? 0)}`;
}
export function fmtNumber(n?: number | null): string {
  return new Intl.NumberFormat('en-IN').format(n ?? 0);
}

export function sourceLabel(s?: string | null): string {
  if (!s) return '';
  if (s === 'Auto') return 'Default';
  if (s === 'Employee') return 'Self';
  return s;
}
