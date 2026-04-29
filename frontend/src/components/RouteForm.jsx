import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Button } from './ui/button'

// ── Zod schema ────────────────────────────────────────────────────────────────
const routeSchema = z.object({
  path:        z.string().min(1, 'Path is required').startsWith('/', 'Path must start with /'),
  method:      z.enum(['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'ANY'], { required_error: 'Method is required' }),
  destination: z.string().min(1, 'Destination is required').url('Must be a valid URL'),
  authRequired: z.boolean(),
  isActive:    z.boolean(),
  rateLimit: z.object({
    limit:         z.coerce.number().int().min(1, 'Min 1'),
    windowSeconds: z.coerce.number().int().min(1, 'Min 1'),
  }).nullable().optional(),
})

const METHODS = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'ANY']

// ── Dialog wrapper ────────────────────────────────────────────────────────────
// Simple accessible modal — no Radix dependency needed beyond what we already have.
function Dialog({ open, onClose, title, children }) {
  useEffect(() => {
    function onKey(e) { if (e.key === 'Escape') onClose() }
    if (open) document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [open, onClose])

  if (!open) return null

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      {/* Backdrop */}
      <div className="absolute inset-0 bg-black/50" onClick={onClose} />
      {/* Panel */}
      <div className="relative z-10 w-full max-w-lg bg-card rounded-lg border shadow-xl p-6 mx-4">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-semibold">{title}</h2>
          <button onClick={onClose} className="text-muted-foreground hover:text-foreground text-xl leading-none">&times;</button>
        </div>
        {children}
      </div>
    </div>
  )
}

// ── RouteForm ─────────────────────────────────────────────────────────────────
// create mode: route = null
// edit mode:   route = existing RouteDto
export default function RouteForm({ open, onClose, route, onSubmit }) {
  const isEdit = !!route

  const { register, handleSubmit, reset, formState: { errors, isSubmitting } } = useForm({
    resolver: zodResolver(routeSchema),
    defaultValues: {
      path:         '',
      method:       'GET',
      destination:  '',
      authRequired: false,
      isActive:     true,
      rateLimit:    null,
    },
  })

  // Re-populate when switching between create/edit
  useEffect(() => {
    if (open) {
      reset(route
        ? {
            path:         route.path,
            method:       route.method,
            destination:  route.destination,
            authRequired: route.authRequired,
            isActive:     route.isActive,
            rateLimit:    route.rateLimit ?? null,
          }
        : { path: '', method: 'GET', destination: '', authRequired: false, isActive: true, rateLimit: null }
      )
    }
  }, [open, route, reset])

  async function submit(data) {
    // Strip rateLimit if both fields are empty/null
    const payload = {
      ...data,
      rateLimit: (data.rateLimit?.limit && data.rateLimit?.windowSeconds) ? data.rateLimit : null,
    }
    await onSubmit(payload)
    onClose()
  }

  function Field({ label, error, children }) {
    return (
      <div>
        <label className="block text-sm font-medium mb-1">{label}</label>
        {children}
        {error && <p className="text-xs text-destructive mt-1">{error}</p>}
      </div>
    )
  }

  const inputCls = 'w-full border border-input rounded-md px-3 py-2 text-sm bg-background focus:outline-none focus:ring-2 focus:ring-ring'

  return (
    <Dialog open={open} onClose={onClose} title={isEdit ? 'Edit Route' : 'Create Route'}>
      <form onSubmit={handleSubmit(submit)} className="space-y-4">
        <Field label="Path" error={errors.path?.message}>
          <input {...register('path')} placeholder="/api/orders" className={inputCls} />
        </Field>

        <Field label="Method" error={errors.method?.message}>
          <select {...register('method')} className={inputCls}>
            {METHODS.map((m) => <option key={m} value={m}>{m}</option>)}
          </select>
        </Field>

        <Field label="Destination URL" error={errors.destination?.message}>
          <input {...register('destination')} placeholder="http://orders-service:8080" className={inputCls} />
        </Field>

        <div className="flex gap-6">
          <label className="flex items-center gap-2 text-sm cursor-pointer">
            <input type="checkbox" {...register('authRequired')} className="rounded" />
            Auth Required
          </label>
          <label className="flex items-center gap-2 text-sm cursor-pointer">
            <input type="checkbox" {...register('isActive')} className="rounded" />
            Active
          </label>
        </div>

        {/* Optional rate limit */}
        <details className="border rounded-md p-3">
          <summary className="text-sm font-medium cursor-pointer select-none">Rate Limit (optional)</summary>
          <div className="mt-3 grid grid-cols-2 gap-3">
            <Field label="Limit (requests)" error={errors.rateLimit?.limit?.message}>
              <input type="number" {...register('rateLimit.limit')} placeholder="100" className={inputCls} />
            </Field>
            <Field label="Window (seconds)" error={errors.rateLimit?.windowSeconds?.message}>
              <input type="number" {...register('rateLimit.windowSeconds')} placeholder="60" className={inputCls} />
            </Field>
          </div>
        </details>

        <div className="flex justify-end gap-3 pt-2">
          <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? 'Saving…' : isEdit ? 'Save Changes' : 'Create Route'}
          </Button>
        </div>
      </form>
    </Dialog>
  )
}
