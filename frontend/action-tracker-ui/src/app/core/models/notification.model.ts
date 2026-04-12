export interface AppNotification {
  id: string;
  title: string;
  message: string;
  type: string;
  actionType: string;
  relatedEntityType?: string;
  relatedEntityId?: string;
  relatedEntityCode?: string;
  url?: string;
  isRead: boolean;
  readAt?: string;
  createdAt: string;
  createdByDisplayName?: string;
}

export interface NotificationSummary {
  unreadCount: number;
  latestNotifications: AppNotification[];
}
