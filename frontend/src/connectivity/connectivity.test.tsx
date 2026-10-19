import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { MemoryRouter } from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import { api, type ConnectivityStatus, type Outage, type ProbeTarget, type ProbeTargetStatus } from '../lib/api'
import { ConnectionChain } from './ConnectionChain'
import { ConnectionSummary } from './ConnectionSummary'
import ConnectivityPage from './ConnectivityPage'
import { formatDuration, formatUptime } from './format'
import { OutagesPanel } from './OutagesPanel'
import { rolesFor, TargetsPanel } from './TargetsPanel'

vi.mock('echarts/core', () => ({
  use: vi.fn(),
  init: vi.fn(() => ({ setOption: vi.fn(), resize: vi.fn(), dispose: vi.fn() })),
}))
vi.mock('echarts/charts', () => ({ LineChart: {} }))
vi.mock('echarts/components', () => ({ GridComponent: {}, LegendComponent: {}, TooltipComponent: {} }))
vi.mock('echarts/renderers', () => ({ CanvasRenderer: {} }))

function renderWithClient(ui: ReactNode) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>{ui}</MemoryRouter>
    </QueryClientProvider>,
  )
}

const gateway: ProbeTarget = {
  id: 1, name: 'Gateway', kind: 'icmp', role: 'gateway', target: null, port: null, resolver: null,
  enabled: true, createdAt: '2026-10-18T10:00:00Z',
}
const cloudflare: ProbeTarget = {
  id: 2, name: 'Cloudflare', kind: 'icmp', role: 'internet', target: '1.1.1.1', port: null, resolver: null,
  enabled: true, createdAt: '2026-10-18T10:00:00Z',
}

function statusOf(target: ProbeTarget, over: Partial<ProbeTargetStatus> = {}): ProbeTargetStatus {
  return { target, up: true, rttMs: 12, checkedAt: '2026-10-18T10:00:00Z', lossPercent: 0, avgRttMs: 12, error: null, ...over }
}

const healthy: ConnectivityStatus = {
  monitoring: true,
  intervalSeconds: 10,
  state: 'healthy',
  layers: { gateway: 'up', internet: 'up', dns: 'up' },
  scope: null,
  reason: null,
  lastCheckedAt: '2026-10-18T10:00:00Z',
  targets: [statusOf(gateway), statusOf(cloudflare)],
  activeOutage: null,
}

const outage: Outage = {
  id: 7,
  startedAt: '2026-10-18T09:00:00Z',
  endedAt: null,
  status: 'active',
  scope: 'wan',
  reason: 'Every internet target is unreachable while the gateway responds',
  closureReason: null,
  durationSeconds: 125,
  notificationSent: false,
  recoveryNotificationSent: false,
  notificationDeliveries: [],
}

describe('formatting', () => {
  it('shortens durations to the two most useful units', () => {
    expect(formatDuration(0)).toBe('0s')
    expect(formatDuration(59.4)).toBe('59s')
    expect(formatDuration(125)).toBe('2m 5s')
    expect(formatDuration(3 * 3600 + 5 * 60 + 9)).toBe('3h 5m')
    expect(formatDuration(2 * 86400 + 5 * 3600)).toBe('2d 5h')
  })

  it('never rounds an imperfect period up to a perfect one', () => {
    expect(formatUptime(100)).toBe('100%')
    expect(formatUptime(99.9996)).toBe('99.99%')
    expect(formatUptime(99.31)).toBe('99.31%')
    expect(formatUptime(0)).toBe('0.00%')
  })

  it('offers only roles that make sense for each check', () => {
    expect(rolesFor('dns')).toEqual(['dns', 'custom'])
    expect(rolesFor('http')).toEqual(['internet', 'custom'])
    expect(rolesFor('icmp')).toContain('gateway')
    expect(rolesFor('tcp')).toContain('gateway')
  })
})

describe('connection chain', () => {
  it('states each layer in words, not just colour', () => {
    renderWithClient(
      <ConnectionChain status={{ ...healthy, layers: { gateway: 'up', internet: 'down', dns: 'unknown' } }} />,
    )

    const items = screen.getAllByRole('listitem')
    expect(items.map((item) => item.textContent)).toEqual([
      expect.stringContaining('Wanetra'),
      expect.stringContaining('Gateway'),
      expect.stringContaining('Internet'),
      expect.stringContaining('DNS'),
    ])
    expect(within(items[1]).getByText('Up')).toBeTruthy()
    expect(within(items[2]).getByText('Down')).toBeTruthy()
    expect(within(items[3]).getByText('No data')).toBeTruthy()
  })
})

describe('connection summary', () => {
  it('shows the live state with a link to the details', async () => {
    vi.spyOn(api, 'getConnectivity').mockResolvedValue(healthy)
    vi.spyOn(api, 'getUptime').mockResolvedValue({
      range: '24h', from: '', to: '', uptimePercent: 99.5, outageCount: 2, totalOutageSeconds: 432,
      longestOutageSeconds: 300, coversFullRange: true,
    })
    renderWithClient(<ConnectionSummary />)

    expect((await screen.findByRole('status')).textContent).toContain('Online')
    expect(screen.getByRole('link', { name: 'Details' }).getAttribute('href')).toBe('/connectivity')
    expect(await screen.findByText('99.50%')).toBeTruthy()
    expect(screen.getByText(/2 outages · 7m 12s down/)).toBeTruthy()
  })

  it('names where an open outage is', async () => {
    vi.spyOn(api, 'getConnectivity').mockResolvedValue({
      ...healthy,
      state: 'outage',
      layers: { gateway: 'up', internet: 'down', dns: 'up' },
      activeOutage: outage,
    })
    vi.spyOn(api, 'getUptime').mockResolvedValue({
      range: '24h', from: '', to: '', uptimePercent: 99, outageCount: 1, totalOutageSeconds: 100,
      longestOutageSeconds: 100, coversFullRange: false,
    })
    renderWithClient(<ConnectionSummary />)

    const banner = await screen.findByRole('status')
    expect(banner.textContent).toContain('Outage')
    expect(banner.textContent).toContain('Internet (ISP)')
    expect(await screen.findByText(/since monitoring began/)).toBeTruthy()
  })

  it('reports a failed request', async () => {
    vi.spyOn(api, 'getConnectivity').mockRejectedValue(new Error('boom'))
    renderWithClient(<ConnectionSummary />)

    expect((await screen.findByRole('alert')).textContent).toBe('boom')
  })
})

describe('outages panel', () => {
  it('lists outages with their location, duration and notification state', async () => {
    vi.spyOn(api, 'getOutages').mockResolvedValue([
      outage,
      { ...outage, id: 6, status: 'recovered', endedAt: '2026-10-18T08:10:00Z', durationSeconds: 600, scope: 'dns', closureReason: 'interrupted',
        notificationDeliveries: [{ provider: 'webhook', trigger: 'opened', status: 'delivered', attemptCount: 1, lastAttemptAt: null, deliveredAt: null, lastErrorSummary: null }] },
    ])
    renderWithClient(<OutagesPanel />)

    const rows = await screen.findAllByRole('row')
    expect(within(rows[1]).getByText('Ongoing')).toBeTruthy()
    expect(within(rows[1]).getByText('2m 5s')).toBeTruthy()
    expect(within(rows[2]).getByText('Closed (interrupted)')).toBeTruthy()
    expect(within(rows[2]).getByText('1/1 delivered')).toBeTruthy()
    expect(within(rows[2]).getByText(/DNS/)).toBeTruthy()
  })

  it('says so when nothing has happened', async () => {
    vi.spyOn(api, 'getOutages').mockResolvedValue([])
    renderWithClient(<OutagesPanel />)

    expect(await screen.findByText(/No outages recorded/)).toBeTruthy()
  })
})

describe('targets panel', () => {
  it('shows each target with its latest result', async () => {
    vi.spyOn(api, 'getProbeTargets').mockResolvedValue([gateway, cloudflare, { ...cloudflare, id: 3, name: 'Third', enabled: false }])
    renderWithClient(
      <TargetsPanel status={{ ...healthy, targets: [statusOf(gateway), statusOf(cloudflare, { up: false, rttMs: null, error: 'timeout', lossPercent: 40 })] }} />,
    )

    const rows = await screen.findAllByRole('row')
    expect(within(rows[1]).getByText('Up')).toBeTruthy()
    expect(within(rows[1]).getByText('Default gateway')).toBeTruthy()
    expect(within(rows[2]).getByText('Down')).toBeTruthy()
    expect(within(rows[2]).getByText(/timeout/)).toBeTruthy()
    expect(within(rows[2]).getByText('40%')).toBeTruthy()
    expect(within(rows[3]).getByText('Paused')).toBeTruthy()
  })

  it('adds a TCP target with its port and refreshes the list', async () => {
    vi.spyOn(api, 'getProbeTargets').mockResolvedValue([gateway])
    const create = vi.spyOn(api, 'createProbeTarget').mockResolvedValue({ ...gateway, id: 9 })
    renderWithClient(<TargetsPanel status={healthy} />)

    await userEvent.click(await screen.findByRole('button', { name: 'Add target' }))
    const form = screen.getByRole('form', { name: 'Add probe target' })
    await userEvent.type(within(form).getByLabelText('Name'), 'NAS')
    await userEvent.selectOptions(within(form).getByLabelText('Check'), 'tcp')
    await userEvent.type(within(form).getByLabelText('Host or IP'), '192.168.1.20')
    await userEvent.type(within(form).getByLabelText('Port'), '445')
    await userEvent.click(within(form).getByRole('button', { name: 'Add target' }))

    await waitFor(() => expect(create).toHaveBeenCalledWith({
      name: 'NAS', kind: 'tcp', role: 'custom', target: '192.168.1.20', port: 445, resolver: null, enabled: true,
    }))
    await waitFor(() => expect(screen.queryByRole('form', { name: 'Add probe target' })).toBeNull())
  })

  it('switches the role to one the new check allows and shows the resolver field for DNS', async () => {
    vi.spyOn(api, 'getProbeTargets').mockResolvedValue([])
    renderWithClient(<TargetsPanel status={healthy} />)

    await userEvent.click(await screen.findByRole('button', { name: 'Add target' }))
    const form = screen.getByRole('form', { name: 'Add probe target' })
    await userEvent.selectOptions(within(form).getByLabelText('Counts as'), 'gateway')
    await userEvent.selectOptions(within(form).getByLabelText('Check'), 'dns')

    expect((within(form).getByLabelText('Counts as') as HTMLSelectElement).value).toBe('custom')
    expect(within(form).getByLabelText('Resolver')).toBeTruthy()
    expect(within(form).queryByLabelText('Port')).toBeNull()
  })

  it('shows the server message when a target is rejected', async () => {
    vi.spyOn(api, 'getProbeTargets').mockResolvedValue([])
    vi.spyOn(api, 'createProbeTarget').mockRejectedValue(new Error('Another probe target already has this name.'))
    renderWithClient(<TargetsPanel status={healthy} />)

    await userEvent.click(await screen.findByRole('button', { name: 'Add target' }))
    const form = screen.getByRole('form', { name: 'Add probe target' })
    await userEvent.type(within(form).getByLabelText('Name'), 'Gateway')
    await userEvent.type(within(form).getByLabelText('Host or IP'), '1.1.1.1')
    await userEvent.click(within(form).getByRole('button', { name: 'Add target' }))

    expect((await within(form).findByRole('alert')).textContent).toBe('Another probe target already has this name.')
  })

  it('asks for confirmation before deleting and can pause a target', async () => {
    vi.spyOn(api, 'getProbeTargets').mockResolvedValue([cloudflare])
    const remove = vi.spyOn(api, 'deleteProbeTarget').mockResolvedValue(undefined)
    const update = vi.spyOn(api, 'updateProbeTarget').mockResolvedValue({ ...cloudflare, enabled: false })
    renderWithClient(<TargetsPanel status={healthy} />)

    await userEvent.click(await screen.findByRole('button', { name: 'Pause Cloudflare' }))
    await waitFor(() => expect(update).toHaveBeenCalledWith(2, expect.objectContaining({ enabled: false, name: 'Cloudflare' })))

    await userEvent.click(screen.getByRole('button', { name: 'Delete Cloudflare' }))
    expect(remove).not.toHaveBeenCalled()
    await userEvent.click(screen.getByRole('button', { name: 'Keep' }))
    expect(screen.getByRole('button', { name: 'Delete Cloudflare' })).toBeTruthy()

    await userEvent.click(screen.getByRole('button', { name: 'Delete Cloudflare' }))
    await userEvent.click(screen.getByRole('button', { name: 'Confirm delete Cloudflare' }))
    await waitFor(() => expect(remove).toHaveBeenCalledWith(2))
  })

  it('edits an existing target in place', async () => {
    vi.spyOn(api, 'getProbeTargets').mockResolvedValue([cloudflare])
    const update = vi.spyOn(api, 'updateProbeTarget').mockResolvedValue(cloudflare)
    renderWithClient(<TargetsPanel status={healthy} />)

    await userEvent.click(await screen.findByRole('button', { name: 'Edit Cloudflare' }))
    const form = screen.getByRole('form', { name: 'Edit Cloudflare' })
    await userEvent.clear(within(form).getByLabelText('Name'))
    await userEvent.type(within(form).getByLabelText('Name'), 'Cloudflare DNS')
    await userEvent.click(within(form).getByRole('button', { name: 'Save target' }))

    await waitFor(() => expect(update).toHaveBeenCalledWith(2, expect.objectContaining({ name: 'Cloudflare DNS', target: '1.1.1.1' })))
  })
})

describe('connectivity page', () => {
  it('assembles the path, uptime, chart, outages and targets', async () => {
    vi.spyOn(api, 'getConnectivity').mockResolvedValue(healthy)
    vi.spyOn(api, 'getProbeTargets').mockResolvedValue([gateway, cloudflare])
    vi.spyOn(api, 'getOutages').mockResolvedValue([])
    vi.spyOn(api, 'getUptime').mockResolvedValue({
      range: '24h', from: '', to: '', uptimePercent: 100, outageCount: 0, totalOutageSeconds: 0,
      longestOutageSeconds: 0, coversFullRange: true,
    })
    const series = vi.spyOn(api, 'getProbeSeries').mockResolvedValue({
      targetId: 1, resolution: 'raw', from: '', to: '',
      points: [{ timestamp: '2026-10-18T10:00:00Z', rttMs: 10, rttMinMs: 10, rttMaxMs: 10, jitterMs: null, lossPercent: 0, sent: 1 }],
    })
    renderWithClient(<ConnectivityPage />)

    expect(await screen.findByRole('heading', { name: 'Connectivity' })).toBeTruthy()
    expect(await screen.findByRole('heading', { name: 'Connection path' })).toBeTruthy()
    expect(await screen.findAllByText('100%')).toHaveLength(3)
    expect(await screen.findByRole('img', { name: /Latency between 10 ms and 10 ms/ })).toBeTruthy()
    await waitFor(() => expect(series).toHaveBeenCalled())

    await userEvent.click(screen.getByRole('button', { name: '24H' }))
    await waitFor(() => expect(series.mock.calls.length).toBeGreaterThan(1))
    await userEvent.selectOptions(screen.getByLabelText('Target'), 'Cloudflare')
    await waitFor(() => expect(series).toHaveBeenLastCalledWith(2, expect.any(Date), expect.any(Date)))
  })

  it('tells the owner how to turn monitoring on when it is off', async () => {
    vi.spyOn(api, 'getConnectivity').mockResolvedValue({ ...healthy, monitoring: false, state: 'disabled' })
    vi.spyOn(api, 'getProbeTargets').mockResolvedValue([])
    vi.spyOn(api, 'getOutages').mockResolvedValue([])
    vi.spyOn(api, 'getUptime').mockResolvedValue({
      range: '24h', from: '', to: '', uptimePercent: 100, outageCount: 0, totalOutageSeconds: 0,
      longestOutageSeconds: 0, coversFullRange: true,
    })
    renderWithClient(<ConnectivityPage />)

    expect(await screen.findByText(/Turn monitoring on under Settings/)).toBeTruthy()
    expect(await screen.findByText('Add a target to see its latency.')).toBeTruthy()
  })
})
