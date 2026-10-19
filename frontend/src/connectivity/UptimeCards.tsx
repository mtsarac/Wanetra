import { useQuery } from '@tanstack/react-query'
import { api, type UptimeRange } from '../lib/api'
import { formatDuration, formatUptime } from './format'

const ranges: { range: UptimeRange; label: string }[] = [
  { range: '24h', label: 'Last 24 hours' },
  { range: '7d', label: 'Last 7 days' },
  { range: '30d', label: 'Last 30 days' },
]

function UptimeCard({ range, label }: { range: UptimeRange; label: string }) {
  const query = useQuery({
    queryKey: ['uptime', range],
    queryFn: () => api.getUptime(range),
    refetchInterval: 60_000,
  })

  const data = query.data
  return (
    <div className="uptime-card">
      <span>{label}</span>
      <strong>{data ? formatUptime(data.uptimePercent) : '—'}</strong>
      <small>
        {query.isError
          ? 'Unavailable'
          : data
            ? `${data.outageCount} ${data.outageCount === 1 ? 'outage' : 'outages'}${data.outageCount ? ` · ${formatDuration(data.totalOutageSeconds)} down` : ''}${data.coversFullRange ? '' : ' · since monitoring began'}`
            : 'Loading…'}
      </small>
    </div>
  )
}

export function UptimeCards({ only }: { only?: UptimeRange }) {
  const shown = only ? ranges.filter((item) => item.range === only) : ranges
  return (
    <div className="uptime-grid" role="group" aria-label="Uptime">
      {shown.map((item) => <UptimeCard key={item.range} {...item} />)}
    </div>
  )
}
