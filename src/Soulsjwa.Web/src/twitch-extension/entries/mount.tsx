import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { ExtensionApp } from '../components/ExtensionApp'
import type { ExtensionView } from '../views'
import '../styles/extension.css'

/** Shared bootstrap of the five entry pages, one per Twitch view. */
export function mount(view: ExtensionView): void {
  const root = document.getElementById('root')
  if (!root) throw new Error('Missing #root element')
  createRoot(root).render(
    <StrictMode>
      <ExtensionApp view={view} />
    </StrictMode>,
  )
}
