import { useState } from 'react'
import { Button } from './ui/button'
import { cn } from '../lib/utils'

// Columns: path, method, destination, authRequired, isActive, actions
// Sortable by path and method.

const METHODS = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'ANY']

function MethodBadge({ method }) {
  const colors = {
    GET:    'bg-blue-100 text-blue-700',
    POST:   'bg-green-100 text-green-700',
    PUT:    'bg-yellow-100 text-yellow-700',
    PATCH:  'bg-orange-100 text-orange-700',
    DELETE: 'bg-red-100 text-red-700',
    ANY:    'bg-purple-100 text-purple-700',
  }
  return (
    <span className={cn('px-2 py-0.5 rounded text-xs font-mono font-semibold', colors[method] ?? 'bg-muted text-muted-foreground')}>
      {method}
    </span>
  )
}

function ActiveBadge({ isActive }) {
  return (
    <span className={cn('px-2 py-0.5 rounded-full text-xs font-medium', isActive ? 'bg-green-100 text-green-700' : 'bg-muted text-muted-foreground')}>
      {isActive ? 'Active' : 'Inactive'}
    </span>
  )
}

export default function RouteTable({ routes, onEdit, onDelete, isAdmin = false }) {
  const [sortKey,  setSortKey]  = useState('path')
  const [sortDir,  setSortDir]  = useState('asc')

  function toggleSort(key) {
    if (sortKey === key) setSortDir((d) => (d === 'asc' ? 'desc' : 'asc'))
    else { setSortKey(key); setSortDir('asc') }
  }

  const sorted = [...routes].sort((a, b) => {
    const av = (a[sortKey] ?? '').toString().toLowerCase()
    const bv = (b[sortKey] ?? '').toString().toLowerCase()
    return sortDir === 'asc' ? av.localeCompare(bv) : bv.localeCompare(av)
  })

  function SortIcon({ col }) {
    if (sortKey !== col) return <span className="ml-1 opacity-30">↕</span>
    return <span className="ml-1">{sortDir === 'asc' ? '↑' : '↓'}</span>
  }

  if (routes.length === 0) {
    return <p className="text-sm text-muted-foreground py-8 text-center">No routes configured.</p>
  }

  return (
    <div className="overflow-x-auto rounded-md border">
      <table className="w-full text-sm">
        <thead className="bg-muted/50">
          <tr>
            <th
              className="text-left px-4 py-3 font-medium cursor-pointer hover:text-foreground select-none"
              onClick={() => toggleSort('path')}
            >
              Path <SortIcon col="path" />
            </th>
            <th
              className="text-left px-4 py-3 font-medium cursor-pointer hover:text-foreground select-none"
              onClick={() => toggleSort('method')}
            >
              Method <SortIcon col="method" />
            </th>
            <th className="text-left px-4 py-3 font-medium">Destination</th>
            <th className="text-left px-4 py-3 font-medium">Auth</th>
            <th className="text-left px-4 py-3 font-medium">Status</th>
            <th className="text-right px-4 py-3 font-medium">Actions</th>
          </tr>
        </thead>
        <tbody className="divide-y">
          {sorted.map((route) => (
            <tr key={route.id} className="hover:bg-muted/30 transition-colors">
              <td className="px-4 py-3 font-mono text-xs">{route.path}</td>
              <td className="px-4 py-3"><MethodBadge method={route.method} /></td>
              <td className="px-4 py-3 text-muted-foreground truncate max-w-[200px]">{route.destination}</td>
              <td className="px-4 py-3">{route.authRequired ? 'Required' : '—'}</td>
              <td className="px-4 py-3"><ActiveBadge isActive={route.isActive} /></td>
              <td className="px-4 py-3 text-right space-x-2">
                {isAdmin && (
                  <>
                    <Button variant="outline" size="sm" onClick={() => onEdit(route)}>Edit</Button>
                    <Button variant="destructive" size="sm" onClick={() => onDelete(route)}>Delete</Button>
                  </>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
