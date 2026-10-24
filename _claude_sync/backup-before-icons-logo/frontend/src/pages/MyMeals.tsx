import { useState } from 'react';
import { Lock } from 'lucide-react';
import { api } from '../api/client';
import type { MyMealDay, MyMealItem, MyMeals as MyMealsData } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import { Empty, ErrorBox, MealPill, PageHeader, Pill, Spinner, Toggle, useAsync, useToast } from '../components/ui';
import { fmtDateTime, fmtDayShort, fmtMoney } from '../utils/format';

function cutoffLabel(t: string) {
  const [h, m] = t.split(':').map(Number);
  return `${((h + 11) % 12) + 1}:${String(m).padStart(2, '0')} ${h < 12 ? 'AM' : 'PM'}`;
}

export default function MyMeals() {
  const { user } = useAuth();
  const toast = useToast();
  const { data, setData, error, loading, reload } = useAsync(() => api.get<MyMealsData>('/api/my/meals'), []);
  const [saving, setSaving] = useState<string | null>(null);

  async function change(day: MyMealDay, meal: MyMealItem, isOn: boolean) {
    const key = `${day.date}-${meal.mealTypeId}`;
    setSaving(key);
    try {
      const res = await api.put<MyMealsData>('/api/my/meals', { mealDate: day.date, mealTypeId: meal.mealTypeId, isOn });
      setData(res);
      toast.success(`${meal.mealTypeName} ${isOn ? 'ON' : 'OFF'} for ${fmtDayShort(day.date)}`);
    } catch (e) {
      toast.error((e as Error).message);
      reload();
    } finally {
      setSaving(null);
    }
  }

  const upcomingOn = data?.days.filter((d) => !d.isToday).flatMap((d) => d.meals).filter((m) => m.isOn) ?? [];

  return (
    <>
      <PageHeader
        title="My Meals"
        subtitle={user ? `${user.employeeName} · ${user.department}` : undefined}
      />
      {error && <ErrorBox message={error} onRetry={reload} />}
      {loading && !data && <Spinner />}
      {data && (
        <>
          <div className="banner">
            You can change meals for the next <b>{data.bookingWindowDays} days</b>. Each day closes at{' '}
            <b>{cutoffLabel(data.cutoffTime)}</b> the day before.
            {upcomingOn.length > 0 && <> You have <b>{upcomingOn.length}</b> upcoming meal{upcomingOn.length > 1 ? 's' : ''} ON.</>}
          </div>
          {data.days.length === 0 && <Empty title="No meal days" />}
          <div className="day-list">
            {data.days.map((day) => {
              const anyServed = day.meals.some((m) => m.isServed);
              const locked = day.isToday || !day.meals.some((m) => m.isOpen);
              return (
                <div key={day.date} className={`day-card ${!anyServed ? 'closed' : locked ? 'locked' : ''}`}>
                  <div className="day-top">
                    <span className="day-date">{day.isToday ? `Today · ${fmtDayShort(day.date)}` : fmtDayShort(day.date)}</span>
                    {anyServed && locked && <span className="day-tag"><Lock size={12} /> Locked</span>}
                  </div>
                  {day.meals.map((m) => {
                    const key = `${day.date}-${m.mealTypeId}`;
                    if (!m.isServed) {
                      return (
                        <div className="meal-line" key={key}>
                          <div>
                            <div className="meal-name">{m.mealTypeName}</div>
                            <div className="meal-meta">{m.notServedReason}</div>
                          </div>
                          <Pill tone="muted">{m.notServedReason?.startsWith('Holiday') ? 'Holiday' : 'No meal'}</Pill>
                        </div>
                      );
                    }
                    const offBlocked = m.isOn && m.isOpen && !m.canChange;
                    return (
                      <div className="meal-line" key={key}>
                        <div>
                          <div className="meal-name">
                            {m.mealTypeName}{m.occasionTitle ? ` · ${m.occasionTitle}` : ''}
                          </div>
                          <div className="meal-meta">
                            {m.isEligible ? 'Default meal' : 'On request'}
                            {m.source && m.source !== 'Auto' ? ` · Changed by ${m.source === 'Employee' ? 'you' : 'admin'}` : ''}
                            {m.employeeCost > 0 ? ` · Your share ${fmtMoney(m.employeeCost)}` : ''}
                            {m.isOpen && m.canChange ? ` · Change by ${fmtDateTime(m.lockTime)}` : ''}
                            {offBlocked ? ' · Contact admin to turn off' : ''}
                          </div>
                        </div>
                        <div className="meal-right">
                          <MealPill on={m.isOn} />
                          <Toggle
                            on={m.isOn}
                            label={`${m.mealTypeName} ${fmtDayShort(day.date)}`}
                            disabled={!m.canChange || saving === key}
                            onChange={(v) => change(day, m, v)}
                          />
                        </div>
                      </div>
                    );
                  })}
                </div>
              );
            })}
          </div>
        </>
      )}
    </>
  );
}
