import { Fragment, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { api, type SpeedTestResult } from './lib/api'

const initialFrom = new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10)
const today = new Date().toISOString().slice(0, 10)

function formatTime(value: string) {
  return new Date(value).toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' })
}

function metric(value: number | null, unit: string) {
  return value == null ? '—' : `${value.toFixed(1)} ${unit}`
}

function Details({ result }: { result: SpeedTestResult }) {
  return (
    <section className="detail-panel" aria-label={`Details for test ${result.id}`}>
      <div><span>Server</span><strong>{result.serverName ?? '—'}</strong></div>
      <div><span>Location</span><strong>{result.serverLocation ?? '—'}</strong></div>
      <div><span>Engine</span><strong>{result.engine}</strong></div>
      <div><span>Duration</span><strong>{metric(result.durationMs, 'ms')}</strong></div>
      <div><span>ISP</span><strong>{result.isp ?? '—'}</strong></div>
      <div><span>External IP</span><strong>{result.externalIp ?? '—'}</strong></div>
      {result.errorMessage && <p className="failure detail-error">{result.errorMessage}</p>}
    </section>
  )
}

export default function HistoryPage() {
  const [from, setFrom] = useState(initialFrom)
  const [to, setTo] = useState(today)
  const [outcome, setOutcome] = useState<'all' | 'success' | 'failed'>('all')
  const [sort, setSort] = useState<'asc' | 'desc'>('desc')
  const [page, setPage] = useState(1)
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const query = useQuery({
    queryKey: ['history-page', from, to, outcome, sort, page],
    queryFn: () => api.getHistoryPage({
      from: from ? new Date(`${from}T00:00:00`) : undefined,
      to: to ? new Date(`${to}T23:59:59.999`) : undefined,
      success: outcome === 'all' ? undefined : outcome === 'success',
      sort,
      page,
      pageSize: 25,
    }),
  })

  function updateFilter(action: () => void) {
    action()
    setPage(1)
    setSelectedId(null)
  }

  return (
    <main className="page-content">
      <header className="page-title"><div><p className="kicker">MEASUREMENTS / ARCHIVE</p><h1>History</h1></div><span className="muted">{query.data ? `${query.data.totalCount} results` : 'Filtering saved tests'}</span></header>
      <section className="panel history-panel">
        <div className="filter-bar">
          <label><span>From</span><input type="date" value={from} max={to || undefined} onChange={(event) => updateFilter(() => setFrom(event.target.value))} /></label>
          <label><span>To</span><input type="date" value={to} min={from || undefined} onChange={(event) => updateFilter(() => setTo(event.target.value))} /></label>
          <label><span>Outcome</span><select value={outcome} onChange={(event) => updateFilter(() => setOutcome(event.target.value as typeof outcome))}><option value="all">All tests</option><option value="success">Successful</option><option value="failed">Failed</option></select></label>
          <label><span>Sort</span><select value={sort} onChange={(event) => updateFilter(() => setSort(event.target.value as typeof sort))}><option value="desc">Newest first</option><option value="asc">Oldest first</option></select></label>
        </div>
        {query.error && <div className="error" role="alert">{query.error.message}</div>}
        {query.isLoading ? <div className="empty">Loading speed tests…</div> : query.data?.items.length ? (
          <div className="table-wrap">
            <table className="history-table"><thead><tr><th>Timestamp</th><th>Download</th><th>Upload</th><th>Latency</th><th>Jitter</th><th>Packet loss</th><th>Server</th><th>Engine</th><th>Status</th><th /></tr></thead>
              <tbody>{query.data.items.map((result) => (
                <Fragment key={result.id}>
                  <tr>
                    <td>{formatTime(result.timestamp)}</td><td>{metric(result.downloadMbps, 'Mbps')}</td><td>{metric(result.uploadMbps, 'Mbps')}</td><td>{metric(result.latencyMs, 'ms')}</td><td>{metric(result.jitterMs, 'ms')}</td><td>{metric(result.packetLossPercent, '%')}</td><td>{result.serverName ?? '—'}</td><td>{result.engine}</td><td className={result.success ? 'success' : 'failure'}>{result.success ? 'Success' : 'Failed'}</td>
                    <td><button className="text-button" onClick={() => setSelectedId(selectedId === result.id ? null : result.id)}>{selectedId === result.id ? 'Hide' : 'Details'}</button></td>
                  </tr>
                  {selectedId === result.id && <tr><td colSpan={10}><Details result={result} /></td></tr>}
                </Fragment>
              ))}</tbody>
            </table>
          </div>
        ) : <div className="empty">No measurements match these filters.</div>}
        {query.data && query.data.totalPages > 1 && <div className="pagination"><span>Page {query.data.page} of {query.data.totalPages}</span><div><button disabled={page <= 1} onClick={() => setPage((current) => current - 1)}>Previous</button><button disabled={page >= query.data!.totalPages} onClick={() => setPage((current) => current + 1)}>Next</button></div></div>}
      </section>
    </main>
  )
}
