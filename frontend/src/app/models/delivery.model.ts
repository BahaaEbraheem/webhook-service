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
  Pending = 'Pending',
  Success = 'Success',
  Failed = 'Failed',
  Retrying = 'Retrying',
  DLQ = 'DLQ'
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
