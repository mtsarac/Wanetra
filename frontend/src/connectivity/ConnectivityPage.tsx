import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api'
import { ConnectionChain } from './ConnectionChain'
import { useConnectivity } from './ConnectionSummary'
import { formatClock, scopeLabels, stateLabels } from './format'
import { LatencyChart } from './LatencyChart'
import { OutagesPanel } from './OutagesPanel'
import { TargetsPanel } from './TargetsPanel'
import { UptimeCards } from './UptimeCards'
import './connectivity.css'

export default function ConnectivityPage() {
  const connectivity = useConnectivity()
  const targets = useQuery({ queryKey: ['probe-targets'], queryFn: api.getProbeTargets })
  const status = connectivity.data

  return (
    <main id="main-content" className="page-content">
      <header className="page-title">
        <div>
          <p className="kicker">Continuous monitoring</p>
          <h1>Connectivity</h1>
        </div>
        <span className="muted" role="status" aria-live="polite">
          {status ? `Every ${status.intervalSeconds}s · last check ${formatClock(status.lastCheckedAt)}` : 'Loading…'}
        </span>
      </header>

      {connectivity.isError && <div className="error" role="alert">{connectivity.error.message}</div>}

      <section className="panel" aria-labelledby="path-heading">
        <div className="panel-head">
          <div>
            <p className="kicker">Right now</p>
            <h2 id="path-heading">Connection path</h2>
          </div>
        </div>
        {status ? (
          <>
            <div className={`state-banner ${status.state}`} role="status" aria-live="polite">
              <strong>{stateLabels[status.state]}</strong>
              <span>
                {status.activeOutage
                  ? `${scopeLabels[status.activeOutage.scope]}: ${status.activeOutage.reason} (since ${formatClock(status.activeOutage.startedAt)})`
                  : status.reason ?? (status.monitoring ? '' : 'Turn monitoring on under Settings → Connection monitoring.')}
              </span>
            </div>
            <ConnectionChain status={status} />
          </>
        ) : (
          <div className="empty">Checking the connection…</div>
        )}
      </section>

      <section className="panel" aria-labelledby="uptime-heading">
        <div className="panel-head">
          <div>
            <p className="kicker">Availability</p>
            <h2 id="uptime-heading">Uptime</h2>
          </div>
          <span className="muted">From recorded outages</span>
        </div>
        <UptimeCards />
      </section>

      <LatencyChart targets={targets.data ?? []} />
      <OutagesPanel />
      <TargetsPanel status={status} />
    </main>
  )
}
