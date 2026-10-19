import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Button } from '../components/ui/button'
import {
  api,
  type ConnectivityStatus,
  type ProbeKind,
  type ProbeRole,
  type ProbeTarget,
  type ProbeTargetInput,
} from '../lib/api'
import { formatMs } from './format'

const kindLabels: Record<ProbeKind, string> = {
  icmp: 'Ping (ICMP)',
  tcp: 'TCP port',
  dns: 'DNS lookup',
  http: 'HTTP request',
}

const roleLabels: Record<ProbeRole, string> = {
  gateway: 'Gateway (your router)',
  internet: 'Internet reachability',
  dns: 'DNS lookup',
  custom: 'Custom (shown, never raises an outage)',
}

export function rolesFor(kind: ProbeKind): ProbeRole[] {
  switch (kind) {
    case 'dns': return ['dns', 'custom']
    case 'http': return ['internet', 'custom']
    default: return ['gateway', 'internet', 'custom']
  }
}

const emptyInput: ProbeTargetInput = {
  name: '', kind: 'icmp', role: 'custom', target: '', port: null, resolver: '', enabled: true,
}

function describe(target: ProbeTarget): string {
  switch (target.kind) {
    case 'tcp': return `${target.target}:${target.port}`
    case 'dns': return `${target.target}${target.resolver ? ` via ${target.resolver}` : ' via system resolver'}`
    default: return target.target ?? 'Default gateway'
  }
}

function TargetForm({ initial, onDone }: { initial: ProbeTargetInput & { id?: number }; onDone: () => void }) {
  const queryClient = useQueryClient()
  const [draft, setDraft] = useState<ProbeTargetInput>(initial)
  const save = useMutation({
    mutationFn: () => {
      const input: ProbeTargetInput = {
        ...draft,
        target: draft.target?.trim() ? draft.target.trim() : null,
        resolver: draft.resolver?.trim() ? draft.resolver.trim() : null,
        port: draft.kind === 'tcp' ? draft.port : null,
      }
      return initial.id == null ? api.createProbeTarget(input) : api.updateProbeTarget(initial.id, input)
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['probe-targets'] })
      void queryClient.invalidateQueries({ queryKey: ['connectivity'] })
      onDone()
    },
  })

  const roles = rolesFor(draft.kind)
  const set = <K extends keyof ProbeTargetInput>(key: K, value: ProbeTargetInput[K]) =>
    setDraft((current) => ({ ...current, [key]: value }))

  function changeKind(kind: ProbeKind) {
    setDraft((current) => ({
      ...current,
      kind,
      role: rolesFor(kind).includes(current.role) ? current.role : 'custom',
    }))
  }

  function submit(event: FormEvent) {
    event.preventDefault()
    save.mutate()
  }

  const hostLabel = draft.kind === 'http' ? 'URL' : draft.kind === 'dns' ? 'Name to look up' : 'Host or IP'
  const gatewayAuto = draft.role === 'gateway' && (draft.kind === 'icmp' || draft.kind === 'tcp')

  return (
    <form onSubmit={submit} className="target-form" aria-label={initial.id == null ? 'Add probe target' : `Edit ${initial.name}`} noValidate>
      <div className="st-fields-grid">
        <label>
          <span className="st-field-title">Name</span>
          <input className="st-input" value={draft.name} maxLength={64} onChange={(e) => set('name', e.target.value)} />
        </label>
        <label>
          <span className="st-field-title">Check</span>
          <select className="st-input" value={draft.kind} onChange={(e) => changeKind(e.target.value as ProbeKind)}>
            {(Object.keys(kindLabels) as ProbeKind[]).map((kind) => <option key={kind} value={kind}>{kindLabels[kind]}</option>)}
          </select>
        </label>
        <label>
          <span className="st-field-title">Counts as</span>
          <select className="st-input" value={draft.role} onChange={(e) => set('role', e.target.value as ProbeRole)}>
            {roles.map((role) => <option key={role} value={role}>{roleLabels[role]}</option>)}
          </select>
        </label>
        <label>
          <span className="st-field-title">{hostLabel}</span>
          <input
            className="st-input"
            value={draft.target ?? ''}
            placeholder={gatewayAuto ? 'Empty = detect the default gateway' : undefined}
            onChange={(e) => set('target', e.target.value)}
          />
        </label>
        {draft.kind === 'tcp' && (
          <label>
            <span className="st-field-title">Port</span>
            <input
              className="st-input"
              type="number"
              min={1}
              max={65535}
              value={draft.port ?? ''}
              onChange={(e) => set('port', e.target.value === '' ? null : Number(e.target.value))}
            />
          </label>
        )}
        {draft.kind === 'dns' && (
          <label>
            <span className="st-field-title">Resolver</span>
            <input
              className="st-input"
              value={draft.resolver ?? ''}
              placeholder="Empty = system resolver"
              onChange={(e) => set('resolver', e.target.value)}
            />
          </label>
        )}
      </div>
      <label className="st-checkbox-label">
        <input type="checkbox" className="st-checkbox-input" checked={draft.enabled} onChange={(e) => set('enabled', e.target.checked)} />
        <span>Enabled</span>
      </label>
      {save.isError && <p className="st-error-alert" role="alert">{save.error.message}</p>}
      <div className="security-row">
        <Button type="submit" disabled={save.isPending || !draft.name.trim()}>
          {save.isPending ? 'Saving…' : initial.id == null ? 'Add target' : 'Save target'}
        </Button>
        <Button type="button" variant="outline" onClick={onDone}>Cancel</Button>
      </div>
    </form>
  )
}

function TargetRow({
  target,
  status,
  onEdit,
}: {
  target: ProbeTarget
  status: ConnectivityStatus['targets'][number] | undefined
  onEdit: () => void
}) {
  const queryClient = useQueryClient()
  const [confirming, setConfirming] = useState(false)
  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ['probe-targets'] })
    void queryClient.invalidateQueries({ queryKey: ['connectivity'] })
  }
  const toggle = useMutation({
    mutationFn: () => api.updateProbeTarget(target.id, {
      name: target.name, kind: target.kind, role: target.role, target: target.target,
      port: target.port, resolver: target.resolver, enabled: !target.enabled,
    }),
    onSuccess: refresh,
  })
  const remove = useMutation({ mutationFn: () => api.deleteProbeTarget(target.id), onSuccess: refresh })

  const state = !target.enabled ? 'off' : status?.up == null ? 'unknown' : status.up ? 'up' : 'down'
  const stateText = { off: 'Paused', unknown: 'No data', up: 'Up', down: 'Down' }[state]

  return (
    <tr>
      <th scope="row">{target.name}</th>
      <td>{kindLabels[target.kind]}<br /><small className="muted">{describe(target)}</small></td>
      <td>{target.role}</td>
      <td><span className={`target-state ${state}`}>{stateText}</span>{status?.error && state === 'down' && <small className="muted"> · {status.error}</small>}</td>
      <td>{formatMs(status?.rttMs)}</td>
      <td>{status?.lossPercent == null ? '—' : `${status.lossPercent.toFixed(0)}%`}</td>
      <td className="target-actions">
        <Button
          variant="outline"
          size="sm"
          onClick={() => toggle.mutate()}
          disabled={toggle.isPending}
          aria-label={`${target.enabled ? 'Pause' : 'Resume'} ${target.name}`}
        >
          {target.enabled ? 'Pause' : 'Resume'}
        </Button>
        <Button variant="outline" size="sm" onClick={onEdit} aria-label={`Edit ${target.name}`}>Edit</Button>
        {confirming ? (
          <>
            <Button size="sm" onClick={() => remove.mutate()} disabled={remove.isPending} aria-label={`Confirm delete ${target.name}`}>Confirm delete</Button>
            <Button variant="outline" size="sm" onClick={() => setConfirming(false)}>Keep</Button>
          </>
        ) : (
          <Button variant="outline" size="sm" onClick={() => setConfirming(true)} aria-label={`Delete ${target.name}`}>Delete</Button>
        )}
      </td>
    </tr>
  )
}

export function TargetsPanel({ status }: { status: ConnectivityStatus | undefined }) {
  const targets = useQuery({ queryKey: ['probe-targets'], queryFn: api.getProbeTargets })
  const [editing, setEditing] = useState<(ProbeTargetInput & { id?: number }) | null>(null)
  const statusById = new Map(status?.targets.map((item) => [item.target.id, item]))

  return (
    <section className="panel" aria-labelledby="targets-heading">
      <div className="panel-head">
        <div>
          <p className="kicker">What Wanetra checks</p>
          <h2 id="targets-heading">Probe targets</h2>
        </div>
        {!editing && <Button onClick={() => setEditing(emptyInput)}>Add target</Button>}
      </div>
      {targets.isError && <div className="error" role="alert">{targets.error.message}</div>}
      {editing && <TargetForm key={editing.id ?? 'new'} initial={editing} onDone={() => setEditing(null)} />}
      {targets.isLoading ? (
        <div className="empty">Loading targets…</div>
      ) : targets.data?.length ? (
        <div className="table-wrap" tabIndex={0} role="region" aria-label="Probe targets table">
          <table>
            <caption className="sr-only">Probe targets and their latest result</caption>
            <thead>
              <tr>
                <th scope="col">Name</th>
                <th scope="col">Check</th>
                <th scope="col">Role</th>
                <th scope="col">State</th>
                <th scope="col">Latency</th>
                <th scope="col">Loss (5 min)</th>
                <th scope="col"><span className="sr-only">Actions</span></th>
              </tr>
            </thead>
            <tbody>
              {targets.data.map((target) => (
                <TargetRow key={target.id} target={target} status={statusById.get(target.id)} onEdit={() => setEditing(target)} />
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <div className="empty">No targets. Add one to start monitoring.</div>
      )}
    </section>
  )
}
