import { useEffect } from 'react'
import { Button } from './ui/button'

// Simple AlertDialog-style confirmation modal.
export default function DeleteDialog({ open, onClose, route, onConfirm, isDeleting }) {
  useEffect(() => {
    function onKey(e) { if (e.key === 'Escape') onClose() }
    if (open) document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [open, onClose])

  if (!open || !route) return null

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/50" onClick={onClose} />
      <div className="relative z-10 w-full max-w-sm bg-card rounded-lg border shadow-xl p-6 mx-4">
        <h2 className="text-lg font-semibold mb-1">Delete Route</h2>
        <p className="text-sm text-muted-foreground mb-4">
          Are you sure you want to delete{' '}
          <span className="font-mono font-medium text-foreground">{route.method} {route.path}</span>?
          This action cannot be undone.
        </p>
        <div className="flex justify-end gap-3">
          <Button variant="outline" onClick={onClose} disabled={isDeleting}>Cancel</Button>
          <Button variant="destructive" onClick={onConfirm} disabled={isDeleting}>
            {isDeleting ? 'Deleting…' : 'Delete'}
          </Button>
        </div>
      </div>
    </div>
  )
}
