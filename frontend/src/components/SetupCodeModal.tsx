import { useState } from 'react';
import { Check, Copy, Phone, ShieldAlert, TicketCheck } from 'lucide-react';
import type { SetupCode } from '../api/types';
import { fmtUtc } from '../utils/format';
import { Button, Modal } from './ui';

/** Shows a freshly issued one-time code to the admin. The code is not stored anywhere, so it is shown once. */
export default function SetupCodeModal({ data, onClose }: { data: SetupCode; onClose: () => void }) {
  const [copied, setCopied] = useState(false);
  const isNew = data.purpose === 'Activation';
  const message =
    `Meal Management: your one-time code is ${data.code} (valid until ${fmtUtc(data.expiresAt)}). ` +
    `Open the app, tap "First time or forgot password? Use a code", and enter your mobile number ${data.mobileNo} with this code.`;

  async function copy() {
    try {
      await navigator.clipboard.writeText(message);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch { /* clipboard not available */ }
  }

  return (
    <Modal
      open
      title={isNew ? 'Activation code' : 'Password reset code'}
      onClose={onClose}
      footer={<>
        <Button variant="ghost" onClick={copy}>{copied ? <Check size={16} /> : <Copy size={16} />} {copied ? 'Copied' : 'Copy instructions'}</Button>
        <Button onClick={onClose}>Done</Button>
      </>}
    >
      <div className="setup-code">
        <p>
          Give this code to <b>{data.employeeName}</b> ({data.employeeCode}) <b>in person or by phone call</b>.
        </p>
        <div className="setup-code-box" aria-label="One-time code">
          <TicketCheck size={22} />
          <span>{data.code}</span>
        </div>
        <ul className="setup-code-facts">
          <li><Phone size={15} /> Use with mobile no <b>{data.mobileNo}</b></li>
          <li><TicketCheck size={15} /> Works once, until <b>{fmtUtc(data.expiresAt)}</b></li>
          {!isNew && <li><ShieldAlert size={15} /> Their current password keeps working until they use the code</li>}
        </ul>
        <div className="alert alert-warn">
          This code is shown only now. Any earlier code for this employee no longer works. Don’t send it to a group chat.
        </div>
      </div>
    </Modal>
  );
}
