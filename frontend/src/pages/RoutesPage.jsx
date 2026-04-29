import { useEffect, useState } from 'react'
import useRoutesStore from '../stores/useRoutesStore'
import useAuthStore   from '../stores/useAuthStore'
import { useToast } from '../components/ui/toast'
import RouteTable   from '../components/RouteTable'
import RouteForm    from '../components/RouteForm'
import DeleteDialog from '../components/DeleteDialog'
import { Button }   from '../components/ui/button'

export default function RoutesPage() {
  const { routes, isLoading, error, fetchRoutes, createRoute, updateRoute, deleteRoute } = useRoutesStore()
  const isAdmin = useAuthStore((s) => s.isAdmin())
  const { toast } = useToast()

  const [formOpen,    setFormOpen]    = useState(false)
  const [editRoute,   setEditRoute]   = useState(null)   // null = create mode
  const [deleteTarget, setDeleteTarget] = useState(null)
  const [isDeleting,  setIsDeleting]  = useState(false)

  // AC1: load routes on mount
  useEffect(() => { fetchRoutes() }, [fetchRoutes])

  // ── Handlers ──────────────────────────────────────────────────────────────
  function openCreate() { setEditRoute(null); setFormOpen(true) }
  function openEdit(route) { setEditRoute(route); setFormOpen(true) }

  async function handleFormSubmit(data) {
    try {
      if (editRoute) {
        await updateRoute(editRoute.id, data)   // AC3
        toast({ title: 'Route updated' })
      } else {
        await createRoute(data)                  // AC2
        toast({ title: 'Route created' })
      }
    } catch (err) {
      // AC6: API error → toast
      toast({
        title:       'Error',
        description: err.response?.data?.title ?? err.message ?? 'Something went wrong',
        variant:     'destructive',
      })
      throw err   // re-throw so RouteForm stays open
    }
  }

  async function handleDelete() {
    if (!deleteTarget) return
    setIsDeleting(true)
    try {
      await deleteRoute(deleteTarget.id)         // AC4
      toast({ title: 'Route deleted' })
      setDeleteTarget(null)
    } catch (err) {
      toast({ title: 'Error', description: err.message, variant: 'destructive' })
    } finally {
      setIsDeleting(false)
    }
  }

  // ── Render ────────────────────────────────────────────────────────────────
  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">Routes</h1>
          <p className="text-sm text-muted-foreground">{routes.length} route{routes.length !== 1 ? 's' : ''} configured</p>
        </div>
        {isAdmin && <Button onClick={openCreate}>+ New Route</Button>}
      </div>

      {error && (
        <div className="rounded-md border border-destructive bg-destructive/10 px-4 py-3 text-sm text-destructive">
          {error}
        </div>
      )}

      {isLoading ? (
        <p className="text-sm text-muted-foreground py-8 text-center">Loading routes…</p>
      ) : (
        <RouteTable
          routes={routes}
          onEdit={openEdit}
          onDelete={(route) => setDeleteTarget(route)}
          isAdmin={isAdmin}
        />
      )}

      {/* Create / Edit modal (AC2, AC3, AC5) */}
      <RouteForm
        open={formOpen}
        onClose={() => setFormOpen(false)}
        route={editRoute}
        onSubmit={handleFormSubmit}
      />

      {/* Delete confirmation (AC4) */}
      <DeleteDialog
        open={!!deleteTarget}
        onClose={() => setDeleteTarget(null)}
        route={deleteTarget}
        onConfirm={handleDelete}
        isDeleting={isDeleting}
      />
    </div>
  )
}
