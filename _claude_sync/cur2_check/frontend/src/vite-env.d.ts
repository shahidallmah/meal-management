/// <reference types="vite/client" />

// App-specific environment variables (see .env.example).
interface ImportMetaEnv {
  readonly VITE_API_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
