import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { DeliveryPagedResponse, DeliveryQueryRequest } from '../models/delivery.model';

@Injectable({
  providedIn: 'root'
})
export class WebhookService {
  private readonly apiUrl = '/api';

  constructor(private http: HttpClient) {}

  /**
   * البحث في عمليات التسليم
   * Search deliveries
   */
  getDeliveries(query: DeliveryQueryRequest): Observable<DeliveryPagedResponse> {
    let params = new HttpParams();
    
    if (query.eventId) params = params.set('eventId', query.eventId);
    if (query.subscriberId) params = params.set('subscriberId', query.subscriberId);
    if (query.status) params = params.set('status', query.status);
    if (query.fromDate) params = params.set('fromDate', query.fromDate);
    if (query.toDate) params = params.set('toDate', query.toDate);
    if (query.page) params = params.set('page', query.page.toString());
    if (query.pageSize) params = params.set('pageSize', query.pageSize.toString());

    return this.http.get<DeliveryPagedResponse>(`${this.apiUrl}/deliveries`, { params });
  }

  /**
   * فحص صحة النظام
   * Health check
   */
  getHealth(): Observable<any> {
    return this.http.get('/health');
  }
}
