import { describe, expect, it, vi } from 'vitest'
import { api, getHistory } from './api'
declare global {
  interface PromiseConstructor {
    withResolvers<T>(): { promise: Promise<T>; resolve: (value: T) => void }
  }
}

function deferred<T>() {
  return Promise.withResolvers<T>()
}

describe('getHistory', () => {
  it('loads every bounded page in chronological order with both date filters', async () => {
    const from = new Date('2026-09-01T00:00:00.000Z')
    const to = new Date('2026-09-30T23:59:59.999Z')
    const rows = [
      { id: 1, timestamp: '2026-09-01T00:00:00Z' },
      { id: 2, timestamp: '2026-09-15T00:00:00Z' },
      { id: 3, timestamp: '2026-09-30T00:00:00Z' },
    ]
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const url = new URL(String(input), 'http://localhost')
      const page = Number(url.searchParams.get('page'))
      const items = page === 1 ? [rows[0]] : page === 2 ? [rows[1]] : [rows[2]]
      return new Response(JSON.stringify({ items, page, pageSize: 200, totalCount: 3, totalPages: 3 }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      })
    })
    vi.stubGlobal('fetch', fetchMock)

    const history = await getHistory(from, to)

    expect(history.map((result) => result.id)).toEqual([1, 2, 3])
    expect(fetchMock).toHaveBeenCalledTimes(3)
    const urls = fetchMock.mock.calls.map(([input]) => new URL(String(input), 'http://localhost'))
    expect(urls.map((url) => url.searchParams.get('page'))).toEqual(['1', '2', '3'])
    for (const url of urls) {
      expect(url.searchParams.get('pageSize')).toBe('200')
      expect(url.searchParams.get('sort')).toBe('asc')
      expect(url.searchParams.get('from')).toBe(from.toISOString())
      expect(url.searchParams.get('to')).toBe(to.toISOString())
    }
  })

  it('does not request additional pages for an empty result set', async () => {
    const fetchMock = vi.fn(async () => new Response(JSON.stringify({
      items: [], page: 1, pageSize: 200, totalCount: 0, totalPages: 0,
    }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
    vi.stubGlobal('fetch', fetchMock)

    await expect(getHistory(new Date('2026-09-01T00:00:00Z'))).resolves.toEqual([])
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })
  it('keeps page requests sequential for long ranges', async () => {
    const pending = new Map<number, (response: Response) => void>()
    const requestedPages = Array.from({ length: 24 }, () => deferred<void>())
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const page = Number(new URL(String(input), 'http://localhost').searchParams.get('page'))
      const response = deferred<Response>()
      pending.set(page, response.resolve)
      requestedPages[page - 1].resolve(undefined)
      return response.promise
    })
    vi.stubGlobal('fetch', fetchMock)

    const historyPromise = getHistory(new Date('2026-09-01T00:00:00Z'))
    for (let page = 1; page <= requestedPages.length; page++) {
      await requestedPages[page - 1].promise
      expect(fetchMock).toHaveBeenCalledTimes(page)
      pending.get(page)!(new Response(JSON.stringify({
        items: [{ id: page, timestamp: `2026-09-${String(page).padStart(2, '0')}T00:00:00Z` }],
        page, pageSize: 200, totalCount: 24, totalPages: 24,
      }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
    }

    expect(await historyPromise).toHaveLength(24)
    expect(fetchMock).toHaveBeenCalledTimes(24)
  })
})

describe('shared request response handling', () => {
  it('resolves an empty successful response and 204 without parsing JSON', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(null, { status: 200 }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
    vi.stubGlobal('fetch', fetchMock)

    await expect(api.testNotification({ id: 1 })).resolves.toBeUndefined()
    await expect(api.testNotification({ id: 1 })).resolves.toBeUndefined()
  })

  it('parses JSON success and surfaces backend errors with status fallbacks', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({
        enabled: true, cronExpression: '*/30 * * * *', timezone: 'UTC', updatedAt: '', nextRuns: [],
      }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ message: 'Provider rejected request.' }), { status: 502 }))
      .mockResolvedValueOnce(new Response('not json', { status: 502 }))
      .mockResolvedValueOnce(new Response(null, { status: 503 }))
    vi.stubGlobal('fetch', fetchMock)

    await expect(api.getSchedule()).resolves.toMatchObject({ timezone: 'UTC' })
    await expect(api.testNotification({ id: 1 })).rejects.toThrow('Provider rejected request.')
    await expect(api.testNotification({ id: 1 })).rejects.toThrow('Request failed (502)')
    await expect(api.testNotification({ id: 1 })).rejects.toThrow('Request failed (503)')
  })
})
