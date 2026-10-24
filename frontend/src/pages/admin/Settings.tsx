import { useEffect, useRef, useState, type ChangeEvent, type FormEvent } from 'react';
import {
  Building2, CalendarOff, Clock, Globe2, ImagePlus, ImageUp, Save, Settings as SettingsIcon, TicketCheck, Trash2, UtensilsCrossed,
} from 'lucide-react';
import { api } from '../../api/client';
import type { Setting } from '../../api/types';
import { useBranding, type BrandingInfo } from '../../components/Branding';
import {
  Button, Card, Checkbox, ErrorBox, Field, Input, PageHeader, Spinner, useAsync, useConfirm, useToast,
} from '../../components/ui';

const WEEKDAYS = ['Saturday', 'Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'];
const LOGO_TYPES = ['image/png', 'image/jpeg', 'image/webp'];
const LOGO_MAX_BYTES = 1024 * 1024; // keep in sync with BrandingController.MaxLogoBytes

function CompanyLogoCard() {
  const toast = useToast();
  const confirm = useConfirm();
  const branding = useBranding();
  const fileRef = useRef<HTMLInputElement>(null);
  const [preview, setPreview] = useState<string | null>(null);
  const [file, setFile] = useState<File | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => () => { if (preview) URL.revokeObjectURL(preview); }, [preview]);

  function pick(e: ChangeEvent<HTMLInputElement>) {
    const f = e.target.files?.[0];
    e.target.value = '';
    if (!f) return;
    if (!LOGO_TYPES.includes(f.type)) { toast.error('Please choose a PNG, JPG or WebP image.'); return; }
    if (f.size > LOGO_MAX_BYTES) { toast.error('The logo must be 1 MB or smaller.'); return; }
    setFile(f);
    setPreview(URL.createObjectURL(f));
  }

  function cancel() {
    setFile(null);
    setPreview(null);
  }

  async function upload() {
    if (!file) return;
    setBusy(true);
    try {
      await api.upload<BrandingInfo>('/api/branding/logo', file);
      branding.reload();
      cancel();
      toast.success('Company logo updated.');
    } catch (err) {
      toast.error((err as Error).message);
    } finally {
      setBusy(false);
    }
  }

  async function remove() {
    const ok = await confirm({
      title: 'Remove company logo',
      message: 'The meal icon will be shown instead of your logo. Continue?',
      confirmText: 'Remove',
      danger: true,
    });
    if (!ok) return;
    setBusy(true);
    try {
      await api.del('/api/branding/logo');
      branding.reload();
      toast.success('Company logo removed.');
    } catch (err) {
      toast.error((err as Error).message);
    } finally {
      setBusy(false);
    }
  }

  const shown = preview ?? branding.logoSrc;
  return (
    <Card title="Company logo" icon={ImagePlus}>
      <div className="logo-setup">
        <div className="logo-preview">
          {shown ? <img src={shown} alt="Company logo preview" /> : <UtensilsCrossed size={36} />}
        </div>
        <div className="logo-actions">
          <p className="muted">
            Shown in the menu, on the sign-in page and in the mobile app.
            <br />PNG, JPG or WebP, up to 1 MB. A square image or a transparent PNG works best.
          </p>
          <input ref={fileRef} className="file-input" type="file" accept={LOGO_TYPES.join(',')} onChange={pick} />
          <div className="row">
            {file ? (
              <>
                <Button type="button" onClick={upload} loading={busy}>{!busy && <ImageUp size={16} />} Save logo</Button>
                <Button type="button" variant="ghost" onClick={cancel} disabled={busy}>Cancel</Button>
              </>
            ) : (
              <>
                <Button type="button" variant="ghost" onClick={() => fileRef.current?.click()} disabled={busy}>
                  <ImagePlus size={16} /> {branding.logoSrc ? 'Change logo' : 'Upload logo'}
                </Button>
                {branding.logoSrc && (
                  <Button type="button" variant="danger" onClick={remove} loading={busy}>{!busy && <Trash2 size={16} />} Remove</Button>
                )}
              </>
            )}
          </div>
          {file && <span className="field-hint">Preview of {file.name}. Click “Save logo” to apply it.</span>}
        </div>
      </div>
    </Card>
  );
}

export default function SettingsPage() {
  const toast = useToast();
  const branding = useBranding();
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
      branding.reload();
      toast.success('Settings saved. Upcoming meals were updated.');
    } catch (err) {
      toast.error((err as Error).message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <PageHeader icon={SettingsIcon} title="Settings" subtitle="Company branding and rules for booking meals" />
      {list.error && <ErrorBox message={list.error} onRetry={list.reload} />}
      <div className="stack" style={{ maxWidth: 760 }}>
        <CompanyLogoCard />
        {!list.data ? <Spinner /> : (
          <form onSubmit={save} className="stack">
            <Card title="Meal booking" icon={Clock}>
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
            <Card title="Weekly off days (no regular meal)" icon={CalendarOff}>
              <div className="row">
                {WEEKDAYS.map((d) => <Checkbox key={d} label={d} checked={offDays.includes(d)} onChange={(on) => toggleDay(d, on)} />)}
              </div>
            </Card>
            <Card title="General" icon={Building2}>
              <div className="form-grid">
                <Field label="Company name" hint="Shown in the menu, sign-in page and Excel reports">
                  <Input icon={Building2} required value={v('CompanyName')} onChange={(e) => set('CompanyName', e.target.value)} />
                </Field>
                <Field label="Time zone" hint="IANA or Windows id, e.g. Asia/Dhaka">
                  <Input icon={Globe2} required value={v('TimeZoneId')} onChange={(e) => set('TimeZoneId', e.target.value)} />
                </Field>
                <Field label="One-time code valid for (hours)" hint="Activation / password reset codes issued by the admin (1–168)">
                  <Input icon={TicketCheck} type="number" min={1} max={168} required value={v('SetupCodeHours') || '24'}
                    onChange={(e) => set('SetupCodeHours', e.target.value)} />
                </Field>
              </div>
            </Card>
            <div><Button type="submit" loading={busy}>{!busy && <Save size={16} />} Save settings</Button></div>
          </form>
        )}
      </div>
    </>
  );
}
