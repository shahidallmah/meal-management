import { Link } from 'react-router-dom';
import { api } from '../../api/client';
import type { Dashboard as DashboardData, MealCount } from '../../api/types';
import { Button, Card, ErrorBox, PageHeader, Pill, Spinner, Stat, useAsync } from '../../components/ui';
import { fmtDate, fmtMoney, fmtNumber, parseDate } from '../../utils/format';

function MealDayCard({ title, meals, date }: { title: string; meals: MealCount[]; date?: string }) {
  return (
    <Card title={title} actions={date && <Link to={`/admin/meal-board?date=${date}`} className="btn btn-text btn-sm">Open board</Link>}>
      {meals.length === 0 && <p className="muted">No meal types configured.</p>}
      <div className="stack">
        {meals.map((m) => (
          <div key={m.mealTypeId} className="row" style={{ justifyContent: 'space-between' }}>
            <div>
              <div style={{ fontWeight: 600 }}>{m.mealTypeName}{m.occasionTitle ? ` · ${m.occasionTitle}` : ''}</div>
              <div className="muted">
                {m.isServed ? `${fmtMoney(m.costPerMeal)} per meal · employee ${fmtMoney(m.employeeCostPerMeal)}` : m.notServedReason}
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
        subtitle={fmtDate(data.today)}
        actions={<>
          <Link to="/admin/reports"><Button variant="ghost">Reports</Button></Link>
          <Link to="/admin/costs"><Button>Set daily cost</Button></Link>
        </>}
      />
      <div className="stack">
        <div className="grid grid-2">
          <MealDayCard title="Today" meals={data.todayMeals} date={data.today} />
          <MealDayCard title="Tomorrow" meals={data.tomorrowMeals} date={tomorrow} />
        </div>

        <div className="grid grid-4">
          <Stat label="Meals this month" value={fmtNumber(m.meals)} tone="accent" sub={`${fmtDate(m.from)} – ${fmtDate(m.to)}`} />
          <Stat label="Total cost this month" value={fmtMoney(m.totalCost)} sub={
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
          <Stat label="Active employees" value={data.employees.active} sub={`${data.employees.defaultEligible} default eligible · ${data.employees.inactive} inactive`} />
          <Stat
            label="Not yet signed up"
            value={data.employees.withoutPassword}
            sub={<Link to="/admin/employees">Active employees without a password</Link>}
          />
        </div>

        <Card title="Meals — last 14 days">
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
