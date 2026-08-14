import type { Session } from './types'

const apiBase = import.meta.env.VITE_API_BASE_URL ?? ''

export class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message)
  }
}

export async function api<T>(path: string, session?: Session, options: RequestInit = {}): Promise<T> {
  const response = await fetch(`${apiBase}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(session ? { Authorization: `Bearer ${session.accessToken}` } : {}),
      ...options.headers,
    },
  })

  if (!response.ok) {
    let message = `Request failed (${response.status})`
    try {
      const problem = await response.json() as { detail?: string; title?: string; errors?: Record<string, string[]> }
      message = problem.detail ?? problem.title ?? Object.values(problem.errors ?? {}).flat().join(' ') ?? message
    } catch { /* use status fallback */ }
    throw new ApiError(response.status, message)
  }

  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}
