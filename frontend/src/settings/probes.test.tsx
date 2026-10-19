import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { api, type Setting } from '../lib/api'
import { ProbesSection } from './ProbesSection'

function setting(key: string, value: number | boolean, min: number | null, max: number | null, over: Partial<Setting> = {}): Setting {
  return {
    key,
    group: 'probes',
    type: typeof value === 'boolean' ? 'boolean' : 'integer',
    value,
    defaultValue: value,
    nullable: false,
    source: 'default',
    locked: false,
    lockReason: null,
    environmentVariable: key.toUpperCase().replaceAll('.', '__'),
    options: null,
    min,
    max,
    updatedAt: null,
    ...over,
  }
}

function probeSettings(over: Record<string, Partial<Setting>> = {}): Setting[] {
  const base: [string, number | boolean, number | null, number | null][] = [
    ['probes.enabled', true, null, null],
    ['probes.intervalSeconds', 10, 5, 300],
    ['probes.timeoutMs', 2000, 200, 10000],
    ['probes.outage.failuresRequired', 3, 1, 20],
    ['probes.outage.recoveriesRequired', 3, 1, 20],
    ['probes.retention.rawHours', 48, 1, 720],
    ['probes.retention.minuteDays', 30, 1, 365],
  ]
  return base.map(([key, value, min, max]) => setting(key, value, min, max, over[key]))
}

function renderSection(settings: Setting[]) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <ProbesSection settings={settings} />
    </QueryClientProvider>,
  )
}

describe('connection monitoring settings', () => {
  it('shows every monitoring setting with its current value', () => {
    renderSection(probeSettings())

    expect((screen.getByLabelText('Monitor the connection') as HTMLInputElement).checked).toBe(true)
    expect((screen.getByLabelText(/^Check every/) as HTMLInputElement).value).toBe('10')
    expect((screen.getByLabelText(/^Probe timeout/) as HTMLInputElement).value).toBe('2000')
    expect((screen.getByLabelText(/^Keep every probe/) as HTMLInputElement).value).toBe('48')
    expect((screen.getByRole('button', { name: 'Save monitoring settings' }) as HTMLButtonElement).disabled).toBe(true)
  })

  it('saves only what changed', async () => {
    const update = vi.spyOn(api, 'updateSettings').mockResolvedValue({ settings: probeSettings() })
    renderSection(probeSettings())

    await userEvent.click(screen.getByLabelText('Monitor the connection'))
    await userEvent.clear(screen.getByLabelText(/^Check every/))
    await userEvent.type(screen.getByLabelText(/^Check every/), '30')
    await userEvent.click(screen.getByRole('button', { name: 'Save monitoring settings' }))

    await waitFor(() => expect(update).toHaveBeenCalledWith({ 'probes.enabled': false, 'probes.intervalSeconds': 30 }))
  })

  it('refuses out-of-range values before saving', async () => {
    const update = vi.spyOn(api, 'updateSettings')
    renderSection(probeSettings())

    await userEvent.clear(screen.getByLabelText(/^Check every/))
    await userEvent.type(screen.getByLabelText(/^Check every/), '1')

    expect(await screen.findByText('Must be between 5 and 300.')).toBeTruthy()
    expect((screen.getByRole('button', { name: 'Save monitoring settings' }) as HTMLButtonElement).disabled).toBe(true)
    expect(update).not.toHaveBeenCalled()
  })

  it('locks a setting that an environment variable controls', () => {
    renderSection(probeSettings({
      'probes.intervalSeconds': { locked: true, lockReason: 'environment', source: 'environment', environmentVariable: 'Probes__IntervalSeconds' },
    }))

    const field = screen.getByLabelText(/^Check every/) as HTMLInputElement
    expect(field.disabled).toBe(true)
    expect(screen.getByText(/Set by environment variable Probes__IntervalSeconds/)).toBeTruthy()
  })

  it('renders nothing when the server does not know the settings', () => {
    const { container } = renderSection([])

    expect(container.textContent).toBe('')
  })
})
