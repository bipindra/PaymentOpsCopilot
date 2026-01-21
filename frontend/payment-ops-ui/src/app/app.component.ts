import { Component } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <header>
      <div class="container">
        <h1>Payment Ops Console</h1>
        <nav>
          <a routerLink="/chat" routerLinkActive="active">Chat</a>
          <a routerLink="/ingest" routerLinkActive="active">Ingest</a>
          <a routerLink="/sources" routerLinkActive="active">Sources</a>
        </nav>
      </div>
    </header>
    <div class="container">
      <router-outlet></router-outlet>
    </div>
  `,
  styles: []
})
export class AppComponent {
  title = 'payment-ops-ui';
}
