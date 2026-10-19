import type { ConnectivityState, LayerState, OutageScope } from '../lib/api'

export function formatDuration(totalSeconds: number): string {
  const seconds = Math.max(0, Math.round(totalSeconds))
  if (seconds < 60) return `${seconds}s`
  const minutes = Math.floor(seconds / 60)
  if (minutes < 60) return `${minutes}m ${seconds % 60}s`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours}h ${minutes % 60}m`
  return `${Math.floor(hours / 24)}d ${hours % 24}h`
}

export function formatUptime(percent: number): string {
  if (percent >= 100) return '100%'
  // Never round a bad day up to "100.00%": show two decimals and keep any outage visible.
  const rounded = Math.min(percent, 99.99)
  return `${rounded.toFixed(2)}%`
}

export function formatMs(value: number | null | undefined): string {
  if (value == null) return '—'
  return value < 10 ? `${value.toFixed(1)} ms` : `${Math.round(value)} ms`
}

export function formatClock(value: string | null | undefined): string {
  return value ? new Date(value).toLocaleString([], { dateStyle: 'short', timeStyle: 'medium' }) : '—'
}

export const scopeLabels: Record<OutageScope, string> = {
  localNetwork: 'Local network',
  wan: 'Internet (ISP)',
  dns: 'DNS',
}

export const layerLabels: Record<LayerState, string> = {
  up: 'Up',
  down: 'Down',
  unknown: 'No data',
}

export const stateLabels: Record<ConnectivityState, string> = {
  healthy: 'Online',
  degraded: 'Checking a problem',
  outage: 'Outage',
  unknown: 'Waiting for data',
  disabled: 'Monitoring off',
}
