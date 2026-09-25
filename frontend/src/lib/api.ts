export type SpeedTestResult = {
  id: number
  timestamp: string
  engine: string
  success: boolean
  downloadMbps: number | null
  uploadMbps: number | null
  latencyMs: number | null
  jitterMs: number | null
  packetLossPercent: number | null
  serverName: string | null
  serverLocation: string | null
  serverId: string | null
  isp: string | null
  externalIp: string | null
  durationMs: number | null
  errorMessage: string | null
}

export type SpeedTestStatus = {
  state: 'idle' | 'running' | 'failed'
  trigger: 'manual' | 'scheduled' | null
  startedAt: string | null
  errorMessage: string | null
}

export type SpeedTestHistoryPage = {
  items: SpeedTestResult[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export type HistoryQuery = {
  from?: Date
  to?: Date
  success?: boolean
  page?: number
  pageSize?: number
  sort?: 'asc' | 'desc'
}

export type Schedule = {
  enabled: boolean
  cronExpression: string
  timezone: string
  updatedAt: string
  nextRuns: string[]
}

export type AlertRule = {
  id: number
  name: string
  enabled: boolean
  minDownloadMbps: number | null
  minUploadMbps: number | null
  maxLatencyMs: number | null
  maxJitterMs: number | null
  maxPacketLossPercent: number | null
  downloadBaselineDropPercent: number | null
  uploadBaselineDropPercent: number | null
  consecutiveFailuresRequired: number
  consecutiveRecoveriesRequired: number
  updatedAt: string
}

export type AlertRuleUpdate = Omit<AlertRule, 'id' | 'updatedAt'>

export type DegradationEvent = {
  id: number
  startedAt: string
  endedAt: string | null
  status: 'active' | 'recovering' | 'recovered'
  reason: string
  baselineDownloadMbps: number | null
  worstDownloadMbps: number | null
  baselineUploadMbps: number | null
  worstUploadMbps: number | null
  maxLatencyMs: number | null
  maxJitterMs: number | null
  maxPacketLossPercent: number | null
  notificationSent: boolean
  recoveryNotificationSent: boolean
}

export type RetentionSettings = {
  days: number
}

export type MetricBaseline = {
  available: boolean
  validSamples: number
  baselineMbps: number | null
  latestMbps: number | null
  percentChange: number | null
}

export type Baseline = {
  windowFrom: string
  windowTo: string
  download: MetricBaseline
  upload: MetricBaseline
}

export type ActiveAlert = {
  id: number
  startedAt: string
  status: 'active' | 'recovering'
  reason: string
  baselineDownloadMbps: number | null
  worstDownloadMbps: number | null
  baselineUploadMbps: number | null
  worstUploadMbps: number | null
}

export type NotificationConfiguration = {
  id: number
  provider: string
  enabled: boolean
  hasConfiguration: boolean
  updatedAt: string
}

export type NotificationConfigurationList = {
  configurations: NotificationConfiguration[]
}
export type NotificationTestTarget = { id: number } | { provider: string; configurationJson: string }

type ApiError = { message?: string }

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, init)

  if (!response.ok) {
    const body = await response.json().catch(() => null) as ApiError | null
    throw new Error(body?.message ?? `Request failed (${response.status})`)
  }

  return response.json() as Promise<T>
}

async function getLatest(): Promise<SpeedTestResult | null> {
  const response = await fetch('/api/speedtests/latest')
  if (response.status === 404) return null
  if (!response.ok) {
    const body = await response.json().catch(() => null) as ApiError | null
    throw new Error(body?.message ?? `Request failed (${response.status})`)
  }
  return response.json() as Promise<SpeedTestResult>
}

export const api = {
  getLatest,
  getStatus: () => request<SpeedTestStatus>('/api/speedtests/status'),
  getSchedule: () => request<Schedule>('/api/schedule'),
  updateSchedule: (schedule: Pick<Schedule, 'enabled' | 'cronExpression' | 'timezone'>) =>
    request<Schedule>('/api/schedule', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(schedule),
    }),
  getBaseline: () => request<Baseline>('/api/baseline'),
  getRetentionSettings: () => request<RetentionSettings>('/api/settings/retention'),
  getAlertRule: async () => {
    const response = await fetch('/api/alerts/rule')
    if (response.status === 404) return null
    if (!response.ok) throw new Error(`Request failed (${response.status})`)
    return response.json() as Promise<AlertRule>
  },
  saveAlertRule: (rule: AlertRuleUpdate) =>
    request<AlertRule>('/api/alerts/rule', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(rule),
    }),
  getActiveAlert: async () => {
    const response = await fetch('/api/alerts/active')
    if (response.status === 404) return null
    if (!response.ok) throw new Error(`Request failed (${response.status})`)
    return response.json() as Promise<ActiveAlert>
  },
  getDegradationEvents: (count = 20) => request<DegradationEvent[]>(`/api/alerts/events?count=${count}`),
  runSpeedTest: () => request<{ status: string }>('/api/speedtests/run', { method: 'POST' }),
  getNotifications: () => request<NotificationConfigurationList>('/api/notifications'),
  saveNotifications: (configurations: { provider: string; enabled: boolean; configurationJson: string }[]) =>
    request<NotificationConfigurationList>('/api/notifications', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ configurations }),
    }),
  testNotification: (target: NotificationTestTarget) =>
    request<void>('/api/notifications/test', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(target),
    }),
  getHistoryPage: (query: HistoryQuery = {}) => {
    const params = new URLSearchParams({
      page: String(query.page ?? 1),
      pageSize: String(query.pageSize ?? 50),
      sort: query.sort ?? 'desc',
    })
    if (query.from) params.set('from', query.from.toISOString())
    if (query.to) params.set('to', query.to.toISOString())
    if (query.success != null) params.set('success', String(query.success))
    return request<SpeedTestHistoryPage>(`/api/speedtests?${params}`)
  },
}

export async function getHistory(from: Date, to?: Date): Promise<SpeedTestResult[]> {
  const query: HistoryQuery = { from, to, sort: 'asc', page: 1, pageSize: 200 }
  const firstPage = await api.getHistoryPage(query)
  const pages = await Promise.all(
    Array.from(
      { length: Math.max(firstPage.totalPages - 1, 0) },
      (_, index) => api.getHistoryPage({ ...query, page: index + 2 }),
    ),
  )

  return [firstPage, ...pages].flatMap((page) => page.items)
}
