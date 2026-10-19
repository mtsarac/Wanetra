import { useQuery } from '@tanstack/react-query'
import type { EChartsOption } from 'echarts'
import * as echarts from 'echarts/core'
import { LineChart } from 'echarts/charts'
import { GridComponent, LegendComponent, TooltipComponent } from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'
import { useEffect, useMemo, useRef, useState } from 'react'
import { Button } from '../components/ui/button'
import { api, type ProbePoint, type ProbeTarget } from '../lib/api'
import { formatMs } from './format'

echarts.use([LineChart, GridComponent, LegendComponent, TooltipComponent, CanvasRenderer])

const ranges = [
  { id: '1h', label: '1H', ms: 60 * 60 * 1000 },
  { id: '6h', label: '6H', ms: 6 * 60 * 60 * 1000 },
  { id: '24h', label: '24H', ms: 24 * 60 * 60 * 1000 },
  { id: '7d', label: '7D', ms: 7 * 24 * 60 * 60 * 1000 },
  { id: '30d', label: '30D', ms: 30 * 24 * 60 * 60 * 1000 },
] as const

type RangeId = (typeof ranges)[number]['id']

function Chart({ points }: { points: ProbePoint[] }) {
  const ref = useRef<HTMLDivElement>(null)
  const option = useMemo<EChartsOption>(() => {
    const css = getComputedStyle(document.documentElement)
    const color = (token: string) => css.getPropertyValue(token).trim()
    const font = { fontFamily: 'JetBrains Mono Variable, monospace', fontSize: 13 }
    return {
      animation: false,
      tooltip: { trigger: 'axis', backgroundColor: color('--surface'), borderColor: color('--line'), textStyle: { color: color('--text'), ...font } },
      legend: { bottom: 0, textStyle: { color: color('--muted'), ...font } },
      grid: { left: 52, right: 54, top: 20, bottom: 44 },
      xAxis: { type: 'time', axisLabel: { color: color('--muted'), fontSize: 12 }, axisLine: { lineStyle: { color: color('--line') } } },
      yAxis: [
        { type: 'value', name: 'ms', min: 0, axisLabel: { color: color('--muted'), fontSize: 12 }, splitLine: { lineStyle: { color: color('--line') } } },
        { type: 'value', name: '% lost', min: 0, max: 100, axisLabel: { color: color('--muted'), fontSize: 12 }, splitLine: { show: false } },
      ],
      series: [
        { name: 'Latency', type: 'line', showSymbol: false, lineStyle: { width: 2 }, itemStyle: { color: color('--blue') }, data: points.map((p) => [p.timestamp, p.rttMs]) },
        { name: 'Packet loss', type: 'line', yAxisIndex: 1, step: 'end', showSymbol: false, lineStyle: { width: 1.5, type: 'dashed' }, itemStyle: { color: color('--red') }, data: points.map((p) => [p.timestamp, p.lossPercent]) },
      ],
    }
  }, [points])

  useEffect(() => {
    if (!ref.current) return
    const chart = echarts.init(ref.current)
    chart.setOption(option)
    const resize = () => chart.resize()
    window.addEventListener('resize', resize)
    return () => { window.removeEventListener('resize', resize); chart.dispose() }
  }, [option])

  const rtts = points.map((p) => p.rttMs).filter((v): v is number => v != null)
  const worstLoss = points.reduce((max, p) => Math.max(max, p.lossPercent), 0)
  const summary = rtts.length
    ? `Latency between ${formatMs(Math.min(...rtts))} and ${formatMs(Math.max(...rtts))}, worst packet loss ${worstLoss.toFixed(0)}% across ${points.length} points.`
    : `No replies in ${points.length} points.`

  return (
    <figure className="chart-figure" aria-label="Latency history figure">
      <div ref={ref} className="chart" role="img" aria-label={summary} />
      <figcaption className="sr-only">{summary}</figcaption>
    </figure>
  )
}

export function LatencyChart({ targets }: { targets: ProbeTarget[] }) {
  const [targetId, setTargetId] = useState<number | null>(null)
  const [rangeId, setRangeId] = useState<RangeId>('6h')
  const selected = targets.find((target) => target.id === targetId) ?? targets.find((target) => target.enabled) ?? targets[0]
  const range = ranges.find((item) => item.id === rangeId) ?? ranges[1]

  const series = useQuery({
    queryKey: ['probe-series', selected?.id, rangeId],
    queryFn: () => api.getProbeSeries(selected!.id, new Date(Date.now() - range.ms), new Date()),
    enabled: selected != null,
    refetchInterval: 30_000,
    staleTime: 0,
  })

  return (
    <section className="panel chart-panel" aria-labelledby="latency-heading">
      <div className="panel-head">
        <div>
          <p className="kicker">Latency</p>
          <h2 id="latency-heading">Round-trip time and loss</h2>
        </div>
        <div className="range-tabs" role="group" aria-label="Time range">
          {ranges.map((item) => (
            <Button
              key={item.id}
              variant="ghost"
              size="sm"
              className={rangeId === item.id ? 'selected' : ''}
              aria-pressed={rangeId === item.id}
              onClick={() => setRangeId(item.id)}
            >
              {item.label}
            </Button>
          ))}
        </div>
      </div>
      <div className="chart-dates">
        <label>
          <span>Target</span>
          <select value={selected?.id ?? ''} onChange={(event) => setTargetId(Number(event.target.value))}>
            {targets.map((target) => <option key={target.id} value={target.id}>{target.name}</option>)}
          </select>
        </label>
        {series.data && <span className="muted">{series.data.points.length} points · {series.data.resolution} resolution</span>}
      </div>
      {series.isError && <div className="error" role="alert">{series.error.message}</div>}
      {!selected ? (
        <div className="empty">Add a target to see its latency.</div>
      ) : series.isLoading ? (
        <div className="empty">Loading samples…</div>
      ) : series.data?.points.length ? (
        <Chart points={series.data.points} />
      ) : (
        <div className="empty">No samples in this range yet.</div>
      )}
    </section>
  )
}
