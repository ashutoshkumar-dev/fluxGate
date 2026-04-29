import { create } from 'zustand'
import { routesService } from '../services/routesService'

const useRoutesStore = create((set, get) => ({
  routes:    [],
  isLoading: false,
  error:     null,

  // ── Fetch ───────────────────────────────────────────────────────────────
  fetchRoutes: async () => {
    set({ isLoading: true, error: null })
    try {
      const routes = await routesService.getRoutes()
      set({ routes, isLoading: false })
    } catch (err) {
      set({ isLoading: false, error: err.message ?? 'Failed to load routes' })
    }
  },

  // ── Create (optimistic) ─────────────────────────────────────────────────
  createRoute: async (data) => {
    const tempId = `temp-${Date.now()}`
    const optimistic = { ...data, id: tempId, isActive: true, createdAt: new Date().toISOString(), updatedAt: new Date().toISOString() }
    set((s) => ({ routes: [...s.routes, optimistic] }))
    try {
      const created = await routesService.createRoute(data)
      set((s) => ({ routes: s.routes.map((r) => (r.id === tempId ? created : r)) }))
      return created
    } catch (err) {
      set((s) => ({ routes: s.routes.filter((r) => r.id !== tempId) }))
      throw err
    }
  },

  // ── Update (optimistic) ─────────────────────────────────────────────────
  updateRoute: async (id, data) => {
    const previous = get().routes.find((r) => r.id === id)
    set((s) => ({ routes: s.routes.map((r) => (r.id === id ? { ...r, ...data } : r)) }))
    try {
      const updated = await routesService.updateRoute(id, data)
      set((s) => ({ routes: s.routes.map((r) => (r.id === id ? updated : r)) }))
      return updated
    } catch (err) {
      if (previous) set((s) => ({ routes: s.routes.map((r) => (r.id === id ? previous : r)) }))
      throw err
    }
  },

  // ── Delete (optimistic) ─────────────────────────────────────────────────
  deleteRoute: async (id) => {
    const previous = get().routes.find((r) => r.id === id)
    set((s) => ({ routes: s.routes.filter((r) => r.id !== id) }))
    try {
      await routesService.deleteRoute(id)
    } catch (err) {
      if (previous) set((s) => ({ routes: [...s.routes, previous] }))
      throw err
    }
  },
}))

export default useRoutesStore
