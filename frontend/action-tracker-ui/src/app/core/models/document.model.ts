export interface DocumentInfo {
  id: string;
  name: string;
  fileName: string;
  contentType: string;
  fileSize: number;
  relatedEntityType: string;
  relatedEntityId: string;
  uploadedByUserId: string;
  uploadedByName: string;
  createdAt: string;
  /** WO-2026-11 A-4: the caller may delete this file (the delete button shows only then). */
  canDelete?: boolean;
}
