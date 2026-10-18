import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { api, UNAUTHORIZED_EVENT, type AuthStatus } from '../lib/api'
import AuthGate from './AuthGate'
import { SecuritySection } from './SecuritySection'
import { AuthContext } from './AuthContext'

const signedIn: AuthStatus = {
  authEnabled: true, envLocked: false, setupRequired: false, accountExists: true,
  authenticated: true, username: 'owner', metricsTokenConfigured: false,
}

function renderWithClient(ui: React.ReactNode) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>)
}

describe('AuthGate', () => {
  it('shows the app once the session is authenticated', async () => {
    vi.spyOn(api, 'getAuthStatus').mockResolvedValue(signedIn)
    renderWithClient(<AuthGate><p>dashboard</p></AuthGate>)

    expect(await screen.findByText('dashboard')).toBeTruthy()
  })

  it('shows the app without a login when authentication is off', async () => {
    vi.spyOn(api, 'getAuthStatus').mockResolvedValue({ ...signedIn, authEnabled: false, username: null })
    renderWithClient(<AuthGate><p>dashboard</p></AuthGate>)

    expect(await screen.findByText('dashboard')).toBeTruthy()
  })

  it('asks for the setup code, username and password on a fresh install', async () => {
    vi.spyOn(api, 'getAuthStatus').mockResolvedValue({
      ...signedIn, setupRequired: true, accountExists: false, authenticated: false, username: null,
    })
    const setup = vi.spyOn(api, 'setupAuth').mockResolvedValue(signedIn)
    renderWithClient(<AuthGate><p>dashboard</p></AuthGate>)

    await userEvent.type(await screen.findByLabelText('Setup code'), 'ABCD-EFGH-JKLM')
    await userEvent.type(screen.getByLabelText(/^password/i), 'a-long-password')
    await userEvent.type(screen.getByLabelText('Confirm password'), 'a-different-one')
    await userEvent.click(screen.getByRole('button', { name: 'Create account' }))
    expect((await screen.findByRole('alert')).textContent).toContain('do not match')
    expect(setup).not.toHaveBeenCalled()

    await userEvent.clear(screen.getByLabelText('Confirm password'))
    await userEvent.type(screen.getByLabelText('Confirm password'), 'a-long-password')
    await userEvent.click(screen.getByRole('button', { name: 'Create account' }))
    await waitFor(() => expect(setup).toHaveBeenCalledWith('ABCD-EFGH-JKLM', 'admin', 'a-long-password'))
  })

  it('shows the sign-in form and reports bad credentials', async () => {
    vi.spyOn(api, 'getAuthStatus').mockResolvedValue({ ...signedIn, authenticated: false, username: null })
    vi.spyOn(api, 'login').mockRejectedValue(new Error('Invalid username or password.'))
    renderWithClient(<AuthGate><p>dashboard</p></AuthGate>)

    await userEvent.type(await screen.findByLabelText('Username'), 'owner')
    await userEvent.type(screen.getByLabelText('Password'), 'nope-nope')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect((await screen.findByRole('alert')).textContent).toBe('Invalid username or password.')
    expect(screen.queryByText('dashboard')).toBeNull()
  })

  it('returns to the sign-in form when any request reports an expired session', async () => {
    const status = vi.spyOn(api, 'getAuthStatus').mockResolvedValue(signedIn)
    renderWithClient(<AuthGate><p>dashboard</p></AuthGate>)
    await screen.findByText('dashboard')

    status.mockResolvedValue({ ...signedIn, authenticated: false, username: null })
    window.dispatchEvent(new Event(UNAUTHORIZED_EVENT))

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeTruthy()
  })
})

describe('SecuritySection', () => {
  function renderSection(status: AuthStatus) {
    return renderWithClient(
      <AuthContext.Provider value={{ status, signOut: vi.fn() }}><SecuritySection /></AuthContext.Provider>,
    )
  }

  it('lets the owner generate a metrics token and shows it once', async () => {
    vi.spyOn(api, 'createMetricsToken').mockResolvedValue({ token: 'wnm_secret' })
    renderSection(signedIn)

    await userEvent.click(screen.getByRole('button', { name: 'Generate token' }))

    expect(await screen.findByText('wnm_secret')).toBeTruthy()
  })

  it('requires the password to turn authentication off', async () => {
    const disable = vi.spyOn(api, 'disableAuth').mockResolvedValue(undefined)
    renderSection(signedIn)

    const button = screen.getByRole('button', { name: 'Turn off authentication' })
    expect((button as HTMLButtonElement).disabled).toBe(true)
    await userEvent.type(screen.getByLabelText('Confirm with your password'), 'my-password')
    await userEvent.click(button)

    await waitFor(() => expect(disable).toHaveBeenCalledWith('my-password'))
  })

  it('offers to turn authentication back on', async () => {
    const enable = vi.spyOn(api, 'enableAuth').mockResolvedValue(undefined)
    renderSection({ ...signedIn, authEnabled: false, username: null })

    await userEvent.click(screen.getByRole('button', { name: 'Turn on authentication' }))

    await waitFor(() => expect(enable).toHaveBeenCalled())
  })

  it('explains that the environment variable controls authentication', () => {
    renderSection({ ...signedIn, authEnabled: false, envLocked: true, username: null })

    expect(screen.getByRole('note').textContent).toContain('WANETRA_AUTH')
    expect(screen.queryByRole('button', { name: 'Turn on authentication' })).toBeNull()
  })

  it('does not render without an auth context', () => {
    const { container } = renderWithClient(<SecuritySection />)

    expect(container.textContent).toBe('')
  })
})
