import { useState } from 'react';
import { api } from '../api/client';
import type { MealHistory } from '../api/types';
import { Card, Empty, ErrorBox, Field, Input, PageHeader, Spinner, Stat, useAsync } from '../components/ui';
import { fmtDate, fmtMoney, monthEndIso, monthStartIso, sourceLabel, todayIso } from '../utils/format';

export default function MealHistoryPage() {
  const [from, setFrom] = useState(monthStartIso());
  const [to, setTo] = useState(todayIso() < monthEndIso() ? todayIso() : monthEndIso());
  const { data, error, loading, reload } = useAsync(() => api.get<MealHistory>('/api/my/history', { from, to }), [from, to]);

  return (
    <>
      <PageHeader title="Meal History" subtitle="Meals you had and your contribution" />
      <div className="filters">
        <Field label="From"><Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} /></Field>
        <Field label="To"><Input type="date" value={to} onChange={(e) => setTo(e.target.value)} /></Field>
      </div>
      {error && <ErrorBox message={error} onRetry={reload} />}
      {loading && !data && <Spinner />}
      {data && (
        <div className="stack">
          <div className="grid grid-4">
            <Stat label="Meals" value={data.mealCount} tone="accent" />
            <Stat label="Your contribution" value={fmtMoney(data.employeeCost)} tone="primary" />
            <Stat label="Company contribution" value={fmtMoney(data.companyCost)} />
            <Stat label="Total meal cost" value={fmtMoney(data.totalCost)} />
          </div>
          <Card title="Details">
            {data.items.length === 0 ? (
              <Empty title="No meals in this period" />
            ) : (
              <div className="table-wrap">
                <table className="table">
                  <thead>
                    <tr><th>Date</th><th>Meal</th><th>Marked by</th><th className="num">Meal cost</th><th className="num">Your share</th><th className="num">Company</th></tr>
                  </thead>
                  <tbody>
                    {data.items.map((i) => (
                      <tr key={`${i.mealDate}-${i.mealTypeId}`}>
                        <td className="nowrap">{fmtDate(i.mealDate)}</td>
                        <td>{i.mealTypeName}</td>
                        <td>{sourceLabel(i.source)}</td>
                        <td className="num">{fmtMoney(i.totalCost)}</td>
                        <td className="num">{fmtMoney(i.employeeCost)}</td>
                        <td className="num">{fmtMoney(i.companyCost)}</td>
                      </tr>
                    ))}
                  </tbody>
                  <tfoot>
                    <tr>
                      <td colSpan={3}>{data.mealCount} meals</td>
                      <td className="num">{fmtMoney(data.totalCost)}</td>
                      <td className="num">{fmtMoney(data.employeeCost)}</td>
                      <td className="num">{fmtMoney(data.companyCost)}</td>
                    </tr>
                  </tfoot>
                </table>
              </div>
            )}
          </Card>
        </div>
      )}
    </>
  );
}
