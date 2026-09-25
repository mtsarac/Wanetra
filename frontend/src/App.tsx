import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { EChartsOption } from 'echarts'
import * as echarts from 'echarts/core'
import { LineChart } from 'echarts/charts'
import { GridComponent, LegendComponent, TooltipComponent } from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'
import { useEffect, useMemo, useRef, useState } from 'react'
import { api, getHistory, type MetricBaseline, type SpeedTestResult } from './lib/api'

echarts.use([LineChart, GridComponent, LegendComponent, TooltipComponent, CanvasRenderer])

type Range = '6H' | '24H' | '7D' | '30D' | '90D' | 'Custom'

const ranges: Record<Exclude<Range, 'Custom'>, number> = {
  '6H': 6 * 60 * 60 * 1000,
  '24H': 24 * 60 * 60 * 1000,
  '7D': 7 * 24 * 60 * 60 * 1000,
  '30D': 30 * 24 * 60 * 60 * 1000,
  '90D': 90 * 24 * 60 * 60 * 1000,
}

function formatNumber(value: number | null | undefined, unit: string) {
  return value == null ? '—' : `${value.toFixed(1)} ${unit}`
}

function formatTime(value: string | null | undefined) {
  return value ? new Date(value).toLocaleString([], { dateStyle: 'short', timeStyle: 'short' }) : '—'
}

function Chart({ results }: { results: SpeedTestResult[] }) {
  const ref = useRef<HTMLDivElement>(null)
  const option = useMemo<EChartsOption>(() => ({
    animation: false,
    tooltip: { trigger: 'axis' },
    legend: { bottom: 0, textStyle: { color: '#a1b6bb' } },
    grid: { left: 45, right: 20, top: 18, bottom: 42 },
    xAxis: { type: 'time', axisLabel: { color: '#a1b6bb' }, axisLine: { lineStyle: { color: '#29444c' } } },
    yAxis: { type: 'value', axisLabel: { color: '#a1b6bb' }, splitLine: { lineStyle: { color: '#29444c' } } },
    series: [
      { name: 'Download', type: 'line', smooth: true, showSymbol: false, itemStyle: { color: '#83ddc8' }, data: results.filter((r) => r.downloadMbps != null).map((r) => [r.timestamp, r.downloadMbps]) },
      { name: 'Upload', type: 'line', smooth: true, showSymbol: false, itemStyle: { color: '#83badb' }, data: results.filter((r) => r.uploadMbps != null).map((r) => [r.timestamp, r.uploadMbps]) },
    ],
  }), [results])

  useEffect(() => {
    if (!ref.current) return
    const chart = echarts.init(ref.current)
    chart.setOption(option)
    const resize = () => chart.resize()
    window.addEventListener('resize', resize)

    return () => { window.removeEventListener('resize', resize); chart.dispose() }
  }, [option])

  return <div ref={ref} className="chart" aria-label="Download and upload history" />
}
function QualityChart({ results }: { results: SpeedTestResult[] }) {
  const ref = useRef<HTMLDivElement>(null)
  const option = useMemo<EChartsOption>(() => {
    const chronological = results
    return {
      animation: false,
      tooltip: { trigger: 'axis' },
      legend: { bottom: 0, textStyle: { color: '#a1b6bb' } },
      grid: { left: 48, right: 54, top: 18, bottom: 42 },
      xAxis: { type: 'time', axisLabel: { color: '#a1b6bb' }, axisLine: { lineStyle: { color: '#29444c' } } },
      yAxis: [
        { type: 'value', name: 'ms', axisLabel: { color: '#a1b6bb' }, splitLine: { lineStyle: { color: '#29444c' } } },
        { type: 'value', name: '%', position: 'right', axisLabel: { color: '#a1b6bb' }, splitLine: { show: false } },
      ],
      series: [
        { name: 'Latency', type: 'line', smooth: true, showSymbol: false, itemStyle: { color: '#f0bf75' }, data: chronological.filter((r) => r.latencyMs != null).map((r) => [r.timestamp, r.latencyMs]) },
        { name: 'Jitter', type: 'line', smooth: true, showSymbol: false, itemStyle: { color: '#c7bce2' }, data: chronological.filter((r) => r.jitterMs != null).map((r) => [r.timestamp, r.jitterMs]) },
        { name: 'Packet loss', type: 'line', smooth: true, showSymbol: false, yAxisIndex: 1, itemStyle: { color: '#f0a69c' }, data: chronological.filter((r) => r.packetLossPercent != null).map((r) => [r.timestamp, r.packetLossPercent]) },
      ],
    }
  }, [results])

  useEffect(() => {
    if (!ref.current) return
    const chart = echarts.init(ref.current)
    chart.setOption(option)
    const resize = () => chart.resize()
    window.addEventListener('resize', resize)
    return () => { window.removeEventListener('resize', resize); chart.dispose() }
  }, [option])

  return <div ref={ref} className="chart" aria-label="Latency, jitter, and packet loss history" />
}

function Metric({ label, value, tone }: { label: string; value: string; tone: string }) {
  return <div className="metric"><span>{label}</span><strong className={tone}>{value}</strong></div>
}

function BaselineMetric({ label, metric }: { label: string; metric: MetricBaseline }) {
  return <div className="baseline-metric"><span>{label}</span>{metric.available ? <><strong>{formatNumber(metric.latestMbps, 'Mbps')}</strong><small>Baseline {formatNumber(metric.baselineMbps, 'Mbps')} · {metric.percentChange == null ? '—' : `${metric.percentChange >= 0 ? '+' : ''}${metric.percentChange.toFixed(1)}%`}</small></> : <small>Collecting baseline data · {metric.validSamples}/10 samples</small>}</div>
}

function App() {
  const client = useQueryClient()
  const [range, setRange] = useState<Range>('24H')
  const [customFrom, setCustomFrom] = useState(() => new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10))
  const [customTo, setCustomTo] = useState(() => new Date().toISOString().slice(0, 10))
  const latest = useQuery({ queryKey: ['latest'], queryFn: api.getLatest, retry: false })
  const status = useQuery({
    queryKey: ['status'],
    queryFn: api.getStatus,
    refetchInterval: (query) => query.state.data?.state === 'running' ? 2000 : false,
  })
  const schedule = useQuery({ queryKey: ['schedule'], queryFn: api.getSchedule })
  const baseline = useQuery({ queryKey: ['baseline'], queryFn: api.getBaseline })
  const activeAlert = useQuery({ queryKey: ['active-alert'], queryFn: api.getActiveAlert })
  const history = useQuery({
    queryKey: ['history', range, customFrom, customTo],
    queryFn: () => {
      const from = range === 'Custom' ? new Date(`${customFrom}T00:00:00`) : new Date(Date.now() - ranges[range])
      const to = range === 'Custom' ? new Date(`${customTo}T23:59:59.999`) : undefined
      return getHistory(from, to)
    },
  })
  const recent = useQuery({
    queryKey: ['recent-history'],
    queryFn: () => api.getHistoryPage({ page: 1, pageSize: 6, sort: 'desc' }),
  })
  const run = useMutation({
    mutationFn: api.runSpeedTest,
    onSuccess: () => client.invalidateQueries({ queryKey: ['status'] }),
  })
  const wasRunning = useRef(false)

  useEffect(() => {
    if (status.data?.state === 'running') {
      wasRunning.current = true
      return
    }
    if (!wasRunning.current) return
    wasRunning.current = false
    void client.invalidateQueries({ queryKey: ['latest'] })
    void client.invalidateQueries({ queryKey: ['history'] })
    void client.invalidateQueries({ queryKey: ['recent-history'] })
    void client.invalidateQueries({ queryKey: ['baseline'] })
    void client.invalidateQueries({ queryKey: ['active-alert'] })
    void client.invalidateQueries({ queryKey: ['status'] })
  }, [client, status.data?.state])

  const error = latest.error ?? status.error ?? schedule.error ?? baseline.error ?? activeAlert.error ?? history.error ?? recent.error ?? run.error
  const baselineUnavailable: MetricBaseline = { available: false, validSamples: 0, baselineMbps: null, latestMbps: null, percentChange: null }

  return (
    <main id="main-content" className="page-content dashboard-page">
      <header className="page-title">
        <div><p className="kicker">Connection overview</p><h1>Network pulse</h1></div>
        <div className="status-chip"><i className={status.data?.state === 'running' ? 'pulse' : ''} /> Test {status.data?.state ?? 'loading'}</div>
      </header>
      {error && <div className="error" role="alert">{error instanceof Error ? error.message : 'Could not load dashboard data.'}</div>}
      {activeAlert.data && <section className="active-alert" aria-live="polite"><div><p className="kicker">Active degradation</p><strong>{activeAlert.data.reason}</strong><small>{activeAlert.data.notificationDeliveries.length ? activeAlert.data.notificationDeliveries.map((delivery) => `${delivery.provider}: ${delivery.status}`).join(' · ') : 'No provider notifications configured'}</small></div><div><span>{activeAlert.data.status}</span><small>Since {formatTime(activeAlert.data.startedAt)}</small></div></section>}
      <section className="metrics">
        <Metric label="Download" value={formatNumber(latest.data?.downloadMbps, 'Mbps')} tone="blue" />
        <Metric label="Upload" value={formatNumber(latest.data?.uploadMbps, 'Mbps')} tone="green" />
        <Metric label="Latency" value={formatNumber(latest.data?.latencyMs, 'ms')} tone="white" />
        <Metric label="Jitter" value={formatNumber(latest.data?.jitterMs, 'ms')} tone="violet" />
        <Metric label="Packet loss (not reported by LibreSpeed)" value={formatNumber(latest.data?.packetLossPercent, '%')} tone="rose" />
        <div className="action-metric"><span>Last test · {formatTime(latest.data?.timestamp)}</span><button disabled={status.data?.state === 'running' || run.isPending} onClick={() => run.mutate()}>{run.isPending || status.data?.state === 'running' ? 'Running…' : 'Run speed test'}</button></div>
      </section>
      <section className="panel baseline">
        <div className="panel-head"><div><p className="kicker">Seven-day baseline</p><h2>Current vs baseline</h2></div><span className="muted">{baseline.isLoading ? 'Loading…' : 'Successful measurements only'}</span></div>
        <div className="baseline-grid">
          <BaselineMetric label="Download" metric={baseline.data?.download ?? baselineUnavailable} />
          <BaselineMetric label="Upload" metric={baseline.data?.upload ?? baselineUnavailable} />
        </div>
      </section>
      <section className="workspace">
        <div className="panel chart-panel">
          <div className="panel-head">
            <div><p className="kicker">Throughput</p><h2>Speed history</h2></div>
            <div className="range-tabs">{([...Object.keys(ranges), 'Custom'] as Range[]).map((item) => <button className={range === item ? 'selected' : ''} key={item} onClick={() => setRange(item)}>{item}</button>)}</div>
          </div>
          {range === 'Custom' && <div className="chart-dates"><label>From<input type="date" value={customFrom} max={customTo} onChange={(event) => setCustomFrom(event.target.value)} /></label><label>To<input type="date" value={customTo} min={customFrom} onChange={(event) => setCustomTo(event.target.value)} /></label></div>}
          {history.isLoading ? <div className="empty">Loading measurements…</div> : history.data?.length ? <Chart results={history.data} /> : <div className="empty">No measurements in selected range.</div>}
        </div>
        <aside className="panel schedule">
          <p className="kicker">Schedule</p>
          <div className="schedule-state"><i className={schedule.data?.enabled ? 'on' : ''} /> {schedule.data?.enabled ? 'Enabled' : 'Disabled'}</div>
          <dl><dt>Expression</dt><dd>{schedule.data?.cronExpression ?? '—'}</dd><dt>Timezone</dt><dd>{schedule.data?.timezone ?? '—'}</dd><dt>Next run</dt><dd>{formatTime(schedule.data?.nextRuns[0])}</dd></dl>
        </aside>
      </section>
      <section className="panel quality-panel">
        <div className="panel-head"><div><p className="kicker">Connection quality</p><h2>Latency and jitter</h2></div><span className="muted">{history.data?.length ?? 0} measurements in selected range · packet loss unavailable with LibreSpeed</span></div>
        {history.isLoading ? <div className="empty">Loading measurements…</div> : history.data?.length ? <QualityChart results={history.data} /> : <div className="empty">No measurements in selected range.</div>}
      </section>
      <section className="panel recent">
        <div className="panel-head"><div><p className="kicker">Recent measurements</p><h2>Latest readings</h2></div><span className="muted">{latest.data ? `Updated ${formatTime(latest.data.timestamp)}` : '—'}</span></div>
        {recent.isLoading ? <div className="empty">Loading recent measurements…</div> : recent.data?.items.length ? <div className="table-wrap"><table><thead><tr><th>Time</th><th>Download</th><th>Upload</th><th>Latency</th><th>Jitter</th><th>Result</th></tr></thead><tbody>{recent.data.items.map((item) => <tr key={item.id}><td>{formatTime(item.timestamp)}</td><td>{formatNumber(item.downloadMbps, 'Mbps')}</td><td>{formatNumber(item.uploadMbps, 'Mbps')}</td><td>{formatNumber(item.latencyMs, 'ms')}</td><td>{formatNumber(item.jitterMs, 'ms')}</td><td className={item.success ? 'success' : 'failure'}>{item.success ? 'Success' : 'Failed'}</td></tr>)}</tbody></table></div> : <div className="empty">No tests have been recorded yet.</div>}
      </section>
    </main>
  )
}

export default App
