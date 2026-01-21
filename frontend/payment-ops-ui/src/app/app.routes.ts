import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    redirectTo: '/chat',
    pathMatch: 'full'
  },
  {
    path: 'chat',
    loadComponent: () => import('./chat/chat.component').then(m => m.ChatComponent)
  },
  {
    path: 'ingest',
    loadComponent: () => import('./ingest/ingest.component').then(m => m.IngestComponent)
  },
  {
    path: 'sources',
    loadComponent: () => import('./sources/sources.component').then(m => m.SourcesComponent)
  }
];
