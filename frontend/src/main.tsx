import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createBrowserRouter, Navigate, RouterProvider } from 'react-router'
import './index.css'
import AlertSettings from './AlertSettings'
import App from './App.tsx'
import AppShell from './AppShell'
import HistoryPage from './HistoryPage'
import NotificationsPanel from './Notifications'
import SettingsPage from './SettingsPage'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      staleTime: 15_000,
    },
  },
})

const router = createBrowserRouter([
  {
    path: '/',
    element: <AppShell />,
    children: [
      { index: true, element: <App /> },
      { path: 'history', element: <HistoryPage /> },
      { path: 'alerts', element: <AlertSettings /> },
      { path: 'notifications', element: <NotificationsPanel /> },
      { path: 'settings', element: <SettingsPage /> },
      { path: '*', element: <Navigate to="/" replace /> },
    ],
  },
])

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  </StrictMode>,
)
