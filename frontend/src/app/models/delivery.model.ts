// نماذج البيانات لعمليات التسليم - Delivery data models

export interface DeliveryResponse {
  id: string;
  eventId: string;
  subscriberId: string;
  status: DeliveryStatus;
  attemptNumber: number;
  httpStatusCode?: number;
  errorMessage?: string;
  durationMs: number;
  createdAt: string;
  deliveredAt?: string;
  nextRetryAt?: string;
  
  // معلومات إضافية - Additional info
  eventType: string;
  tenantId: string;
  callbackUrl: string;
}

export interface DeliveryPagedResponse {
  deliveries: DeliveryResponse[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export enum DeliveryStatus {
  Pending = 0,
  Success = 1,
  Failed = 2,
  Retrying = 3,
  DLQ = 4
}

export interface DeliveryQueryRequest {
  eventId?: string;
  subscriberId?: string;
  status?: DeliveryStatus;
  fromDate?: string;
  toDate?: string;
  page?: number;
  pageSize?: number;
}
