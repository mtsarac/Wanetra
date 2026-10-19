import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import { api } from '../lib/api'
import { ConnectionChain } from './ConnectionChain'
import { formatClock, scopeLabels, stateLabels } from './format'
import { UptimeCards } from './UptimeCards'
import './connectivity.css'

export function useConnectivity() {
  return useQuery({
    queryKey: ['connectivity'],
    queryFn: api.getConnectivity,
    refetchInterval: 5_000,
    staleTime: 0,
  })
}

export function ConnectionSummary() {
  const connectivity = useConnectivity()
  const status = connectivity.data

  return (
    <section className="panel connection-summary" aria-labelledby="connection-heading">
      <div className="panel-head">
        <div>
          <p className="kicker">Live connection</p>
          <h2 id="connection-heading">Is the internet up?</h2>
        </div>
        <Link className="summary-link" to="/connectivity">Details</Link>
      </div>
      {connectivity.isError && <div className="error" role="alert">{connectivity.error.message}</div>}
      {status && (
        <>
          <div className={`state-banner ${status.state}`} role="status" aria-live="polite">
            <strong>{stateLabels[status.state]}</strong>
            <span>
              {status.activeOutage
                ? `${scopeLabels[status.activeOutage.scope]} · since ${formatClock(status.activeOutage.startedAt)}`
                : status.reason ?? (status.state === 'healthy' ? `Checked ${formatClock(status.lastCheckedAt)}` : '')}
            </span>
          </div>
          <ConnectionChain status={status} />
          <UptimeCards only="24h" />
        </>
      )}
      {!status && connectivity.isLoading && <div className="empty">Checking the connection…</div>}
    </section>
  )
}
