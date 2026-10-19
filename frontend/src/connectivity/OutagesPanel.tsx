import { useQuery } from '@tanstack/react-query'
import { api, type Outage } from '../lib/api'
import { formatClock, formatDuration, scopeLabels } from './format'

function notice(outage: Outage): string {
  if (!outage.notificationDeliveries.length) return 'No notification destinations'
  const opened = outage.notificationDeliveries.filter((delivery) => delivery.trigger === 'opened')
  const delivered = opened.filter((delivery) => delivery.status === 'delivered').length
  return `${delivered}/${opened.length} delivered`
}

export function OutagesPanel() {
  const outages = useQuery({ queryKey: ['outages'], queryFn: () => api.getOutages(20), refetchInterval: 30_000 })

  return (
    <section className="panel" aria-labelledby="outages-heading">
      <div className="panel-head">
        <div>
          <p className="kicker">Timeline</p>
          <h2 id="outages-heading">Outages</h2>
        </div>
        <span className="muted">Latest 20</span>
      </div>
      {outages.isError && <div className="error" role="alert">{outages.error.message}</div>}
      {outages.isLoading ? (
        <div className="empty">Loading outages…</div>
      ) : outages.data?.length ? (
        <div className="table-wrap" tabIndex={0} role="region" aria-label="Outages table">
          <table>
            <caption className="sr-only">Recorded connection outages</caption>
            <thead>
              <tr>
                <th scope="col">Started</th>
                <th scope="col">Duration</th>
                <th scope="col">Where</th>
                <th scope="col">Status</th>
                <th scope="col">Notification</th>
              </tr>
            </thead>
            <tbody>
              {outages.data.map((outage) => (
                <tr key={outage.id}>
                  <th scope="row">{formatClock(outage.startedAt)}</th>
                  <td>{formatDuration(outage.durationSeconds)}</td>
                  <td>{scopeLabels[outage.scope]}<br /><small className="muted">{outage.reason}</small></td>
                  <td className={outage.status === 'active' ? 'failure' : 'success'}>
                    {outage.status === 'active' ? 'Ongoing' : outage.closureReason ? `Closed (${outage.closureReason})` : 'Recovered'}
                  </td>
                  <td>{notice(outage)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <div className="empty">No outages recorded. Nice.</div>
      )}
    </section>
  )
}
