import type { Device, DeviceEvent } from '../lib/api'

export function ipToNumber(ip: string | null): number {
  if (!ip) return Number.MAX_SAFE_INTEGER
  return ip.split('.').reduce((total, part) => total * 256 + Number(part), 0)
}

// Unknown devices first (they need a decision), then online ones, then by address.
export function sortDevices(devices: Device[]): Device[] {
  return [...devices].sort((a, b) =>
    Number(a.known) - Number(b.known)
    || Number(b.online) - Number(a.online)
    || ipToNumber(a.ip) - ipToNumber(b.ip)
    || a.displayName.localeCompare(b.displayName))
}

export type DeviceFilter = 'all' | 'online' | 'offline' | 'unknown'

export function filterDevices(devices: Device[], filter: DeviceFilter, search: string): Device[] {
  const needle = search.trim().toLowerCase()
  return devices.filter((device) => {
    if (filter === 'online' && !device.online) return false
    if (filter === 'offline' && device.online) return false
    if (filter === 'unknown' && device.known) return false
    if (!needle) return true
    return [device.displayName, device.name, device.hostname, device.vendor, device.description, device.mac, device.ip]
      .some((value) => value?.toLowerCase().includes(needle))
  })
}

export function formatWhen(value: string | null | undefined): string {
  return value ? new Date(value).toLocaleString([], { dateStyle: 'short', timeStyle: 'short' }) : '—'
}

export function describeEvent(event: DeviceEvent): string {
  switch (event.type) {
    case 'joined': return `Joined the network${event.detail ? ` as ${event.detail}` : ''}`
    case 'online': return `Back online${event.detail ? ` at ${event.detail}` : ''}`
    case 'offline': return 'Stopped answering'
    case 'ipChanged': return `Address changed: ${event.detail ?? ''}`
  }
}
