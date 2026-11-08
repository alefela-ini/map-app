/**
 * REST clients for the two resources. Both expose the same shape so they can be used
 * with the generic {@link useEditableCollection} hook.
 */
import type { CreateMapObjectRequest, CreatePolygonRequest, MapObjectDto, PolygonDto } from '../types';
import { request } from './http';

export interface CollectionApi<TDto, TRequest> {
  list(signal?: AbortSignal): Promise<TDto[]>;
  /** Saves new items and returns the saved items (this batch only, in request order). */
  create(items: TRequest[]): Promise<TDto[]>;
  remove(id: string): Promise<void>;
  removeAll(): Promise<void>;
}

function createCollectionApi<TDto, TRequest>(basePath: string): CollectionApi<TDto, TRequest> {
  return {
    list: (signal) => request<TDto[]>(basePath, { signal }),
    create: (items) => request<TDto[]>(basePath, { method: 'POST', body: JSON.stringify(items) }),
    remove: (id) => request<void>(`${basePath}/${encodeURIComponent(id)}`, { method: 'DELETE' }),
    removeAll: () => request<void>(basePath, { method: 'DELETE' }),
  };
}

export const polygonsApi = createCollectionApi<PolygonDto, CreatePolygonRequest>('/api/polygons');
export const objectsApi = createCollectionApi<MapObjectDto, CreateMapObjectRequest>('/api/objects');
