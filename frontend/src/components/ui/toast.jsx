// Minimal toast context — no extra library needed.
// Usage: const { toast } = useToast()
//        toast({ title: 'Error', description: '...', variant: 'destructive' })

import { createContext, useCallback, useContext, useReducer } from 'react'
import { cn } from '../../lib/utils'

const ToastContext = createContext(null)

let nextId = 0

function reducer(state, action) {
  switch (action.type) {
    case 'ADD':    return [...state, action.toast]
    case 'REMOVE': return state.filter((t) => t.id !== action.id)
    default:       return state
  }
}

export function ToastProvider({ children }) {
  const [toasts, dispatch] = useReducer(reducer, [])

  const toast = useCallback(({ title, description, variant = 'default', duration = 4000 }) => {
    const id = ++nextId
    dispatch({ type: 'ADD', toast: { id, title, description, variant } })
    setTimeout(() => dispatch({ type: 'REMOVE', id }), duration)
  }, [])

  return (
    <ToastContext.Provider value={{ toast }}>
      {children}

      {/* Toast viewport */}
      <div className="fixed bottom-4 right-4 z-50 flex flex-col gap-2 max-w-sm w-full pointer-events-none">
        {toasts.map((t) => (
          <div
            key={t.id}
            className={cn(
              'rounded-lg border px-4 py-3 shadow-lg pointer-events-auto bg-card text-card-foreground',
              t.variant === 'destructive' && 'border-destructive bg-destructive text-destructive-foreground',
            )}
          >
            {t.title && <p className="font-semibold text-sm">{t.title}</p>}
            {t.description && <p className="text-sm opacity-90">{t.description}</p>}
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  )
}

export function useToast() {
  const ctx = useContext(ToastContext)
  if (!ctx) throw new Error('useToast must be used inside <ToastProvider>')
  return ctx
}
