import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { MemoryRouter } from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import { api, type Device, type LanStatus, type Setting } from '../lib/api'
import { LanSection } from '../settings/LanSection'
import DevicesPage from './DevicesPage'
import { filterDevices, ipToNumber, sortDevices } from './format'

function renderWithClient(ui: ReactNode) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>{ui}</MemoryRouter>
    </QueryClientProvider>,
  )
}

function device(over: Partial<Device>): Device {
  return {
    id: 1, mac: 'aa:bb:cc:00:00:01', ip: '192.168.1.10', displayName: 'Device', name: null, hostname: null,
    description: null, vendor: null, known: true, watch: false, online: true, randomizedMac: false,
    firstSeenAt: '2026-10-18T10:00:00Z', lastSeenAt: '2026-10-18T10:00:00Z', lastStateChangeAt: '2026-10-18T10:00:00Z',
    ...over,
  }
}

const router = device({ id: 1, displayName: 'Home router', ip: '192.168.1.1', vendor: 'TP-Link', mac: 'aa:bb:cc:00:00:01' })
const laptop = device({ id: 2, displayName: 'ali-laptop', ip: '192.168.1.20', mac: 'aa:bb:cc:00:00:02', name: 'Ali laptop' })
const camera = device({ id: 3, displayName: 'cam-7', ip: '192.168.1.9', mac: 'aa:bb:cc:00:00:03', known: false })
const phone = device({ id: 4, displayName: 'Phone', ip: '192.168.1.30', mac: 'da:a1:19:00:00:04', online: false, randomizedMac: true })

const status: LanStatus = {
  enabled: true, scanning: false, nextScanAt: '2026-10-18T10:05:00Z',
  lastScan: { startedAt: '2026-10-18T10:00:00Z', finishedAt: '2026-10-18T10:00:11Z', networks: '192.168.1.0/24', hostsFound: 3, success: true, error: null, trigger: 'scheduled' },
  totalDevices: 4, onlineDevices: 3, unknownDevices: 1, networks: ['192.168.1.0/24'], onlyContainerNetworks: false,
}

function mockData(lan: Partial<LanStatus> = {}, devices: Device[] = [router, laptop, camera, phone]) {
  vi.spyOn(api, 'getLanStatus').mockResolvedValue({ ...status, ...lan })
  vi.spyOn(api, 'getDevices').mockResolvedValue(devices)
  vi.spyOn(api, 'getDeviceEvents').mockResolvedValue([])
}

describe('device helpers', () => {
  it('orders addresses numerically, not as text', () => {
    expect(ipToNumber('192.168.1.9')).toBeLessThan(ipToNumber('192.168.1.10'))
    expect(ipToNumber(null)).toBeGreaterThan(ipToNumber('255.255.255.255'))
  })

  it('puts devices that need a decision first, then online ones', () => {
    expect(sortDevices([router, phone, laptop, camera]).map((d) => d.displayName)).toEqual([
      'cam-7', 'Home router', 'ali-laptop', 'Phone',
    ])
  })

  it('filters by state and by anything printed in the row', () => {
    const all = [router, laptop, camera, phone]
    expect(filterDevices(all, 'online', '').map((d) => d.id)).toEqual([1, 2, 3])
    expect(filterDevices(all, 'offline', '').map((d) => d.id)).toEqual([4])
    expect(filterDevices(all, 'unknown', '').map((d) => d.id)).toEqual([3])
    expect(filterDevices(all, 'all', 'tp-link').map((d) => d.id)).toEqual([1])
    expect(filterDevices(all, 'all', '00:00:02').map((d) => d.id)).toEqual([2])
    expect(filterDevices(all, 'all', '192.168.1.30').map((d) => d.id)).toEqual([4])
  })
})

describe('devices page', () => {
  it('lists devices with unknown ones first and flags them', async () => {
    mockData()
    renderWithClient(<DevicesPage />)

    const rows = await screen.findAllByRole('row')
    expect(within(rows[1]).getByText('cam-7')).toBeTruthy()
    expect(within(rows[1]).getByText('New')).toBeTruthy()
    expect(within(rows[2]).getByText('Home router')).toBeTruthy()
    expect(within(rows[4]).getByText('Private MAC')).toBeTruthy()
    expect(within(rows[4]).getByText('Offline')).toBeTruthy()
    const counts = screen.getByRole('group', { name: 'Device counts' })
    expect(within(counts).getByText('3')).toBeTruthy()
    expect(within(counts).getByText('Check these')).toBeTruthy()
  })

  it('narrows the list with the search box and the state filter', async () => {
    mockData()
    renderWithClient(<DevicesPage />)
    await screen.findByText('cam-7')

    await userEvent.type(screen.getByLabelText('Search'), 'tp-link')
    expect(screen.queryByText('cam-7')).toBeNull()
    expect(screen.getByText('Home router')).toBeTruthy()

    await userEvent.clear(screen.getByLabelText('Search'))
    await userEvent.selectOptions(screen.getByLabelText('Show'), 'offline')
    expect(screen.getByText('Phone')).toBeTruthy()
    expect(screen.queryByText('Home router')).toBeNull()

    await userEvent.type(screen.getByLabelText('Search'), 'zzz')
    expect(await screen.findByText('No device matches the filter.')).toBeTruthy()
  })

  it('starts a scan and marks everything known on request', async () => {
    mockData()
    const scan = vi.spyOn(api, 'startLanScan').mockResolvedValue({ status: 'started' })
    const markAll = vi.spyOn(api, 'markAllDevicesKnown').mockResolvedValue({ updated: 1 })
    renderWithClient(<DevicesPage />)

    await userEvent.click(await screen.findByRole('button', { name: 'Scan now' }))
    await waitFor(() => expect(scan).toHaveBeenCalled())
    await userEvent.click(screen.getByRole('button', { name: 'Mark all as known' }))
    await waitFor(() => expect(markAll).toHaveBeenCalled())
  })

  it('explains why scanning is unavailable', async () => {
    mockData({ enabled: false, nextScanAt: null, lastScan: null, onlyContainerNetworks: true, networks: [] }, [])
    renderWithClient(<DevicesPage />)

    expect((await screen.findAllByRole('note')).map((note) => note.textContent).join(' ')).toMatch(/Network scanning is off.*only sees Docker/s)
    expect((screen.getByRole('button', { name: 'Scan now' }) as HTMLButtonElement).disabled).toBe(true)
    expect(screen.getByText('Turn scanning on to build the list.')).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Settings → Network scanning' }).getAttribute('href')).toBe('/settings')
  })

  it('shows why the last scan failed and keeps the button off while scanning', async () => {
    mockData({
      scanning: true,
      lastScan: { ...status.lastScan!, success: false, error: 'No device answered the scan.', hostsFound: 0 },
    })
    renderWithClient(<DevicesPage />)

    expect((await screen.findByRole('alert')).textContent).toContain('No device answered the scan.')
    expect((screen.getByRole('button', { name: 'Scanning…' }) as HTMLButtonElement).disabled).toBe(true)
  })

  it('edits a device from its details and records the choice', async () => {
    mockData()
    const update = vi.spyOn(api, 'updateDevice').mockResolvedValue({ ...camera, known: true, watch: true, name: 'Porch camera' })
    renderWithClient(<DevicesPage />)

    await userEvent.click(await screen.findByRole('button', { name: 'Details for cam-7' }))
    const form = await screen.findByRole('form', { name: 'Edit cam-7' })
    expect((within(form).getByRole('button', { name: 'Save device' }) as HTMLButtonElement).disabled).toBe(true)
    await userEvent.type(within(form).getByLabelText('Name'), 'Porch camera')
    await userEvent.click(within(form).getByLabelText('This is my device (known)'))
    await userEvent.click(within(form).getByLabelText(/Watch it/))
    await userEvent.click(within(form).getByRole('button', { name: 'Save device' }))

    await waitFor(() => expect(update).toHaveBeenCalledWith(3, { name: 'Porch camera', known: true, watch: true }))
  })

  it('asks before forgetting a device and shows its history', async () => {
    mockData()
    vi.spyOn(api, 'getDeviceEvents').mockResolvedValue([
      { id: 2, deviceId: 3, timestamp: '2026-10-18T09:00:00Z', type: 'ipChanged', detail: '192.168.1.8 → 192.168.1.9' },
      { id: 1, deviceId: 3, timestamp: '2026-10-18T08:00:00Z', type: 'joined', detail: '192.168.1.8' },
    ])
    const forget = vi.spyOn(api, 'deleteDevice').mockResolvedValue(undefined)
    renderWithClient(<DevicesPage />)

    await userEvent.click(await screen.findByRole('button', { name: 'Details for cam-7' }))
    expect(await screen.findByText(/Address changed: 192.168.1.8 → 192.168.1.9/)).toBeTruthy()
    expect(screen.getByText(/Joined the network as 192.168.1.8/)).toBeTruthy()

    await userEvent.click(screen.getByRole('button', { name: 'Forget device' }))
    expect(forget).not.toHaveBeenCalled()
    await userEvent.click(screen.getByRole('button', { name: 'Confirm forget' }))
    await waitFor(() => expect(forget).toHaveBeenCalledWith(3))
  })
})

function lanSetting(key: string, value: string | number | boolean | null, type: Setting['type'], over: Partial<Setting> = {}): Setting {
  return {
    key, group: 'lan', type, value, defaultValue: value, nullable: type === 'text', source: 'default', locked: false,
    lockReason: null, environmentVariable: key.toUpperCase().replaceAll('.', '__'), options: null,
    min: type === 'integer' ? 1 : null, max: type === 'integer' ? 100 : null, updatedAt: null, ...over,
  }
}

const lanSettings: Setting[] = [
  lanSetting('lan.enabled', false, 'boolean'),
  lanSetting('lan.scanIntervalMinutes', 5, 'integer'),
  lanSetting('lan.cidr', null, 'text'),
  lanSetting('lan.offlineAfterScans', 3, 'integer'),
  lanSetting('lan.alertOnNewDevice', true, 'boolean'),
]

describe('network scanning settings', () => {
  it('turns scanning on together with the networks to scan', async () => {
    const update = vi.spyOn(api, 'updateSettings').mockResolvedValue({ settings: lanSettings })
    renderWithClient(<LanSection settings={lanSettings} />)

    await userEvent.click(screen.getByLabelText('Scan the local network'))
    await userEvent.type(screen.getByLabelText(/^Networks to scan/), '192.168.1.0/24')
    await userEvent.click(screen.getByRole('button', { name: 'Save scanning settings' }))

    await waitFor(() => expect(update).toHaveBeenCalledWith({ 'lan.enabled': true, 'lan.cidr': '192.168.1.0/24' }))
  })

  it('shows the server message when the networks are refused', async () => {
    vi.spyOn(api, 'updateSettings').mockRejectedValue(new Error('lan.cidr: Only private address ranges can be scanned.'))
    renderWithClient(<LanSection settings={lanSettings} />)

    await userEvent.type(screen.getByLabelText(/^Networks to scan/), '8.8.8.0/24')
    await userEvent.click(screen.getByRole('button', { name: 'Save scanning settings' }))

    expect((await screen.findByRole('alert')).textContent).toContain('Only private address ranges')
  })

  it('validates the numbers before saving', async () => {
    renderWithClient(<LanSection settings={lanSettings} />)

    await userEvent.clear(screen.getByLabelText(/^Scan every/))
    await userEvent.type(screen.getByLabelText(/^Scan every/), '0')

    expect(await screen.findByText('Must be between 1 and 100.')).toBeTruthy()
    expect((screen.getByRole('button', { name: 'Save scanning settings' }) as HTMLButtonElement).disabled).toBe(true)
  })
})
