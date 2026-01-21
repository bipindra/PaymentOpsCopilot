import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ApiService, IngestResponse } from '../services/api.service';
import { firstValueFrom } from 'rxjs';

@Component({
  selector: 'app-ingest',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './ingest.component.html',
  styleUrls: ['./ingest.component.css']
})
export class IngestComponent {
  loading = false;
  error: string | null = null;
  success: string | null = null;
  results: IngestResponse[] = [];

  constructor(private apiService: ApiService) {}

  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.uploadFiles(input.files);
    }
  }

  async uploadFiles(files: FileList) {
    this.loading = true;
    this.error = null;
    this.success = null;
    this.results = [];

    try {
      const response = await firstValueFrom(this.apiService.ingestFiles(files));
      if (response?.results) {
        this.results = response.results;
        this.success = `Successfully ingested ${this.results.length} file(s)`;
      }
    } catch (err: any) {
      this.error = err.message || 'Failed to ingest files';
    } finally {
      this.loading = false;
    }
  }

  async ingestSamples() {
    this.loading = true;
    this.error = null;
    this.success = null;
    this.results = [];

    try {
      const response = await firstValueFrom(this.apiService.ingestSamples());
      if (response?.documents) {
        this.results = response.documents;
        this.success = `Successfully ingested ${response.ingested} sample document(s)`;
      }
    } catch (err: any) {
      this.error = err.message || 'Failed to ingest samples';
    } finally {
      this.loading = false;
    }
  }
}
