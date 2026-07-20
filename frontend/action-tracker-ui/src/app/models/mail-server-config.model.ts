export interface MailServerConfig {
  id: string;
  host: string;
  port: number;
  useSsl: boolean;
  fromEmail: string;
  fromName: string;
  username: string;
  hasPassword: boolean;
  isActive: boolean;
  createdAt: string;
  createdBy?: string | null;
  updatedAt?: string | null;
  updatedBy?: string | null;
}

export interface UpdateMailServerConfig {
  host: string;
  port: number;
  useSsl: boolean;
  fromEmail: string;
  fromName: string;
  username: string;
  /** Null/empty means keep the existing stored password. */
  password?: string | null;
}
