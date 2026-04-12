export interface EmailTemplate {
  id: string;
  templateKey: string;
  name: string;
  subject: string;
  htmlBody: string;
  isActive: boolean;
  description?: string;
  createdAt: string;
  updatedAt: string;
}

export interface EmailTemplateListItem {
  id: string;
  templateKey: string;
  name: string;
  subject: string;
  isActive: boolean;
  description?: string;
}

export interface UpdateEmailTemplate {
  subject: string;
  htmlBody: string;
  isActive: boolean;
}

export interface EmailLog {
  id: string;
  templateKey: string;
  toEmail: string;
  subject: string;
  sentAt: string;
  status: string;
  errorMessage?: string;
  relatedEntityType?: string;
  relatedEntityId?: string;
}
