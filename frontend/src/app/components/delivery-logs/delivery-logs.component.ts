import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { WebhookService } from '../../services/webhook.service';
import { DeliveryResponse, DeliveryPagedResponse, DeliveryStatus, DeliveryQueryRequest } from '../../models/delivery.model';

@Component({
  selector: 'app-delivery-logs',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './delivery-logs.component.html',
  styleUrls: ['./delivery-logs.component.css']
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
    return statusMap[status] || status.toString();
  }

  getStatusClass(status: DeliveryStatus): string {
    const statusClassMap = {
      [DeliveryStatus.Pending]: 'warning',
      [DeliveryStatus.Success]: 'success',
      [DeliveryStatus.Failed]: 'danger',
      [DeliveryStatus.Retrying]: 'info',
      [DeliveryStatus.DLQ]: 'secondary'
    };
    return statusClassMap[status] || 'secondary';
  }

  getStatusIcon(status: DeliveryStatus): string {
    const statusIconMap = {
      [DeliveryStatus.Pending]: 'fas fa-clock',
      [DeliveryStatus.Success]: 'fas fa-check-circle',
      [DeliveryStatus.Failed]: 'fas fa-times-circle',
      [DeliveryStatus.Retrying]: 'fas fa-redo',
      [DeliveryStatus.DLQ]: 'fas fa-skull-crossbones'
    };
    return statusIconMap[status] || 'fas fa-question-circle';
  }

  getHttpStatusClass(statusCode: number): string {
    if (statusCode >= 200 && statusCode < 300) return 'success';
    if (statusCode >= 300 && statusCode < 400) return 'warning';
    return 'danger';
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
