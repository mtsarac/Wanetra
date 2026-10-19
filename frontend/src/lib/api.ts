export type SpeedTestResult = {
  id: number
  timestamp: string
  engine: string
  success: boolean
  failureKind: 'networkFailure' | 'measurementFailure' | 'localExecutionFailure' | null
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
export type NotificationDelivery = {
  provider: string
  trigger: 'opened' | 'recovered'
  status: 'pending' | 'delivered' | 'skipped'
  attemptCount: number
  lastAttemptAt: string | null
  deliveredAt: string | null
  lastErrorSummary: string | null
}

export type DegradationEvent = {
  id: number
  startedAt: string
  endedAt: string | null
  status: 'active' | 'recovering' | 'recovered' | 'disabled'
  reason: string
  closureReason: string | null
  baselineDownloadMbps: number | null
  worstDownloadMbps: number | null
  baselineUploadMbps: number | null
  worstUploadMbps: number | null
  maxLatencyMs: number | null
  maxJitterMs: number | null
  maxPacketLossPercent: number | null
  notificationSent: boolean
  recoveryNotificationSent: boolean
  notificationDeliveries: NotificationDelivery[]
}

export type SettingType = 'choice' | 'integer' | 'boolean' | 'text'
export type SettingGroup = 'speedtest' | 'retention' | 'probes'
export type SettingSource = 'default' | 'stored' | 'environment'
export type SettingLockReason = 'environment' | 'restricted'

export type Setting = {
  key: string
  group: SettingGroup
  type: SettingType
  value: string | number | boolean | null
  defaultValue: string | number | boolean | null
  nullable: boolean
  source: SettingSource
  locked: boolean
  lockReason: SettingLockReason | null
  environmentVariable: string
  options: string[] | null
  min: number | null
  max: number | null
  updatedAt: string | null
}

export type SettingsResponse = {
  settings: Setting[]
}

export type SettingUpdates = Record<string, string | number | boolean | null>

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
  notificationDeliveries: NotificationDelivery[]
}

export type NotificationConfiguration = {
  id: number
  provider: string
  enabled: boolean
  hasConfiguration: boolean
  updatedAt: string
  serverUrl: string | null
  topic: string | null
  priority: string | null
  tags: string | null
  method: string | null
  hasUrl: boolean
  hasHeaders: boolean
  hasCredentials: boolean
}

export type NotificationConfigurationList = {
  configurations: NotificationConfiguration[]
}
export type NotificationTestTarget = { id: number } | { provider: string; configurationJson: string }

export type ProbeKind = 'icmp' | 'tcp' | 'dns' | 'http'
export type ProbeRole = 'gateway' | 'internet' | 'dns' | 'custom'

export type ProbeTarget = {
  id: number
  name: string
  kind: ProbeKind
  role: ProbeRole
  target: string | null
  port: number | null
  resolver: string | null
  enabled: boolean
  createdAt: string
}

export type ProbeTargetInput = Omit<ProbeTarget, 'id' | 'createdAt'>

export type LayerState = 'up' | 'down' | 'unknown'
export type OutageScope = 'localNetwork' | 'wan' | 'dns'
export type ConnectivityState = 'healthy' | 'degraded' | 'outage' | 'unknown' | 'disabled'

export type ProbeTargetStatus = {
  target: ProbeTarget
  up: boolean | null
  rttMs: number | null
  checkedAt: string | null
  lossPercent: number | null
  avgRttMs: number | null
  error: string | null
}

export type Outage = {
  id: number
  startedAt: string
  endedAt: string | null
  status: 'active' | 'recovered'
  scope: OutageScope
  reason: string
  closureReason: string | null
  durationSeconds: number
  notificationSent: boolean
  recoveryNotificationSent: boolean
  notificationDeliveries: NotificationDelivery[]
}

export type ConnectivityStatus = {
  monitoring: boolean
  intervalSeconds: number
  state: ConnectivityState
  layers: { gateway: LayerState; internet: LayerState; dns: LayerState }
  scope: OutageScope | null
  reason: string | null
  lastCheckedAt: string | null
  targets: ProbeTargetStatus[]
  activeOutage: Outage | null
}

export type ProbePoint = {
  timestamp: string
  rttMs: number | null
  rttMinMs: number | null
  rttMaxMs: number | null
  jitterMs: number | null
  lossPercent: number
  sent: number
}

export type ProbeSeries = {
  targetId: number
  resolution: 'raw' | 'minute' | 'hour'
  from: string
  to: string
  points: ProbePoint[]
}

export type UptimeRange = '24h' | '7d' | '30d' | '90d'

export type Uptime = {
  range: UptimeRange
  from: string
  to: string
  uptimePercent: number
  outageCount: number
  totalOutageSeconds: number
  longestOutageSeconds: number
  coversFullRange: boolean
}

type ApiError = { code?: string; message?: string }

export const UNAUTHORIZED_EVENT = 'wanetra:unauthorized'
const CSRF_HEADER = 'X-Wanetra-Request'

export class ApiRequestError extends Error {
  readonly status: number
  readonly code: string | null

  constructor(message: string, status: number, code: string | null) {
    super(message)
    this.name = 'ApiRequestError'
    this.status = status
    this.code = code
  }
}

export type AuthStatus = {
  authEnabled: boolean
  envLocked: boolean
  setupRequired: boolean
  accountExists: boolean
  authenticated: boolean
  username: string | null
  metricsTokenConfigured: boolean
}

async function apiFetch(url: string, init?: RequestInit): Promise<Response> {
  const method = init?.method?.toUpperCase() ?? 'GET'
  const headers = new Headers(init?.headers)
  if (method !== 'GET' && method !== 'HEAD') headers.set(CSRF_HEADER, '1')

  const response = await fetch(url, { ...init, headers })
  if (response.status === 401 && !url.startsWith('/api/auth/')) {
    window.dispatchEvent(new Event(UNAUTHORIZED_EVENT))
  }
  return response
}

async function failure(response: Response): Promise<ApiRequestError> {
  const body = await response.json().catch(() => null) as ApiError | null
  return new ApiRequestError(body?.message ?? `Request failed (${response.status})`, response.status, body?.code ?? null)
}

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await apiFetch(url, init)

  if (!response.ok) throw await failure(response)

  if (response.status === 204) return undefined as T
  const body = await response.text()
  return body.trim() === '' ? undefined as T : JSON.parse(body) as T
}

function sendJson<T>(method: 'POST' | 'PUT', url: string, body?: unknown): Promise<T> {
  return request<T>(url, {
    method,
    headers: { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
}

const postJson = <T>(url: string, body?: unknown) => sendJson<T>('POST', url, body)

async function getLatest(): Promise<SpeedTestResult | null> {
  const response = await apiFetch('/api/speedtests/latest')
  if (response.status === 404) return null
  if (!response.ok) throw await failure(response)
  return response.json() as Promise<SpeedTestResult>
}

export const api = {
  getConnectivity: () => request<ConnectivityStatus>('/api/probes/status'),
  getProbeTargets: () => request<ProbeTarget[]>('/api/probes/targets'),
  createProbeTarget: (input: ProbeTargetInput) => postJson<ProbeTarget>('/api/probes/targets', input),
  updateProbeTarget: (id: number, input: ProbeTargetInput) =>
    sendJson<ProbeTarget>('PUT', `/api/probes/targets/${id}`, input),
  deleteProbeTarget: (id: number) => request<void>(`/api/probes/targets/${id}`, { method: 'DELETE' }),
  getProbeSeries: (targetId: number, from: Date, to: Date) => {
    const params = new URLSearchParams({
      targetId: String(targetId),
      from: from.toISOString(),
      to: to.toISOString(),
    })
    return request<ProbeSeries>(`/api/probes/series?${params}`)
  },
  getOutages: (count = 20) => request<Outage[]>(`/api/outages?count=${count}`),
  getUptime: (range: UptimeRange) => request<Uptime>(`/api/uptime?range=${range}`),
  getAuthStatus: () => request<AuthStatus>('/api/auth/status'),
  setupAuth: (setupCode: string, username: string, password: string) =>
    postJson<AuthStatus>('/api/auth/setup', { setupCode, username, password }),
  login: (username: string, password: string) =>
    postJson<AuthStatus>('/api/auth/login', { username, password }),
  logout: () => postJson<void>('/api/auth/logout'),
  changePassword: (currentPassword: string, newPassword: string) =>
    postJson<void>('/api/auth/change-password', { currentPassword, newPassword }),
  disableAuth: (password: string) => postJson<void>('/api/auth/disable', { password }),
  enableAuth: () => postJson<void>('/api/auth/enable'),
  createMetricsToken: () => postJson<{ token: string }>('/api/auth/metrics-token'),
  revokeMetricsToken: () => request<void>('/api/auth/metrics-token', { method: 'DELETE' }),
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
  getSettings: () => request<SettingsResponse>('/api/settings'),
  updateSettings: (values: SettingUpdates) =>
    request<SettingsResponse>('/api/settings', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ values }),
    }),
  getAlertRule: async () => {
    const response = await apiFetch('/api/alerts/rule')
    if (response.status === 404) return null
    if (!response.ok) throw await failure(response)
    return response.json() as Promise<AlertRule>
  },
  saveAlertRule: (rule: AlertRuleUpdate) =>
    request<AlertRule>('/api/alerts/rule', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(rule),
    }),
  getActiveAlert: async () => {
    const response = await apiFetch('/api/alerts/active')
    if (response.status === 404) return null
    if (!response.ok) throw await failure(response)
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
  const results = [...firstPage.items]

  for (let page = 2; page <= firstPage.totalPages; page++) {
    const nextPage = await api.getHistoryPage({ ...query, page })
    results.push(...nextPage.items)
  }

  return results
}
