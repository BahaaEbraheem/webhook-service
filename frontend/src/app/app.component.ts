import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet, RouterModule } from '@angular/router';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterModule],
  template: `
    <div class="app-container">
      <header class="header">
        <div class="container">
          <h1>
            <span class="rtl">خدمة الويب هوك</span>
            <span class="ltr">Webhook Service</span>
          </h1>
          <nav>
            <a routerLink="/deliveries" routerLinkActive="active">
              <span class="rtl">سجل التسليمات</span>
              <span class="ltr">Delivery Logs</span>
            </a>
          </nav>
        </div>
      </header>
      
      <main class="main-content">
        <div class="container">
          <router-outlet></router-outlet>
        </div>
      </main>
      
      <footer class="footer">
        <div class="container">
          <p class="rtl">© 2024 خدمة الويب هوك - جميع الحقوق محفوظة</p>
          <p class="ltr">© 2024 Webhook Service - All rights reserved</p>
        </div>
      </footer>
    </div>
  `,
  styles: [`
    .app-container {
      min-height: 100vh;
      display: flex;
      flex-direction: column;
    }
    
    .header {
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      color: white;
      padding: 1rem 0;
      box-shadow: 0 2px 4px rgba(0,0,0,0.1);
    }
    
    .container {
      max-width: 1200px;
      margin: 0 auto;
      padding: 0 1rem;
    }
    
    .header h1 {
      margin: 0;
      font-size: 1.8rem;
      font-weight: 600;
    }
    
    .header nav {
      margin-top: 1rem;
    }
    
    .header nav a {
      color: white;
      text-decoration: none;
      padding: 0.5rem 1rem;
      border-radius: 4px;
      transition: background-color 0.3s;
    }
    
    .header nav a:hover,
    .header nav a.active {
      background-color: rgba(255,255,255,0.2);
    }
    
    .main-content {
      flex: 1;
      padding: 2rem 0;
    }
    
    .footer {
      background-color: #2c3e50;
      color: white;
      padding: 1rem 0;
      text-align: center;
    }
    
    .footer p {
      margin: 0.25rem 0;
      font-size: 0.9rem;
    }
    
    .rtl {
      direction: rtl;
      text-align: right;
    }
    
    .ltr {
      direction: ltr;
      text-align: left;
    }
  `]
})
export class AppComponent {
  title = 'webhook-service-frontend';
}
