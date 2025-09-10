import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { WebhookService } from '../../services/webhook.service';
import { DeliveryResponse, DeliveryPagedResponse, DeliveryStatus, DeliveryQueryRequest } from '../../models/delivery.model';

@Component({
  selector: 'app-delivery-logs',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="delivery-logs">
      <div class="header-section">
        <h2>
          <span class="rtl">سجل عمليات التسليم</span>
          <span class="ltr">Delivery Logs</span>
        </h2>
        
        <!-- فلاتر البحث - Search Filters -->
        <div class="filters">
          <div class="filter-row">
            <div class="filter-group">
              <label class="rtl">الحالة:</label>
              <label class="ltr">Status:</label>
              <select [(ngModel)]="query.status" (change)="loadDeliveries()">
                <option value="">الكل - All</option>
                <option value="Pending">في الانتظار - Pending</option>
                <option value="Success">نجح - Success</option>
                <option value="Failed">فشل - Failed</option>
                <option value="Retrying">إعادة محاولة - Retrying</option>
                <option value="DLQ">قائمة الانتظار الميتة - DLQ</option>
              </select>
            </div>
            
            <div class="filter-group">
              <label class="rtl">من تاريخ:</label>
              <label class="ltr">From Date:</label>
              <input type="datetime-local" [(ngModel)]="query.fromDate" (change)="loadDeliveries()">
            </div>
            
            <div class="filter-group">
              <label class="rtl">إلى تاريخ:</label>
              <label class="ltr">To Date:</label>
              <input type="datetime-local" [(ngModel)]="query.toDate" (change)="loadDeliveries()">
            </div>
            
            <button class="refresh-btn" (click)="loadDeliveries()">
              <span class="rtl">تحديث</span>
              <span class="ltr">Refresh</span>
            </button>
          </div>
        </div>
      </div>

      <!-- حالة التحميل - Loading State -->
      <div *ngIf="loading" class="loading">
        <span class="rtl">جاري التحميل...</span>
        <span class="ltr">Loading...</span>
      </div>

      <!-- رسالة الخطأ - Error Message -->
      <div *ngIf="error" class="error">
        <span class="rtl">خطأ في تحميل البيانات:</span>
        <span class="ltr">Error loading data:</span>
        {{ error }}
      </div>

      <!-- جدول النتائج - Results Table -->
      <div *ngIf="!loading && !error" class="table-container">
        <table class="delivery-table">
          <thead>
            <tr>
              <th class="rtl">الحالة</th>
              <th class="ltr">Status</th>
              <th class="rtl">نوع الحدث</th>
              <th class="ltr">Event Type</th>
              <th class="rtl">URL الاستدعاء</th>
              <th class="ltr">Callback URL</th>
              <th class="rtl">المحاولة</th>
              <th class="ltr">Attempt</th>
              <th class="rtl">كود HTTP</th>
              <th class="ltr">HTTP Code</th>
              <th class="rtl">المدة (ms)</th>
              <th class="ltr">Duration (ms)</th>
              <th class="rtl">تاريخ الإنشاء</th>
              <th class="ltr">Created At</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let delivery of deliveries" [class]="'status-' + delivery.status.toLowerCase()">
              <td>
                <span class="status-badge" [class]="'status-' + delivery.status.toLowerCase()">
                  {{ getStatusText(delivery.status) }}
                </span>
              </td>
              <td>{{ delivery.eventType }}</td>
              <td class="url-cell" [title]="delivery.callbackUrl">
                {{ truncateUrl(delivery.callbackUrl) }}
              </td>
              <td>{{ delivery.attemptNumber }}</td>
              <td>
                <span *ngIf="delivery.httpStatusCode" 
                      [class]="getHttpStatusClass(delivery.httpStatusCode)">
                  {{ delivery.httpStatusCode }}
                </span>
                <span *ngIf="!delivery.httpStatusCode">-</span>
              </td>
              <td>{{ delivery.durationMs }}</td>
              <td>{{ formatDate(delivery.createdAt) }}</td>
            </tr>
          </tbody>
        </table>

        <!-- رسالة عدم وجود بيانات - No Data Message -->
        <div *ngIf="deliveries.length === 0" class="no-data">
          <span class="rtl">لا توجد عمليات تسليم</span>
          <span class="ltr">No deliveries found</span>
        </div>
      </div>

      <!-- التنقل بين الصفحات - Pagination -->
      <div *ngIf="response && response.totalPages > 1" class="pagination">
        <button [disabled]="query.page === 1" (click)="goToPage(query.page! - 1)">
          <span class="rtl">السابق</span>
          <span class="ltr">Previous</span>
        </button>
        
        <span class="page-info">
          <span class="rtl">صفحة {{ query.page }} من {{ response.totalPages }}</span>
          <span class="ltr">Page {{ query.page }} of {{ response.totalPages }}</span>
        </span>
        
        <button [disabled]="query.page === response.totalPages" (click)="goToPage(query.page! + 1)">
          <span class="rtl">التالي</span>
          <span class="ltr">Next</span>
        </button>
      </div>

      <!-- إحصائيات - Statistics -->
      <div *ngIf="response" class="stats">
        <span class="rtl">إجمالي النتائج: {{ response.totalCount }}</span>
        <span class="ltr">Total Results: {{ response.totalCount }}</span>
      </div>
    </div>
  `,
  styles: [`
    .delivery-logs {
      padding: 1rem;
    }

    .header-section h2 {
      color: #2c3e50;
      margin-bottom: 1.5rem;
      border-bottom: 2px solid #3498db;
      padding-bottom: 0.5rem;
    }

    .filters {
      background: white;
      padding: 1.5rem;
      border-radius: 8px;
      box-shadow: 0 2px 4px rgba(0,0,0,0.1);
      margin-bottom: 1.5rem;
    }

    .filter-row {
      display: flex;
      gap: 1rem;
      align-items: end;
      flex-wrap: wrap;
    }

    .filter-group {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }

    .filter-group label {
      font-weight: 600;
      color: #2c3e50;
      font-size: 0.9rem;
    }

    .filter-group select,
    .filter-group input {
      padding: 0.5rem;
      border: 1px solid #ddd;
      border-radius: 4px;
      font-size: 0.9rem;
    }

    .refresh-btn {
      background: #3498db;
      color: white;
      border: none;
      padding: 0.5rem 1rem;
      border-radius: 4px;
      cursor: pointer;
      font-size: 0.9rem;
      transition: background-color 0.3s;
    }

    .refresh-btn:hover {
      background: #2980b9;
    }

    .loading, .error {
      text-align: center;
      padding: 2rem;
      font-size: 1.1rem;
    }

    .error {
      color: #e74c3c;
      background: #fdf2f2;
      border: 1px solid #f5c6cb;
      border-radius: 4px;
    }

    .table-container {
      background: white;
      border-radius: 8px;
      overflow: hidden;
      box-shadow: 0 2px 4px rgba(0,0,0,0.1);
    }

    .delivery-table {
      width: 100%;
      border-collapse: collapse;
    }

    .delivery-table th {
      background: #f8f9fa;
      padding: 1rem;
      text-align: left;
      font-weight: 600;
      color: #2c3e50;
      border-bottom: 2px solid #dee2e6;
    }

    .delivery-table td {
      padding: 0.75rem 1rem;
      border-bottom: 1px solid #dee2e6;
    }

    .delivery-table tr:hover {
      background: #f8f9fa;
    }

    .status-badge {
      padding: 0.25rem 0.5rem;
      border-radius: 12px;
      font-size: 0.8rem;
      font-weight: 600;
      text-transform: uppercase;
    }

    .status-success {
      background: #d4edda;
      color: #155724;
    }

    .status-failed {
      background: #f8d7da;
      color: #721c24;
    }

    .status-pending {
      background: #fff3cd;
      color: #856404;
    }

    .status-retrying {
      background: #cce5ff;
      color: #004085;
    }

    .status-dlq {
      background: #e2e3e5;
      color: #383d41;
    }

    .url-cell {
      max-width: 200px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .http-success {
      color: #28a745;
      font-weight: 600;
    }

    .http-error {
      color: #dc3545;
      font-weight: 600;
    }

    .http-redirect {
      color: #ffc107;
      font-weight: 600;
    }

    .pagination {
      display: flex;
      justify-content: center;
      align-items: center;
      gap: 1rem;
      margin: 1.5rem 0;
    }

    .pagination button {
      background: #3498db;
      color: white;
      border: none;
      padding: 0.5rem 1rem;
      border-radius: 4px;
      cursor: pointer;
      transition: background-color 0.3s;
    }

    .pagination button:hover:not(:disabled) {
      background: #2980b9;
    }

    .pagination button:disabled {
      background: #bdc3c7;
      cursor: not-allowed;
    }

    .page-info {
      font-weight: 600;
      color: #2c3e50;
    }

    .stats {
      text-align: center;
      margin-top: 1rem;
      color: #7f8c8d;
      font-size: 0.9rem;
    }

    .no-data {
      text-align: center;
      padding: 3rem;
      color: #7f8c8d;
      font-size: 1.1rem;
    }

    .rtl {
      direction: rtl;
      text-align: right;
    }

    .ltr {
      direction: ltr;
      text-align: left;
    }

    @media (max-width: 768px) {
      .filter-row {
        flex-direction: column;
        align-items: stretch;
      }
      
      .delivery-table {
        font-size: 0.8rem;
      }
      
      .delivery-table th,
      .delivery-table td {
        padding: 0.5rem;
      }
    }
  `]
})
export class DeliveryLogsComponent implements OnInit {
  deliveries: DeliveryResponse[] = [];
  response: DeliveryPagedResponse | null = null;
  loading = false;
  error: string | null = null;
  
  query: DeliveryQueryRequest = {
    page: 1,
    pageSize: 20
  };

  constructor(private webhookService: WebhookService) {}

  ngOnInit() {
    this.loadDeliveries();
  }

  loadDeliveries() {
    this.loading = true;
    this.error = null;
    
    this.webhookService.getDeliveries(this.query).subscribe({
      next: (response) => {
        this.response = response;
        this.deliveries = response.deliveries;
        this.loading = false;
      },
      error: (error) => {
        this.error = error.message || 'حدث خطأ في تحميل البيانات - Error loading data';
        this.loading = false;
        console.error('Error loading deliveries:', error);
      }
    });
  }

  goToPage(page: number) {
    this.query.page = page;
    this.loadDeliveries();
  }

  getStatusText(status: DeliveryStatus): string {
    const statusMap = {
      [DeliveryStatus.Pending]: 'في الانتظار - Pending',
      [DeliveryStatus.Success]: 'نجح - Success', 
      [DeliveryStatus.Failed]: 'فشل - Failed',
      [DeliveryStatus.Retrying]: 'إعادة محاولة - Retrying',
      [DeliveryStatus.DLQ]: 'قائمة ميتة - DLQ'
    };
    return statusMap[status] || status;
  }

  getHttpStatusClass(statusCode: number): string {
    if (statusCode >= 200 && statusCode < 300) return 'http-success';
    if (statusCode >= 300 && statusCode < 400) return 'http-redirect';
    return 'http-error';
  }

  truncateUrl(url: string): string {
    return url.length > 40 ? url.substring(0, 40) + '...' : url;
  }

  formatDate(dateString: string): string {
    const date = new Date(dateString);
    return date.toLocaleString('ar-EG', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit'
    });
  }
}
