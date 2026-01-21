import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ApiService, Source, SourceDetail } from '../services/api.service';
import { firstValueFrom } from 'rxjs';

@Component({
  selector: 'app-sources',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './sources.component.html',
  styleUrls: ['./sources.component.css']
})
export class SourcesComponent implements OnInit {
  sources: Source[] = [];
  loading = false;
  error: string | null = null;
  selectedSource: SourceDetail | null = null;
  loadingDetail = false;

  constructor(private apiService: ApiService) {}

  ngOnInit() {
    this.loadSources();
  }

  async loadSources() {
    this.loading = true;
    this.error = null;

    try {
      this.sources = await firstValueFrom(this.apiService.getSources());
    } catch (err: any) {
      this.error = err.message || 'Failed to load sources';
    } finally {
      this.loading = false;
    }
  }

  async viewSource(source: Source) {
    this.loadingDetail = true;
    this.selectedSource = null;

    try {
      this.selectedSource = await firstValueFrom(this.apiService.getSource(source.documentId));
    } catch (err: any) {
      this.error = err.message || 'Failed to load source details';
    } finally {
      this.loadingDetail = false;
    }
  }

  formatBytes(bytes: number): string {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i];
  }
}
