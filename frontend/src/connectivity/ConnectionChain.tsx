import type { ConnectivityStatus, LayerState } from '../lib/api'
import { layerLabels } from './format'

type Node = { id: string; label: string; state: LayerState; detail: string }

export function ConnectionChain({ status }: { status: ConnectivityStatus }) {
  const nodes: Node[] = [
    { id: 'device', label: 'Wanetra', state: 'up', detail: 'This server' },
    { id: 'gateway', label: 'Gateway', state: status.layers.gateway, detail: 'Router' },
    { id: 'internet', label: 'Internet', state: status.layers.internet, detail: 'Public IPs' },
    { id: 'dns', label: 'DNS', state: status.layers.dns, detail: 'Name lookups' },
  ]

  return (
    <ol className="chain" aria-label="Connection path">
      {nodes.map((node, index) => (
        <li key={node.id} className={`chain-node ${node.state}`}>
          {index > 0 && <span className="chain-link" aria-hidden="true" />}
          <span className="chain-dot" aria-hidden="true" />
          <span className="chain-text">
            <strong>{node.label}</strong>
            <small>{node.detail}</small>
            <span className="chain-state">{layerLabels[node.state]}</span>
          </span>
        </li>
      ))}
    </ol>
  )
}
