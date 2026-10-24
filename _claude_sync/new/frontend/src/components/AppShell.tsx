import { useEffect, useState } from 'react';
import { NavLink, Outlet, useLocation } from 'react-router-dom';
import {
  UtensilsCrossed, History, UserRound, LayoutDashboard, Users, ClipboardList, Wallet,
  CalendarDays, Soup, Settings, FileSpreadsheet, Menu, LogOut, Building2,
} from 'lucide-react';
import { useAuth } from '../auth/AuthContext';
import { Button } from './ui';
import { BrandLogo, useBranding } from './Branding';

const employeeNav = [
  { to: '/meals', label: 'My Meals', icon: UtensilsCrossed },
  { to: '/history', label: 'History', icon: History },
  { to: '/profile', label: 'Profile', icon: UserRound },
];

const adminNav = [
  { to: '/admin', label: 'Dashboard', icon: LayoutDashboard, end: true },
  { to: '/admin/meal-board', label: 'Meal Board', icon: ClipboardList },
  { to: '/admin/employees', label: 'Employees', icon: Users },
  { to: '/admin/costs', label: 'Daily Cost', icon: Wallet },
  { to: '/admin/meal-types', label: 'Meals & Occasions', icon: Soup },
  { to: '/admin/holidays', label: 'Holidays', icon: CalendarDays },
  { to: '/admin/master-data', label: 'Departments', icon: Building2 },
  { to: '/admin/reports', label: 'Reports', icon: FileSpreadsheet },
  { to: '/admin/settings', label: 'Settings', icon: Settings },
];

/** Company logo when uploaded (Settings → Company logo), otherwise the meal icon. */
export function Logo({ size = 22 }: { size?: number }) {
  return <BrandLogo size={size} />;
}

export default function AppShell() {
  const { user, isAdmin, signOut } = useAuth();
  const [open, setOpen] = useState(false);
  const loc = useLocation();
  const { companyName, logoSrc } = useBranding();
  useEffect(() => setOpen(false), [loc.pathname]);

  const current = [...adminNav, ...employeeNav].find((n) =>
    n.to === '/admin' ? loc.pathname === '/admin' : loc.pathname.startsWith(n.to));

  return (
    <div className="shell">
      <aside className={`sidebar ${open ? 'open' : ''}`}>
        <div className="brand">
          <span className={`brand-mark ${logoSrc ? 'has-logo' : ''}`}><Logo size={20} /></span>
          <span className="brand-text">
            <b>{companyName || 'Meal Management'}</b>
            {companyName && <small>Meal Management</small>}
          </span>
        </div>
        <nav className="nav-group">
          <div className="nav-title">My meals</div>
          {employeeNav.map((n) => (
            <NavLink key={n.to} to={n.to} className="nav-link"><n.icon size={18} />{n.label}</NavLink>
          ))}
        </nav>
        {isAdmin && (
          <nav className="nav-group">
            <div className="nav-title">Admin</div>
            {adminNav.map((n) => (
              <NavLink key={n.to} to={n.to} end={n.end} className="nav-link"><n.icon size={18} />{n.label}</NavLink>
            ))}
          </nav>
        )}
        <div className="sidebar-foot">
          <div className="who">{user?.employeeName}</div>
          <div>{user?.employeeCode} · {user?.department}</div>
          <Button variant="ghost" size="sm" onClick={signOut}><LogOut size={14} /> Sign out</Button>
        </div>
      </aside>
      {open && <div className="scrim" onClick={() => setOpen(false)} />}

      <div className="main">
        <header className="topbar">
          <button className="icon-btn" aria-label="Menu" onClick={() => setOpen(true)}><Menu size={22} /></button>
          <span className={`brand-mark ${logoSrc ? 'has-logo' : ''}`}><Logo size={17} /></span>
          <span className="title">{current?.label ?? 'Meal Management'}</span>
        </header>
        <main className="content">
          <Outlet />
        </main>
        <nav className="bottom-nav">
          {employeeNav.map((n) => (
            <NavLink key={n.to} to={n.to}><n.icon size={20} />{n.label}</NavLink>
          ))}
          {isAdmin && (
            <NavLink to="/admin" end={false}><LayoutDashboard size={20} />Admin</NavLink>
          )}
        </nav>
      </div>
    </div>
  );
}
