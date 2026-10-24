import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react';
import { UtensilsCrossed } from 'lucide-react';
import { api, API_BASE } from '../api/client';

export interface BrandingInfo {
  companyName: string;
  /** Relative API path to the logo (with a version query), or null when no logo is set. */
  logoUrl: string | null;
}

interface BrandingCtx extends BrandingInfo {
  logoSrc: string | null;
  reload: () => void;
}

const Ctx = createContext<BrandingCtx>({ companyName: '', logoUrl: null, logoSrc: null, reload: () => {} });

const CACHE_KEY = 'mm.branding';
function readCache(): BrandingInfo | null {
  try { const v = localStorage.getItem(CACHE_KEY); return v ? JSON.parse(v) as BrandingInfo : null; } catch { return null; }
}
function writeCache(b: BrandingInfo) {
  try { localStorage.setItem(CACHE_KEY, JSON.stringify(b)); } catch { /* storage unavailable */ }
}

/** Loads the company name and logo once (public endpoint, works before sign-in). */
export function BrandingProvider({ children }: { children: ReactNode }) {
  const [info, setInfo] = useState<BrandingInfo>(() => readCache() ?? { companyName: '', logoUrl: null });

  const reload = useCallback(() => {
    api.get<BrandingInfo>('/api/branding')
      .then((b) => { setInfo(b); writeCache(b); })
      .catch(() => { /* keep cached/default branding */ });
  }, []);

  useEffect(() => { reload(); }, [reload]);

  const logoSrc = info.logoUrl ? API_BASE + info.logoUrl : null;
  return <Ctx.Provider value={{ ...info, logoSrc, reload }}>{children}</Ctx.Provider>;
}

export const useBranding = () => useContext(Ctx);

/** Company logo if one is uploaded, otherwise the meal icon. Fills its container. */
export function BrandLogo({ size = 22 }: { size?: number }) {
  const { logoSrc, companyName } = useBranding();
  const [failed, setFailed] = useState(false);
  useEffect(() => setFailed(false), [logoSrc]);
  if (logoSrc && !failed) {
    return <img className="logo-img" src={logoSrc} alt={companyName ? `${companyName} logo` : 'Company logo'} onError={() => setFailed(true)} />;
  }
  return <UtensilsCrossed size={size} strokeWidth={2.2} />;
}

/** Wrapper class helper: adds "has-logo" so the container switches to a white tile. */
export function useHasLogo() {
  return !!useBranding().logoSrc;
}
