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

type SpeedTestHistoryPage = {
  items: SpeedTestResult[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export type Schedule = {
  enabled: boolean
  cronExpression: string
  timezone: string
  updatedAt: string
  nextRuns: string[]
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
  getBaseline: () => request<Baseline>('/api/baseline'),
  getActiveAlert: async () => {
    const response = await fetch('/api/alerts/active')
    if (response.status === 404) return null
    if (!response.ok) throw new Error(`Request failed (${response.status})`)
    return response.json() as Promise<ActiveAlert>
  },
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
  getHistoryPage: (from: Date, page: number) => request<SpeedTestHistoryPage>(
    `/api/speedtests?${new URLSearchParams({
      from: from.toISOString(),
      sort: 'asc',
      page: String(page),
      pageSize: '200',
    })}`,
  ),
}

export async function getHistory(from: Date): Promise<SpeedTestResult[]> {
  const firstPage = await api.getHistoryPage(from, 1)
  const pages = await Promise.all(
    Array.from({ length: firstPage.totalPages - 1 }, (_, index) => api.getHistoryPage(from, index + 2)),
  )

  return [firstPage, ...pages].flatMap((page) => page.items)
}
