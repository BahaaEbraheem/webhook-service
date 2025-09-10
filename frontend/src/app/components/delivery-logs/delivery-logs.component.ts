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
    pageSize: 10
  };

  jumpToPage: number = 1;

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

  searchDeliveries() {
    this.query.page = 1; // Reset to first page when searching
    this.loadDeliveries();
  }

  onPageSizeChange() {
    this.query.page = 1; // Reset to first page when changing page size
    this.loadDeliveries();
  }

  getStartItem(): number {
    if (!this.response || this.response.totalCount === 0) return 0;
    return ((this.query.page || 1) - 1) * (this.query.pageSize || 10) + 1;
  }

  getEndItem(): number {
    if (!this.response || this.response.totalCount === 0) return 0;
    const start = this.getStartItem();
    const end = start + this.deliveries.length - 1;
    return Math.min(end, this.response.totalCount);
  }

  getVisiblePages(): number[] {
    if (!this.response) return [];

    const currentPage = this.query.page || 1;
    const totalPages = this.response.totalPages;
    const visiblePages: number[] = [];

    // Show max 5 pages around current page
    const maxVisible = 5;
    let start = Math.max(1, currentPage - Math.floor(maxVisible / 2));
    let end = Math.min(totalPages, start + maxVisible - 1);

    // Adjust start if we're near the end
    if (end - start + 1 < maxVisible) {
      start = Math.max(1, end - maxVisible + 1);
    }

    for (let i = start; i <= end; i++) {
      visiblePages.push(i);
    }

    return visiblePages;
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
