import { useEffect, useState, type FormEvent } from 'react';
import { api } from '../../api/client';
import type { Setting } from '../../api/types';
import { Button, Card, Checkbox, ErrorBox, Field, Input, PageHeader, Spinner, useAsync, useToast } from '../../components/ui';

const WEEKDAYS = ['Saturday', 'Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'];

export default function SettingsPage() {
  const toast = useToast();
  const list = useAsync(() => api.get<Setting[]>('/api/settings'), []);
  const [values, setValues] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (list.data) setValues(Object.fromEntries(list.data.map((s) => [s.key, s.value])));
  }, [list.data]);

  const v = (k: string) => values[k] ?? '';
  const set = (k: string, val: string) => setValues((x) => ({ ...x, [k]: val }));
  const offDays = v('WeeklyOffDays').split(',').map((s) => s.trim()).filter(Boolean);
  const toggleDay = (d: string, on: boolean) =>
    set('WeeklyOffDays', (on ? [...offDays, d] : offDays.filter((x) => x !== d)).join(','));

  async function save(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    try {
      list.setData(await api.put<Setting[]>('/api/settings', values));
      toast.success('Settings saved. Upcoming meals were updated.');
    } catch (err) {
      toast.error((err as Error).message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <PageHeader title="Settings" subtitle="Rules for booking meals" />
      {list.error && <ErrorBox message={list.error} onRetry={list.reload} />}
      {!list.data ? <Spinner /> : (
        <form onSubmit={save} className="stack" style={{ maxWidth: 760 }}>
          <Card title="Meal booking">
            <div className="form-grid">
              <Field label="Booking window (days)" hint="How many days ahead employees can turn meals ON/OFF">
                <Input type="number" min={1} max={60} required value={v('BookingWindowDays')} onChange={(e) => set('BookingWindowDays', e.target.value)} />
              </Field>
              <Field label="Daily cut-off time" hint="A day's meal locks at this time on the previous day">
                <Input type="time" required value={v('CutoffTime')} onChange={(e) => set('CutoffTime', e.target.value)} />
              </Field>
              <div className="span-2">
                <Checkbox
                  label="Default-eligible employees can turn their meal OFF"
                  checked={v('AllowEligibleOptOut') === 'true'}
                  onChange={(on) => set('AllowEligibleOptOut', on ? 'true' : 'false')}
                />
              </div>
            </div>
          </Card>
          <Card title="Weekly off days (no regular meal)">
            <div className="row">
              {WEEKDAYS.map((d) => <Checkbox key={d} label={d} checked={offDays.includes(d)} onChange={(on) => toggleDay(d, on)} />)}
            </div>
          </Card>
          <Card title="General">
            <div className="form-grid">
              <Field label="Company name" hint="Shown on Excel reports"><Input required value={v('CompanyName')} onChange={(e) => set('CompanyName', e.target.value)} /></Field>
              <Field label="Time zone" hint="IANA or Windows id, e.g. Asia/Dhaka"><Input required value={v('TimeZoneId')} onChange={(e) => set('TimeZoneId', e.target.value)} /></Field>
            </div>
          </Card>
          <div><Button type="submit" loading={busy}>Save settings</Button></div>
        </form>
      )}
    </>
  );
}
