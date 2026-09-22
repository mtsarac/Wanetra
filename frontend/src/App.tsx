import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { EChartsOption } from 'echarts'
import * as echarts from 'echarts/core'
import { LineChart } from 'echarts/charts'
import { GridComponent, LegendComponent, TooltipComponent } from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'
import { useEffect, useMemo, useRef, useState } from 'react'
import { api, getHistory, type MetricBaseline, type SpeedTestResult } from './lib/api'
import NotificationsPanel from './Notifications'

echarts.use([LineChart, GridComponent, LegendComponent, TooltipComponent, CanvasRenderer])

type Range = '24h' | '7d' | '30d'

const ranges: Record<Range, number> = { '24h': 1, '7d': 7, '30d': 30 }

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
    legend: { bottom: 0, textStyle: { color: '#8fa3b8' } },
    grid: { left: 45, right: 20, top: 18, bottom: 42 },
    xAxis: { type: 'time', axisLabel: { color: '#8fa3b8' }, axisLine: { lineStyle: { color: '#24364a' } } },
    yAxis: { type: 'value', axisLabel: { color: '#8fa3b8' }, splitLine: { lineStyle: { color: '#1b2b3b' } } },
    series: [
      { name: 'Download', type: 'line', smooth: true, showSymbol: false, itemStyle: { color: '#88d7ff' }, data: results.filter((r) => r.downloadMbps != null).map((r) => [r.timestamp, r.downloadMbps]) },
      { name: 'Upload', type: 'line', smooth: true, showSymbol: false, itemStyle: { color: '#a3e635' }, data: results.filter((r) => r.uploadMbps != null).map((r) => [r.timestamp, r.uploadMbps]) },
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

function Metric({ label, value, tone }: { label: string; value: string; tone: string }) {
  return <div className="metric"><span>{label}</span><strong className={tone}>{value}</strong></div>
}

function BaselineMetric({ label, metric }: { label: string; metric: MetricBaseline }) {
  return <div className="baseline-metric"><span>{label}</span>{metric.available ? <><strong>{formatNumber(metric.latestMbps, 'Mbps')}</strong><small>Baseline {formatNumber(metric.baselineMbps, 'Mbps')} · {metric.percentChange == null ? '—' : `${metric.percentChange >= 0 ? '+' : ''}${metric.percentChange.toFixed(1)}%`}</small></> : <small>Collecting baseline data · {metric.validSamples}/10 samples</small>}</div>
}

function App() {
  const client = useQueryClient()
  const [range, setRange] = useState<Range>('24h')
  const latest = useQuery({ queryKey: ['latest'], queryFn: api.getLatest, retry: false })
  const status = useQuery({ queryKey: ['status'], queryFn: api.getStatus, refetchInterval: (query) => query.state.data?.state === 'running' ? 2000 : false })
  const schedule = useQuery({ queryKey: ['schedule'], queryFn: api.getSchedule })
  const baseline = useQuery({ queryKey: ['baseline'], queryFn: api.getBaseline })
  const activeAlert = useQuery({ queryKey: ['active-alert'], queryFn: api.getActiveAlert })
  const history = useQuery({ queryKey: ['history', range], queryFn: () => getHistory(new Date(Date.now() - ranges[range] * 86400000)) })
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
    void client.invalidateQueries({ queryKey: ['baseline'] })
    void client.invalidateQueries({ queryKey: ['status'] })
  }, [client, status.data?.state])

  const error = latest.error ?? status.error ?? schedule.error ?? baseline.error ?? activeAlert.error ?? history.error
  const recent = [...(history.data ?? [])].sort((a, b) => b.timestamp.localeCompare(a.timestamp)).slice(0, 6)

  return (
    <main className="shell">
      <header className="topbar">
        <div><p className="kicker">WANETRA / OPERATIONS</p><h1>Network pulse</h1></div>
        <div className="status-chip"><i className={status.data?.state === 'running' ? 'pulse' : ''} /> {status.data?.state ?? 'loading'}</div>
      </header>
      {error && <div className="error" role="alert">{error instanceof Error ? error.message : 'Could not load dashboard data.'}</div>}
      {activeAlert.data && <section className="active-alert" aria-live="polite"><div><p className="kicker">ACTIVE DEGRADATION</p><strong>{activeAlert.data.reason}</strong></div><div><span>{activeAlert.data.status}</span><small>Since {formatTime(activeAlert.data.startedAt)}</small></div></section>}
      <section className="metrics">
        <Metric label="Download" value={formatNumber(latest.data?.downloadMbps, 'Mbps')} tone="blue" />
        <Metric label="Upload" value={formatNumber(latest.data?.uploadMbps, 'Mbps')} tone="green" />
        <Metric label="Latency" value={formatNumber(latest.data?.latencyMs, 'ms')} tone="white" />
        <div className="action-metric"><span>Manual test</span><button disabled={status.data?.state === 'running' || run.isPending} onClick={() => run.mutate()}>{run.isPending || status.data?.state === 'running' ? 'Running…' : 'Run speed test'}</button></div>
      </section>
      <section className="panel baseline"><div className="panel-head"><div><p className="kicker">BASELINE / 7 DAYS</p><h2>Current vs baseline</h2></div><span className="muted">{baseline.isLoading ? 'Loading…' : 'Successful measurements only'}</span></div><div className="baseline-grid"><BaselineMetric label="Download" metric={baseline.data?.download ?? { available: false, validSamples: 0, baselineMbps: null, latestMbps: null, percentChange: null }} /><BaselineMetric label="Upload" metric={baseline.data?.upload ?? { available: false, validSamples: 0, baselineMbps: null, latestMbps: null, percentChange: null }} /></div></section>
      <section className="workspace">
        <div className="panel chart-panel"><div className="panel-head"><div><p className="kicker">THROUGHPUT</p><h2>Speed history</h2></div><div className="range-tabs">{(Object.keys(ranges) as Range[]).map((item) => <button className={range === item ? 'selected' : ''} key={item} onClick={() => setRange(item)}>{item}</button>)}</div></div>{history.isLoading ? <div className="empty">Loading measurements…</div> : history.data?.length ? <Chart results={history.data} /> : <div className="empty">No measurements in selected range.</div>}</div>
        <aside className="panel schedule"><p className="kicker">SCHEDULE</p><div className="schedule-state"><i className={schedule.data?.enabled ? 'on' : ''} /> {schedule.data?.enabled ? 'Enabled' : 'Disabled'}</div><dl><dt>Expression</dt><dd>{schedule.data?.cronExpression ?? '—'}</dd><dt>Timezone</dt><dd>{schedule.data?.timezone ?? '—'}</dd><dt>Next run</dt><dd>{formatTime(schedule.data?.nextRuns[0])}</dd></dl></aside>
      </section>
      <NotificationsPanel />
      <section className="panel recent"><div className="panel-head"><div><p className="kicker">RECENT MEASUREMENTS</p><h2>Latest readings</h2></div><span className="muted">{latest.data ? `Updated ${formatTime(latest.data.timestamp)}` : '—'}</span></div>{recent.length ? <div className="table-wrap"><table><thead><tr><th>Time</th><th>Download</th><th>Upload</th><th>Latency</th><th>Result</th></tr></thead><tbody>{recent.map((item) => <tr key={item.id}><td>{formatTime(item.timestamp)}</td><td>{formatNumber(item.downloadMbps, 'Mbps')}</td><td>{formatNumber(item.uploadMbps, 'Mbps')}</td><td>{formatNumber(item.latencyMs, 'ms')}</td><td className={item.success ? 'success' : 'failure'}>{item.success ? 'Success' : 'Failed'}</td></tr>)}</tbody></table></div> : <div className="empty">No measurements recorded yet. Run first test.</div>}</section>
    </main>
  )
}

export default App
