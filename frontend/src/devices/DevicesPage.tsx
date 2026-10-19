import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router'
import { Button } from '../components/ui/button'
import { api } from '../lib/api'
import { DeviceDetail } from './DeviceDetail'
import { filterDevices, formatWhen, sortDevices, type DeviceFilter } from './format'
import '../connectivity/connectivity.css'
import './devices.css'

export default function DevicesPage() {
  const queryClient = useQueryClient()
  const [filter, setFilter] = useState<DeviceFilter>('all')
  const [search, setSearch] = useState('')
  const [selectedId, setSelectedId] = useState<number | null>(null)

  const status = useQuery({
    queryKey: ['lan-status'],
    queryFn: api.getLanStatus,
    refetchInterval: (query) => (query.state.data?.scanning ? 2_000 : 15_000),
    staleTime: 0,
  })
  const devices = useQuery({ queryKey: ['devices'], queryFn: api.getDevices, refetchInterval: 15_000, staleTime: 0 })

  const scan = useMutation({
    mutationFn: api.startLanScan,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['lan-status'] }),
  })
  const markAll = useMutation({
    mutationFn: api.markAllDevicesKnown,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['devices'] })
      void queryClient.invalidateQueries({ queryKey: ['lan-status'] })
    },
  })

  const lan = status.data
  const all = devices.data ?? []
  const shown = sortDevices(filterDevices(all, filter, search))
  const selected = all.find((device) => device.id === selectedId)
  const busy = scan.isPending || lan?.scanning === true
  const error = status.error ?? devices.error ?? scan.error ?? markAll.error

  return (
    <main id="main-content" className="page-content">
      <header className="page-title">
        <div>
          <p className="kicker">Local network</p>
          <h1>Devices</h1>
        </div>
        <Button disabled={!lan?.enabled || busy} onClick={() => scan.mutate()}>
          {busy ? 'Scanning…' : 'Scan now'}
        </Button>
      </header>

      {error && <div className="error" role="alert">{error.message}</div>}

      {lan && !lan.enabled && (
        <section className="callout" role="note">
          <strong>Network scanning is off.</strong>{' '}
          Turn it on under <Link to="/settings">Settings → Network scanning</Link>.
        </section>
      )}
      {lan?.onlyContainerNetworks && (
        <section className="callout warn" role="note">
          <strong>Wanetra only sees Docker&apos;s own network.</strong>{' '}
          To scan your LAN run it with host networking: <code>docker compose -f compose.lan.yaml up -d</code>. This works on Linux hosts.
        </section>
      )}
      {lan?.lastScan && !lan.lastScan.success && (
        <section className="callout warn" role="alert">
          <strong>The last scan failed.</strong> {lan.lastScan.error}
        </section>
      )}

      <section className="panel" aria-labelledby="scan-heading">
        <div className="panel-head">
          <div>
            <p className="kicker">Inventory</p>
            <h2 id="scan-heading">Overview</h2>
          </div>
          <span className="muted" role="status" aria-live="polite">
            {lan?.scanning ? 'Scan running…' : lan?.lastScan ? `Last scan ${formatWhen(lan.lastScan.finishedAt)}` : 'No scan yet'}
          </span>
        </div>
        <div className="stat-grid" role="group" aria-label="Device counts">
          <div className="stat-card"><span>Online</span><strong>{lan?.onlineDevices ?? '—'}</strong><small>of {lan?.totalDevices ?? '—'} seen</small></div>
          <div className={`stat-card${lan?.unknownDevices ? ' attention' : ''}`}>
            <span>Not marked as known</span><strong>{lan?.unknownDevices ?? '—'}</strong>
            <small>{lan?.unknownDevices ? 'Check these' : 'Everything is accounted for'}</small>
          </div>
          <div className="stat-card">
            <span>Networks</span><strong>{lan?.networks.length ? lan.networks.join(', ') : '—'}</strong>
            <small>{lan?.enabled && lan.nextScanAt ? `Next scan ${formatWhen(lan.nextScanAt)}` : 'Scanning is off'}</small>
          </div>
        </div>
        {lan && lan.unknownDevices > 0 && (
          <div className="security-row">
            <Button variant="outline" onClick={() => markAll.mutate()} disabled={markAll.isPending}>Mark all as known</Button>
            <span className="muted">Do this once you have checked the list.</span>
          </div>
        )}
      </section>

      <section className="panel" aria-labelledby="devices-heading">
        <div className="panel-head">
          <div>
            <p className="kicker">Everything Wanetra has seen</p>
            <h2 id="devices-heading">All devices</h2>
          </div>
          <span className="muted">{shown.length} shown</span>
        </div>
        <div className="filter-bar" role="search" aria-label="Filter devices">
          <label>
            <span>Search</span>
            <input type="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Name, IP, MAC or vendor" />
          </label>
          <label>
            <span>Show</span>
            <select value={filter} onChange={(e) => setFilter(e.target.value as DeviceFilter)}>
              <option value="all">All devices</option>
              <option value="online">Online</option>
              <option value="offline">Offline</option>
              <option value="unknown">Not marked as known</option>
            </select>
          </label>
        </div>
        {devices.isLoading ? (
          <div className="empty">Loading devices…</div>
        ) : shown.length ? (
          <div className="table-wrap" tabIndex={0} role="region" aria-label="Devices table">
            <table>
              <caption className="sr-only">Devices on the local network</caption>
              <thead>
                <tr>
                  <th scope="col">Status</th>
                  <th scope="col">Device</th>
                  <th scope="col">IP address</th>
                  <th scope="col">MAC address</th>
                  <th scope="col">Last seen</th>
                  <th scope="col"><span className="sr-only">Actions</span></th>
                </tr>
              </thead>
              <tbody>
                {shown.map((device) => (
                  <tr key={device.id} className={device.id === selectedId ? 'selected-row' : undefined}>
                    <td><span className={`device-state ${device.online ? 'online' : 'offline'}`}>{device.online ? 'Online' : 'Offline'}</span></td>
                    <th scope="row">
                      {device.displayName}
                      {!device.known && <span className="badge warn">New</span>}
                      {device.watch && <span className="badge">Watched</span>}
                      {device.randomizedMac && <span className="badge" title="This device uses a private MAC address that can change per network">Private MAC</span>}
                      <br /><small className="muted">{device.vendor ?? device.description ?? '—'}</small>
                    </th>
                    <td>{device.ip ?? '—'}</td>
                    <td><code>{device.mac}</code></td>
                    <td>{formatWhen(device.lastSeenAt)}</td>
                    <td>
                      <Button variant="outline" size="sm" onClick={() => setSelectedId(device.id)} aria-label={`Details for ${device.displayName}`}>
                        Details
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <div className="empty">
            {all.length ? 'No device matches the filter.' : lan?.enabled ? 'No devices yet. The first scan can take a minute.' : 'Turn scanning on to build the list.'}
          </div>
        )}
      </section>

      {selected && <DeviceDetail key={selected.id} device={selected} onClose={() => setSelectedId(null)} />}
    </main>
  )
}
