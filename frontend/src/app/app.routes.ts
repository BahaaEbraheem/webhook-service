import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    redirectTo: '/deliveries',
    pathMatch: 'full'
  },
  {
    path: 'deliveries',
    loadComponent: () => import('./components/delivery-logs/delivery-logs.component').then(m => m.DeliveryLogsComponent)
  }
];
