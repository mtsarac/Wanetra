import { describe, expect, it, vi } from 'vitest'
import { getHistory } from './api'

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
      return {
        ok: true,
        status: 200,
        json: async () => ({ items, page, pageSize: 200, totalCount: 3, totalPages: 3 }),
      } as Response
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
    const fetchMock = vi.fn(async () => ({
      ok: true,
      status: 200,
      json: async () => ({ items: [], page: 1, pageSize: 200, totalCount: 0, totalPages: 0 }),
    } as Response))
    vi.stubGlobal('fetch', fetchMock)

    await expect(getHistory(new Date('2026-09-01T00:00:00Z'))).resolves.toEqual([])
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })
})
