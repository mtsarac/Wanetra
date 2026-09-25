import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { describe, expect, it, vi } from 'vitest'
import AlertSettings from './AlertSettings'
import App from './App'
import HistoryPage from './HistoryPage'
import NotificationsPanel from './Notifications'
import ScheduleSettings from './ScheduleSettings'
import { api, type AlertRule, type Schedule, type SpeedTestHistoryPage } from './lib/api'

vi.mock('echarts/core', () => ({
  use: vi.fn(),
  init: vi.fn(() => ({ setOption: vi.fn(), resize: vi.fn(), dispose: vi.fn() })),
}))
vi.mock('echarts/charts', () => ({ LineChart: {} }))
vi.mock('echarts/components', () => ({ GridComponent: {}, LegendComponent: {}, TooltipComponent: {} }))
vi.mock('echarts/renderers', () => ({ CanvasRenderer: {} }))

function renderWithClient(component: ReactNode) {
  const client = new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: 0 },
      mutations: { retry: false },
    },
  })
  return render(<QueryClientProvider client={client}>{component}</QueryClientProvider>)
}

const emptyPage: SpeedTestHistoryPage = {
  items: [], page: 1, pageSize: 200, totalCount: 0, totalPages: 0,
}

function setupDashboard() {
  vi.spyOn(api, 'getLatest').mockResolvedValue(null)
  vi.spyOn(api, 'getStatus').mockResolvedValue({ state: 'idle', trigger: null, startedAt: null, errorMessage: null })
  vi.spyOn(api, 'getSchedule').mockResolvedValue({
    enabled: false, cronExpression: '*/30 * * * *', timezone: 'Europe/Istanbul', updatedAt: '', nextRuns: [],
  })
  vi.spyOn(api, 'getBaseline').mockResolvedValue({
    windowFrom: '', windowTo: '',
    download: { available: false, validSamples: 0, baselineMbps: null, latestMbps: null, percentChange: null },
    upload: { available: false, validSamples: 0, baselineMbps: null, latestMbps: null, percentChange: null },
  })
  vi.spyOn(api, 'getActiveAlert').mockResolvedValue(null)
  vi.spyOn(api, 'getHistoryPage').mockResolvedValue(emptyPage)
}

const emptyRule: AlertRule = {
  id: 1, name: 'WAN health', enabled: true, minDownloadMbps: null, minUploadMbps: null,
  maxLatencyMs: null, maxJitterMs: null, maxPacketLossPercent: null,
  downloadBaselineDropPercent: 30, uploadBaselineDropPercent: null,
  consecutiveFailuresRequired: 3, consecutiveRecoveriesRequired: 2, updatedAt: '',
}

describe('alert settings', () => {
  it('marks packet-loss alerts unavailable and blocks invalid consecutive counts', async () => {
    vi.spyOn(api, 'getAlertRule').mockResolvedValue(emptyRule)
    vi.spyOn(api, 'getDegradationEvents').mockResolvedValue([])
    const save = vi.spyOn(api, 'saveAlertRule').mockResolvedValue(emptyRule)
    const user = userEvent.setup()
    renderWithClient(<AlertSettings />)

    const packetLoss = await screen.findByLabelText(/Maximum packet loss/)
    expect((packetLoss as HTMLInputElement).disabled).toBe(true)
    expect(screen.getByText('Unavailable with LibreSpeed; this threshold is not applied.')).toBeTruthy()

    const failureCount = screen.getByLabelText('Consecutive unhealthy tests') as HTMLInputElement
    await user.clear(failureCount)
    await user.type(failureCount, '0')
    expect(failureCount.checkValidity()).toBe(false)
    await user.click(screen.getByRole('button', { name: 'Save alert rule' }))
    expect(save).not.toHaveBeenCalled()
  })
})

describe('schedule settings', () => {
  it('saves the enabled schedule with the edited cron expression and timezone', async () => {
    const schedule: Schedule = {
      enabled: false, cronExpression: '*/30 * * * *', timezone: 'Europe/Istanbul', updatedAt: '', nextRuns: [],
    }
    vi.spyOn(api, 'getSchedule').mockResolvedValue(schedule)
    const save = vi.spyOn(api, 'updateSchedule').mockImplementation(async (update) => ({ ...schedule, ...update }))
    const user = userEvent.setup()
    renderWithClient(<ScheduleSettings />)

    await user.click(await screen.findByLabelText('Enable scheduled tests'))
    const cron = screen.getByLabelText('Cron expression')
    await user.clear(cron)
    await user.type(cron, '0 * * * *')
    const timezone = screen.getByLabelText('Timezone')
    await user.clear(timezone)
    await user.type(timezone, 'Asia/Tokyo')
    await user.click(screen.getByRole('button', { name: 'Save schedule' }))

    await waitFor(() => expect(save.mock.calls[0]?.[0]).toEqual({ enabled: true, cronExpression: '0 * * * *', timezone: 'Asia/Tokyo' }))
    expect(await screen.findByText('Schedule saved.')).toBeTruthy()
  })

  it('renders schedule API errors', async () => {
    vi.spyOn(api, 'getSchedule').mockRejectedValue(new Error('Schedule API unavailable'))
    renderWithClient(<ScheduleSettings />)
    expect((await screen.findByRole('alert')).textContent).toContain('Schedule API unavailable')
  })
})

describe('notification settings', () => {
  it('keeps saved credentials blank in the UI and submits blank fields for server-side preservation', async () => {
    vi.spyOn(api, 'getNotifications').mockResolvedValue({ configurations: [
      {
        id: 1, provider: 'ntfy', enabled: true, hasConfiguration: true, updatedAt: '',
        serverUrl: 'https://ntfy.sh', topic: 'wanetra', priority: 'default', tags: null,
        method: null, hasUrl: false, hasHeaders: false, hasCredentials: true,
      },
      {
        id: 2, provider: 'webhook', enabled: false, hasConfiguration: false, updatedAt: '',
        serverUrl: null, topic: null, priority: null, tags: null, method: null,
        hasUrl: false, hasHeaders: false, hasCredentials: false,
      },
    ] })
    const save = vi.spyOn(api, 'saveNotifications').mockResolvedValue({ configurations: [] })
    const user = userEvent.setup()
    renderWithClient(<NotificationsPanel />)

    expect((await screen.findByLabelText('Password') as HTMLInputElement).value).toBe('')
    expect((await screen.findAllByPlaceholderText('(stored)')).every((input) => input instanceof HTMLInputElement && input.value === '')).toBe(true)
    await user.click(screen.getByRole('button', { name: 'Save notifications' }))

    await waitFor(() => expect(save).toHaveBeenCalledTimes(1))
    const payload = save.mock.calls[0][0]
    const ntfy = payload.find((configuration) => configuration.provider === 'ntfy')
    expect(ntfy).toBeDefined()
    expect(JSON.parse(ntfy!.configurationJson)).toMatchObject({ serverUrl: '', topic: '' })
    expect(JSON.parse(ntfy!.configurationJson)).not.toHaveProperty('password')
    expect((await screen.findByRole('status')).textContent).toContain('Secrets stay stored')
  })

  it('reports a successful saved-provider test when the API returns an empty body', async () => {
    vi.spyOn(api, 'getNotifications').mockResolvedValue({ configurations: [
      {
        id: 4, provider: 'webhook', enabled: true, hasConfiguration: true, updatedAt: '',
        serverUrl: null, topic: null, priority: null, tags: null, method: 'PUT',
        hasUrl: true, hasHeaders: true, hasCredentials: false,
      },
    ] })
    const test = vi.spyOn(api, 'testNotification').mockResolvedValue(undefined)
    const user = userEvent.setup()
    renderWithClient(<NotificationsPanel />)

    await user.click((await screen.findAllByRole('button', { name: 'Send test' }))[1])

    await waitFor(() => expect(test).toHaveBeenCalledWith({ id: 4 }))
    expect((await screen.findByRole('status')).textContent).toContain('Test notification sent.')
  })
})

describe('history filters', () => {
  it('maps outcome and sort filters to the paged history request', async () => {
    const getPage = vi.spyOn(api, 'getHistoryPage').mockResolvedValue(emptyPage)
    const user = userEvent.setup()
    renderWithClient(<HistoryPage />)
    await screen.findByText('No measurements match these filters.')

    await user.selectOptions(screen.getByLabelText('Outcome'), 'failed')
    await waitFor(() => expect(getPage).toHaveBeenLastCalledWith(expect.objectContaining({ success: false, page: 1, pageSize: 25 })))
    await user.selectOptions(screen.getByLabelText('Sort'), 'asc')
    await waitFor(() => expect(getPage).toHaveBeenLastCalledWith(expect.objectContaining({ success: false, sort: 'asc', page: 1 })))
  })
})

describe('dashboard', () => {
  it('shows running state after a manual speed test starts', async () => {
    setupDashboard()
    vi.spyOn(api, 'getStatus')
      .mockResolvedValueOnce({ state: 'idle', trigger: null, startedAt: null, errorMessage: null })
      .mockResolvedValue({ state: 'running', trigger: 'manual', startedAt: '2026-09-21T12:00:00Z', errorMessage: null })
    vi.spyOn(api, 'runSpeedTest').mockResolvedValue({ status: 'running' })
    const user = userEvent.setup()
    renderWithClient(<App />)

    await user.click(await screen.findByRole('button', { name: 'Run speed test' }))
    expect(await screen.findByRole('button', { name: 'Running…' })).toBeTruthy()
    expect((screen.getByRole('button', { name: 'Running…' }) as HTMLButtonElement).disabled).toBe(true)
  })

  it('renders dashboard API errors', async () => {
    setupDashboard()
    vi.spyOn(api, 'getLatest').mockRejectedValue(new Error('Latest result unavailable'))
    renderWithClient(<App />)
    expect((await screen.findByRole('alert')).textContent).toContain('Latest result unavailable')
  })
})
