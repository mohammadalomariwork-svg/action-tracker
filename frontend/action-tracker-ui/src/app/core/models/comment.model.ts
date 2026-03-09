export interface CommentInfo {
  id: string;
  relatedEntityType: string;
  relatedEntityId: string;
  content: string;
  authorUserId: string;
  authorName: string;
  isHighImportance: boolean;
  createdAt: string;
  updatedAt: string | null;
}
