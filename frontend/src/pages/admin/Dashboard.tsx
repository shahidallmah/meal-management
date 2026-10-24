import { Link } from 'react-router-dom';
import {
  BarChart3, CalendarClock, ChefHat, ClipboardList, FileSpreadsheet, LayoutDashboard, Sun,
  UserPlus, Users, Wallet,
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { mealIcon } from '../../utils/mealIcon';
import { api } from '../../api/client';
import type { Dashboard as DashboardData, MealCount } from '../../api/types';
import { Button, Card, ErrorBox, IconChip, PageHeader, Pill, Spinner, Stat, useAsync } from '../../components/ui';
import { fmtDate, fmtMoney, fmtNumber, parseDate } from '../../utils/format';

function MealDayCard({ title, meals, date, icon }: { title: string; meals: MealCount[]; date?: string; icon: LucideIcon }) {
  return (
    <Card title={title} icon={icon} actions={date && <Link to={`/admin/meal-board?date=${date}`} className="btn btn-text btn-sm"><ClipboardList size={15} /> Open board</Link>}>
      {meals.length === 0 && <p className="muted">No meal types configured.</p>}
      <div className="stack">
        {meals.map((m) => (
          <div key={m.mealTypeId} className="row" style={{ justifyContent: 'space-between', flexWrap: 'nowrap' }}>
            <div className="meal-left">
            <IconChip icon={mealIcon(m.mealTypeName)} tone={m.isServed ? 'warm' : 'muted'} />
            <div>
              <div style={{ fontWeight: 600 }}>{m.mealTypeName}{m.occasionTitle ? ` · ${m.occasionTitle}` : ''}</div>
              <div className="muted">
                {m.isServed ? `${fmtMoney(m.costPerMeal)} per meal · employee ${fmtMoney(m.employeeCostPerMeal)}` : m.notServedReason}
              </div>
            </div>
            </div>
            {m.isServed ? (
              <div style={{ textAlign: 'right' }}>
                <div className="stat-value tone-accent" style={{ fontSize: '1.5rem' }}>{m.onCount}</div>
                <div className="muted">{fmtMoney(m.totalCost)}</div>
              </div>
            ) : (
              <Pill tone="muted">No meal</Pill>
            )}
          </div>
        ))}
      </div>
    </Card>
  );
}

export default function Dashboard() {
  const { data, error, loading, reload } = useAsync(() => api.get<DashboardData>('/api/dashboard'), []);
  if (error) return <ErrorBox message={error} onRetry={reload} />;
  if (loading || !data) return <Spinner />;

  const m = data.month;
  const empPct = m.totalCost > 0 ? (m.employeeTotal / m.totalCost) * 100 : 0;
  const maxTrend = Math.max(1, ...data.trend.map((t) => t.meals));
  const tomorrow = data.tomorrowMeals[0]?.date;

  return (
    <>
      <PageHeader
        title="Dashboard"
        icon={LayoutDashboard}
        subtitle={fmtDate(data.today)}
        actions={<>
          <Link to="/admin/reports"><Button variant="ghost"><FileSpreadsheet size={17} /> Reports</Button></Link>
          <Link to="/admin/costs"><Button><Wallet size={17} /> Set daily cost</Button></Link>
        </>}
      />
      <div className="stack">
        <div className="grid grid-2">
          <MealDayCard title="Today" icon={Sun} meals={data.todayMeals} date={data.today} />
          <MealDayCard title="Tomorrow" icon={CalendarClock} meals={data.tomorrowMeals} date={tomorrow} />
        </div>

        <div className="grid grid-4">
          <Stat label="Meals this month" icon={ChefHat} chip="warm" value={fmtNumber(m.meals)} tone="accent" sub={`${fmtDate(m.from)} – ${fmtDate(m.to)}`} />
          <Stat label="Total cost this month" icon={Wallet} chip="mint" value={fmtMoney(m.totalCost)} sub={
            <>
              <span className="split-bar" style={{ display: 'flex' }}>
                <i style={{ width: `${empPct}%`, background: 'var(--cost-employee)' }} />
                <i style={{ width: `${100 - empPct}%`, background: 'var(--cost-company)' }} />
              </span>
              <span className="legend">
                <span style={{ ['--c' as string]: 'var(--cost-employee)' }}>Employee {fmtMoney(m.employeeTotal)}</span>
                <span style={{ ['--c' as string]: 'var(--cost-company)' }}>Company {fmtMoney(m.companyTotal)}</span>
              </span>
            </>
          } />
          <Stat label="Active employees" icon={Users} chip="sky" value={data.employees.active} sub={`${data.employees.defaultEligible} default eligible · ${data.employees.inactive} inactive`} />
          <Stat
            label="Not yet signed up"
            icon={UserPlus}
            chip="violet"
            value={data.employees.withoutPassword}
            sub={<Link to="/admin/employees">Active employees without a password</Link>}
          />
        </div>

        <Card title="Meals — last 14 days" icon={BarChart3}>
          <div className="bars" role="img" aria-label="Meals per day for the last 14 days">
            {data.trend.map((t) => {
              const d = parseDate(t.date);
              return (
                <div key={t.date} className={`bar ${t.date === data.today ? 'today' : ''}`} title={`${fmtDate(t.date)}: ${t.meals} meals, ${fmtMoney(t.totalCost)}`}>
                  <b>{t.meals || ''}</b>
                  <i style={{ height: `${(t.meals / maxTrend) * 100}%` }} />
                  <span>{d.getDate()}</span>
                </div>
              );
            })}
          </div>
        </Card>
      </div>
    </>
  );
}
