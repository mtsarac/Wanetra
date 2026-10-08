import { Button } from '../components/ui/button'

type FormFooterProps = {
  isSaving: boolean
  isDirty: boolean
  hasErrors: boolean
  statusMessage: string | null
  serverError: string | null
  saveLabel?: string
}

export function FormFooter({
  isSaving,
  isDirty,
  hasErrors,
  statusMessage,
  serverError,
  saveLabel = 'Save changes',
}: FormFooterProps) {
  const isDisabled = isSaving || !isDirty || hasErrors

  return (
    <div className="st-actions-bar">
      <div className="st-actions-left">
        <Button
          type="submit"
          disabled={isDisabled}
          className="st-submit-btn"
        >
          {isSaving ? 'Saving…' : saveLabel}
        </Button>
        {isDirty && !isSaving && (
          <span className="st-dirty-indicator">Unsaved changes</span>
        )}
      </div>
      <p className="st-status-msg" role="status" aria-live="polite">
        {statusMessage ?? ''}
      </p>
      {serverError && (
        <p className="st-error-alert" role="alert">
          {serverError}
        </p>
      )}
    </div>
  )
}
