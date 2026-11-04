/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Optional absolute API base URL. Empty = same origin (`/api` is proxied). */
  readonly VITE_API_BASE_URL?: string;
}
