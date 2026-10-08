import type { Setting } from '../lib/api'
import {
  formatTimestamp,
  lockReasonText,
  OOKLA_ACCEPT_LICENSE_KEY,
  sourceText,
} from './types'

type OoklaLicenseBlockProps = {
  setting: Setting | undefined
  draftAccepted: boolean
  isEngineOokla: boolean
  isLicenseLocked: boolean
  onToggle: (checked: boolean) => void
  onReset: (key: string) => void
  isResetting: boolean
}

export function OoklaLicenseBlock({
  setting,
  draftAccepted,
  isEngineOokla,
  isLicenseLocked,
  onToggle,
  onReset,
  isResetting,
}: OoklaLicenseBlockProps) {
  if (!setting) return null

  const isAcceptedByEnv = setting.source === 'environment' && Boolean(setting.value)
  const isRejectedByEnv = setting.source === 'environment' && !setting.value
  const lockText = lockReasonText(setting)
  const acceptedTime = formatTimestamp(setting.updatedAt)

  const isCheckboxDisabled = isLicenseLocked || (isEngineOokla && draftAccepted)
  const isStored = setting.source === 'stored'
  const canReset = isStored && !setting.locked && !isEngineOokla

  return (
    <div className="st-license-card">
      <h3 className="st-h3">Ookla Speedtest license acceptance</h3>
      <p className="st-license-intro">
        Ookla Speedtest is proprietary software by Ookla, LLC. Using the Ookla engine requires accepting their EULA:
      </p>
      <ul className="st-license-terms">
        <li>Personal, non-commercial use on a single personal computer only.</li>
        <li>No redistribution. Not for routers, modems, or shared network appliances.</li>
        <li>Wanetra downloads the official CLI directly from Ookla on first use.</li>
        <li>Each test sends measurement data to Ookla servers.</li>
        <li>Wanetra maintainers cannot confirm your use is allowed and this is not legal advice. For business or shared use, select LibreSpeed or Cloudflare.</li>
      </ul>
      <p className="st-license-links">
        Review:{' '}
        <a href="https://www.speedtest.net/about/eula" target="_blank" rel="noreferrer noopener">
          Ookla EULA<span className="st-sr-only"> (opens in new tab)</span>
        </a>
        {', '}
        <a href="https://www.speedtest.net/about/terms" target="_blank" rel="noreferrer noopener">
          Terms of Use<span className="st-sr-only"> (opens in new tab)</span>
        </a>
        {', and '}
        <a href="https://www.ookla.com/privacy" target="_blank" rel="noreferrer noopener">
          Privacy Policy<span className="st-sr-only"> (opens in new tab)</span>
        </a>
        .
      </p>

      <label
        htmlFor="ookla-license-checkbox"
        className={`st-checkbox-label ${isCheckboxDisabled ? 'st-disabled' : ''}`}
      >
        <input
          id="ookla-license-checkbox"
          type="checkbox"
          className="st-checkbox-input"
          checked={draftAccepted}
          disabled={isCheckboxDisabled}
          onChange={(e) => onToggle(e.target.checked)}
          aria-describedby={[
            'license-terms-hint',
            lockText ? `lock-${OOKLA_ACCEPT_LICENSE_KEY}` : null,
          ].filter(Boolean).join(' ') || undefined}
        />
        <span>
          I have read and accept the Ookla EULA, Terms of Use and Privacy Policy, and my use is personal and non-commercial.
        </span>
      </label>
      <p id="license-terms-hint" className="st-sr-only">
        Acceptance required before selecting the Ookla speed-test engine.
      </p>

      {isAcceptedByEnv && (
        <p className="st-accepted-time">
          Accepted by environment variable <code>{setting.environmentVariable}</code>.
        </p>
      )}

      {!isAcceptedByEnv && !isRejectedByEnv && draftAccepted && acceptedTime && (
        <p className="st-accepted-time">
          Accepted on <time dateTime={setting.updatedAt ?? undefined}>{acceptedTime}</time>.
        </p>
      )}

      {isEngineOokla && draftAccepted && !isLicenseLocked && (
        <p className="st-help">
          To withdraw license acceptance, select a different speed-test engine first.
        </p>
      )}

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
            aria-label="Reset Ookla license acceptance to default"
          >
            Reset to default<span className="st-sr-only"> for Ookla license</span>
          </button>
        )}
      </div>
    </div>
  )
}
