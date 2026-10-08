import type { ReactNode } from 'react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { api, type Setting } from '../lib/api'
import SettingsPage from '../SettingsPage'

function makeDefaultSettings(): Setting[] {
  return [
    {
      key: 'speedtest.engine',
      group: 'speedtest',
      type: 'choice',
      value: 'librespeed',
      defaultValue: 'librespeed',
      nullable: false,
      source: 'default',
      locked: false,
      lockReason: null,
      environmentVariable: 'SPEEDTEST__ENGINE',
      options: ['librespeed', 'cloudflare', 'ookla'],
      min: null,
      max: null,
      updatedAt: null,
    },
    {
      key: 'speedtest.librespeed.timeoutSeconds',
      group: 'speedtest',
      type: 'integer',
      value: 120,
      defaultValue: 120,
      nullable: false,
      source: 'default',
      locked: false,
      lockReason: null,
      environmentVariable: 'SPEEDTEST__LIBRESPEED__TIMEOUTSECONDS',
      options: null,
      min: 10,
      max: 1800,
      updatedAt: null,
    },
    {
      key: 'speedtest.librespeed.serverId',
      group: 'speedtest',
      type: 'integer',
      value: null,
      defaultValue: null,
      nullable: true,
      source: 'default',
      locked: false,
      lockReason: null,
      environmentVariable: 'SPEEDTEST__LIBRESPEED__SERVERID',
      options: null,
      min: 1,
      max: null,
      updatedAt: null,
    },
    {
      key: 'speedtest.librespeed.executablePath',
      group: 'speedtest',
      type: 'text',
      value: 'librespeed-cli',
      defaultValue: 'librespeed-cli',
      nullable: false,
      source: 'default',
      locked: true,
      lockReason: 'restricted',
      environmentVariable: 'SPEEDTEST__LIBRESPEED__EXECUTABLEPATH',
      options: null,
      min: null,
      max: null,
      updatedAt: null,
    },
    {
      key: 'speedtest.cloudflare.timeoutSeconds',
      group: 'speedtest',
      type: 'integer',
      value: 300,
      defaultValue: 300,
      nullable: false,
      source: 'default',
      locked: false,
      lockReason: null,
      environmentVariable: 'SPEEDTEST__CLOUDFLARE__TIMEOUTSECONDS',
      options: null,
      min: 10,
      max: 1800,
      updatedAt: null,
    },
    {
      key: 'speedtest.cloudflare.executablePath',
      group: 'speedtest',
      type: 'text',
      value: 'cfspeedtest',
      defaultValue: 'cfspeedtest',
      nullable: false,
      source: 'default',
      locked: true,
      lockReason: 'restricted',
      environmentVariable: 'SPEEDTEST__CLOUDFLARE__EXECUTABLEPATH',
      options: null,
      min: null,
      max: null,
      updatedAt: null,
    },
    {
      key: 'speedtest.ookla.timeoutSeconds',
      group: 'speedtest',
      type: 'integer',
      value: 120,
      defaultValue: 120,
      nullable: false,
      source: 'default',
      locked: false,
      lockReason: null,
      environmentVariable: 'SPEEDTEST__OOKLA__TIMEOUTSECONDS',
      options: null,
      min: 10,
      max: 1800,
      updatedAt: null,
    },
    {
      key: 'speedtest.ookla.serverId',
      group: 'speedtest',
      type: 'integer',
      value: null,
      defaultValue: null,
      nullable: true,
      source: 'default',
      locked: false,
      lockReason: null,
      environmentVariable: 'SPEEDTEST__OOKLA__SERVERID',
      options: null,
      min: 1,
      max: null,
      updatedAt: null,
    },
    {
      key: 'speedtest.ookla.executablePath',
      group: 'speedtest',
      type: 'text',
      value: null,
      defaultValue: null,
      nullable: true,
      source: 'default',
      locked: true,
      lockReason: 'restricted',
      environmentVariable: 'SPEEDTEST__OOKLA__EXECUTABLEPATH',
      options: null,
      min: null,
      max: null,
      updatedAt: null,
    },
    {
      key: 'speedtest.ookla.acceptLicense',
      group: 'speedtest',
      type: 'boolean',
      value: false,
      defaultValue: false,
      nullable: false,
      source: 'default',
      locked: false,
      lockReason: null,
      environmentVariable: 'SPEEDTEST__OOKLA__ACCEPTLICENSE',
      options: null,
      min: null,
      max: null,
      updatedAt: null,
    },
    {
      key: 'retention.days',
      group: 'retention',
      type: 'integer',
      value: 365,
      defaultValue: 365,
      nullable: false,
      source: 'default',
      locked: false,
      lockReason: null,
      environmentVariable: 'DATARETENTION__DAYS',
      options: null,
      min: 1,
      max: null,
      updatedAt: null,
    },
  ]
}

function renderWithClient(component: ReactNode) {
  const client = new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: 0 },
      mutations: { retry: false },
    },
  })
  return render(<QueryClientProvider client={client}>{component}</QueryClientProvider>)
}

function setupFakeServer(initialSettings: Setting[]) {
  const current: Setting[] = initialSettings.map((s) => ({ ...s }))

  const getSpy = vi.spyOn(api, 'getSettings').mockImplementation(async () => ({
    settings: current.map((s) => ({ ...s })),
  }))

  const updateSpy = vi.spyOn(api, 'updateSettings').mockImplementation(async (values) => {
    for (const [k, v] of Object.entries(values)) {
      const idx = current.findIndex((s) => s.key === k)
      if (idx >= 0) {
        const item = current[idx]!
        if (v === null) {
          current[idx] = {
            ...item,
            value: item.defaultValue,
            source: 'default',
            updatedAt: null,
          }
        } else {
          current[idx] = {
            ...item,
            value: v,
            source: 'stored',
            updatedAt: '2026-10-08T12:00:00Z',
          }
        }
      }
    }
    return { settings: current.map((s) => ({ ...s })) }
  })

  // ScheduleSettings is rendered by SettingsPage — provide a mock
  vi.spyOn(api, 'getSchedule').mockResolvedValue({
    enabled: false,
    cronExpression: '*/30 * * * *',
    timezone: 'UTC',
    updatedAt: '',
    nextRuns: [],
  })

  return { getSpy, updateSpy, current }
}

describe('SettingsPage', () => {
  it('renders engine options, per-engine inputs, and sources from settings response', async () => {
    setupFakeServer(makeDefaultSettings())
    renderWithClient(<SettingsPage />)

    expect(await screen.findByRole('heading', { name: 'Test engine and options' })).toBeTruthy()

    const libreRadio = screen.getByRole('radio', { name: /LibreSpeed/ }) as HTMLInputElement
    const cfRadio = screen.getByRole('radio', { name: /Cloudflare/ }) as HTMLInputElement
    const ooklaRadio = screen.getByRole('radio', { name: /Ookla/ }) as HTMLInputElement

    expect(libreRadio.checked).toBe(true)
    expect(cfRadio.checked).toBe(false)
    expect(ooklaRadio.checked).toBe(false)
    expect(ooklaRadio.disabled).toBe(true)

    const timeoutInput = screen.getByLabelText(/^Timeout \(seconds\)/) as HTMLInputElement
    expect(timeoutInput.value).toBe('120')

    const serverIdInput = screen.getByLabelText(/^Server ID/) as HTMLInputElement
    expect(serverIdInput.value).toBe('')

    const execInput = screen.getByLabelText(/^Executable path/) as HTMLInputElement
    expect(execInput.value).toBe('librespeed-cli')
    expect(execInput.readOnly).toBe(true)

    const retentionInput = screen.getByLabelText(/^Keep results for \(days\)/) as HTMLInputElement
    expect(retentionInput.value).toBe('365')

    const sources = screen.getAllByText('Default')
    expect(sources.length).toBeGreaterThanOrEqual(3)
  })

  it('changing engine sends only changed keys on save', async () => {
    const { updateSpy } = setupFakeServer(makeDefaultSettings())
    const user = userEvent.setup()
    renderWithClient(<SettingsPage />)

    const cfRadio = await screen.findByRole('radio', { name: /Cloudflare/ })
    await user.click(cfRadio)

    const saveBtn = screen.getByRole('button', { name: 'Save speed-test settings' })
    await user.click(saveBtn)

    await waitFor(() => {
      expect(updateSpy).toHaveBeenCalledWith({ 'speedtest.engine': 'cloudflare' })
    })

    expect(await screen.findByText(/Speed-test settings saved/)).toBeTruthy()
  })

  it('changing timeout and server id sends only changed keys', async () => {
    const { updateSpy } = setupFakeServer(makeDefaultSettings())
    const user = userEvent.setup()
    renderWithClient(<SettingsPage />)

    const timeout = await screen.findByLabelText(/^Timeout \(seconds\)/)
    await user.clear(timeout)
    await user.type(timeout, '90')

    const serverId = screen.getByLabelText(/^Server ID/)
    await user.type(serverId, '42')

    const saveBtn = screen.getByRole('button', { name: 'Save speed-test settings' })
    await user.click(saveBtn)

    await waitFor(() => {
      expect(updateSpy).toHaveBeenCalledWith({
        'speedtest.librespeed.timeoutSeconds': 90,
        'speedtest.librespeed.serverId': 42,
      })
    })
  })

  it('changing retention sends only retention.days on save', async () => {
    const { updateSpy } = setupFakeServer(makeDefaultSettings())
    const user = userEvent.setup()
    renderWithClient(<SettingsPage />)

    const retentionInput = await screen.findByLabelText(/^Keep results for \(days\)/)
    await user.clear(retentionInput)
    await user.type(retentionInput, '180')

    const saveBtn = screen.getByRole('button', { name: 'Save retention settings' })
    await user.click(saveBtn)

    await waitFor(() => {
      expect(updateSpy).toHaveBeenCalledWith({ 'retention.days': 180 })
    })

    expect(await screen.findByText(/Retention settings saved/)).toBeTruthy()
  })

  it('reset to default sends null for that setting key', async () => {
    const custom = makeDefaultSettings()
    const timeoutSetting = custom.find((s) => s.key === 'speedtest.librespeed.timeoutSeconds')!
    timeoutSetting.value = 60
    timeoutSetting.source = 'stored'
    timeoutSetting.updatedAt = '2026-10-01T00:00:00Z'

    const { updateSpy } = setupFakeServer(custom)
    const user = userEvent.setup()
    renderWithClient(<SettingsPage />)

    const resetBtn = await screen.findByRole('button', { name: 'Reset Timeout (seconds) to default' })
    await user.click(resetBtn)

    await waitFor(() => {
      expect(updateSpy).toHaveBeenCalledWith({ 'speedtest.librespeed.timeoutSeconds': null })
    })

    expect(await screen.findByText(/Reset to default/)).toBeTruthy()
  })

  it('env-locked field is disabled/read-only and shows its variable name and reason', async () => {
    const custom = makeDefaultSettings()
    const retentionSetting = custom.find((s) => s.key === 'retention.days')!
    retentionSetting.value = 90
    retentionSetting.source = 'environment'
    retentionSetting.locked = true
    retentionSetting.lockReason = 'environment'

    setupFakeServer(custom)
    renderWithClient(<SettingsPage />)

    const retentionInput = (await screen.findByLabelText(/^Keep results for \(days\)/)) as HTMLInputElement
    expect(retentionInput.disabled).toBe(true)
    expect(retentionInput.readOnly).toBe(true)

    expect(screen.getByText('Set by DATARETENTION__DAYS')).toBeTruthy()
    expect(
      screen.getByText(/Set by environment variable DATARETENTION__DAYS; remove it to change this here/),
    ).toBeTruthy()

    expect(screen.queryByRole('button', { name: 'Reset Keep results for (days) to default' })).toBeNull()
  })

  it('restricted executable path is read-only with the restricted reason', async () => {
    setupFakeServer(makeDefaultSettings())
    renderWithClient(<SettingsPage />)

    const execInput = (await screen.findByLabelText(/^Executable path/)) as HTMLInputElement
    expect(execInput.readOnly).toBe(true)

    expect(
      screen.getByText(/Executable paths can only be set with environment variables \(SPEEDTEST__LIBRESPEED__EXECUTABLEPATH\) until authentication is available/),
    ).toBeTruthy()
  })

  it('Ookla selection is disabled until the license box is checked, sending both in PUT', async () => {
    const { updateSpy } = setupFakeServer(makeDefaultSettings())
    const user = userEvent.setup()
    renderWithClient(<SettingsPage />)

    const ooklaRadio = (await screen.findByRole('radio', { name: /Ookla/ })) as HTMLInputElement
    expect(ooklaRadio.disabled).toBe(true)

    const licenseCheckbox = screen.getByRole('checkbox', {
      name: /I have read and accept the Ookla EULA/,
    }) as HTMLInputElement
    expect(licenseCheckbox.checked).toBe(false)

    await user.click(licenseCheckbox)
    expect(licenseCheckbox.checked).toBe(true)
    expect(ooklaRadio.disabled).toBe(false)

    await user.click(ooklaRadio)
    expect(ooklaRadio.checked).toBe(true)

    const saveBtn = screen.getByRole('button', { name: 'Save speed-test settings' })
    await user.click(saveBtn)

    await waitFor(() => {
      expect(updateSpy).toHaveBeenCalledWith({
        'speedtest.engine': 'ookla',
        'speedtest.ookla.acceptLicense': true,
      })
    })
  })

  it('displays server errors in a role=alert region', async () => {
    setupFakeServer(makeDefaultSettings())
    vi.spyOn(api, 'updateSettings').mockRejectedValue(
      new Error('speedtest.engine is locked by environment variable SPEEDTEST__ENGINE.'),
    )

    const user = userEvent.setup()
    renderWithClient(<SettingsPage />)

    const cfRadio = await screen.findByRole('radio', { name: /Cloudflare/ })
    await user.click(cfRadio)

    const saveBtn = screen.getByRole('button', { name: 'Save speed-test settings' })
    await user.click(saveBtn)

    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toContain(
      'speedtest.engine is locked by environment variable SPEEDTEST__ENGINE.',
    )
  })

  it('shows accepted-by-environment when Ookla license is env-locked', async () => {
    const custom = makeDefaultSettings()
    const license = custom.find((s) => s.key === 'speedtest.ookla.acceptLicense')!
    license.value = true
    license.source = 'environment'
    license.locked = true
    license.lockReason = 'environment'

    setupFakeServer(custom)
    renderWithClient(<SettingsPage />)

    expect(await screen.findByText(/Accepted by environment variable/)).toBeTruthy()
    const ooklaRadio = screen.getByRole('radio', { name: /Ookla/ }) as HTMLInputElement
    expect(ooklaRadio.disabled).toBe(false)
  })

  it('renders load error if settings query fails', async () => {
    vi.spyOn(api, 'getSettings').mockRejectedValue(new Error('Network offline'))
    vi.spyOn(api, 'getSchedule').mockResolvedValue({
      enabled: false,
      cronExpression: '*/30 * * * *',
      timezone: 'UTC',
      updatedAt: '',
      nextRuns: [],
    })

    renderWithClient(<SettingsPage />)

    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toContain('Failed to load settings: Network offline')
  })
})
