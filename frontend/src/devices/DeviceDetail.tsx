import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Button } from '../components/ui/button'
import { api, type Device } from '../lib/api'
import { describeEvent, formatWhen } from './format'

export function DeviceDetail({ device, onClose }: { device: Device; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [name, setName] = useState(device.name ?? '')
  const [known, setKnown] = useState(device.known)
  const [watch, setWatch] = useState(device.watch)
  const [confirming, setConfirming] = useState(false)

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ['devices'] })
    void queryClient.invalidateQueries({ queryKey: ['lan-status'] })
    void queryClient.invalidateQueries({ queryKey: ['probe-targets'] })
    void queryClient.invalidateQueries({ queryKey: ['connectivity'] })
  }

  const events = useQuery({ queryKey: ['device-events', device.id], queryFn: () => api.getDeviceEvents(device.id, 20) })
  const save = useMutation({
    mutationFn: () => api.updateDevice(device.id, { name, known, watch }),
    onSuccess: () => { refresh(); void queryClient.invalidateQueries({ queryKey: ['device-events', device.id] }) },
  })
  const forget = useMutation({
    mutationFn: () => api.deleteDevice(device.id),
    onSuccess: () => { refresh(); onClose() },
  })

  const dirty = name.trim() !== (device.name ?? '') || known !== device.known || watch !== device.watch

  function submit(event: FormEvent) {
    event.preventDefault()
    save.mutate()
  }

  return (
    <section className="panel device-detail" aria-labelledby="device-detail-heading">
      <div className="panel-head">
        <div>
          <p className="kicker">Device</p>
          <h2 id="device-detail-heading">{device.displayName}</h2>
        </div>
        <Button variant="outline" size="sm" onClick={onClose}>Close</Button>
      </div>

      <dl className="device-facts">
        <div><dt>Status</dt><dd>{device.online ? 'Online' : 'Offline'} since {formatWhen(device.lastStateChangeAt)}</dd></div>
        <div><dt>IP address</dt><dd>{device.ip ?? '—'}</dd></div>
        <div><dt>MAC address</dt><dd>{device.mac}{device.randomizedMac ? ' (private address)' : ''}</dd></div>
        <div><dt>Vendor</dt><dd>{device.vendor ?? (device.randomizedMac ? 'Hidden by a private address' : 'Unknown')}</dd></div>
        <div><dt>Hostname</dt><dd>{device.hostname ?? '—'}</dd></div>
        <div><dt>Described as</dt><dd>{device.description ?? '—'}</dd></div>
        <div><dt>First seen</dt><dd>{formatWhen(device.firstSeenAt)}</dd></div>
        <div><dt>Last seen</dt><dd>{formatWhen(device.lastSeenAt)}</dd></div>
      </dl>

      <form onSubmit={submit} className="target-form" aria-label={`Edit ${device.displayName}`} noValidate>
        <label>
          <span className="st-field-title">Name</span>
          <input
            className="st-input"
            value={name}
            maxLength={64}
            placeholder={device.hostname ?? device.vendor ?? 'Give this device a name'}
            onChange={(e) => setName(e.target.value)}
          />
        </label>
        <label className="st-checkbox-label">
          <input type="checkbox" className="st-checkbox-input" checked={known} onChange={(e) => setKnown(e.target.checked)} />
          <span>This is my device (known)</span>
        </label>
        <label className="st-checkbox-label">
          <input type="checkbox" className="st-checkbox-input" checked={watch} onChange={(e) => setWatch(e.target.checked)} />
          <span>Watch it: alert when it goes offline and ping it for latency</span>
        </label>
        {save.isError && <p className="st-error-alert" role="alert">{save.error.message}</p>}
        <div className="security-row">
          <Button type="submit" disabled={save.isPending || !dirty}>{save.isPending ? 'Saving…' : 'Save device'}</Button>
          {confirming ? (
            <>
              <Button type="button" onClick={() => forget.mutate()} disabled={forget.isPending}>Confirm forget</Button>
              <Button type="button" variant="outline" onClick={() => setConfirming(false)}>Keep</Button>
            </>
          ) : (
            <Button type="button" variant="outline" onClick={() => setConfirming(true)}>Forget device</Button>
          )}
        </div>
      </form>

      <h3 className="device-history-title">History</h3>
      {events.isLoading ? (
        <p className="muted">Loading history…</p>
      ) : events.data?.length ? (
        <ul className="device-history">
          {events.data.map((event) => (
            <li key={event.id}><time dateTime={event.timestamp}>{formatWhen(event.timestamp)}</time> {describeEvent(event)}</li>
          ))}
        </ul>
      ) : (
        <p className="muted">Nothing has changed since Wanetra first saw it.</p>
      )}
    </section>
  )
}
