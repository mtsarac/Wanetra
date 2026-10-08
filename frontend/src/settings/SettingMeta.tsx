import type { Setting } from '../lib/api'
import { lockReasonText, sourceText } from './types'

type SettingMetaProps = {
  setting: Setting
  onReset: (key: string) => void
  isResetting: boolean
  fieldLabel: string
}

export function SettingMeta({ setting, onReset, isResetting, fieldLabel }: SettingMetaProps) {
  const isEnv = setting.source === 'environment'
  const isStored = setting.source === 'stored'
  const lockText = lockReasonText(setting)
  const canReset = isStored && !setting.locked

  return (
    <div className="st-meta-row">
      <div className="st-meta-info">
        <p className="st-source-line">
          Source: <span className="st-source-label">{sourceText(setting)}</span>
        </p>
        {lockText && (
          <p id={`lock-${setting.key}`} className="st-lock-note">
            {lockText}
          </p>
        )}
      </div>
      {canReset && (
        <button
          type="button"
          className="st-reset-button"
          onClick={() => onReset(setting.key)}
          disabled={isResetting}
          aria-label={`Reset ${fieldLabel} to default`}
        >
          Reset to default<span className="st-sr-only"> for {fieldLabel}</span>
        </button>
      )}
      {!isStored && !isEnv && (
        <span className="st-sr-only">At default value</span>
      )}
    </div>
  )
}
