import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface AskRequest {
  question: string;
  topK?: number;
}

export interface AskResponse {
  answerMarkdown: string;
  citations: Citation[];
  retrieved: Citation[];
  elapsedMs: number;
  tokensUsed?: number;
}

export interface Citation {
  documentName: string;
  chunkIndex: number;
  snippet: string;
  score?: number;
}

export interface Source {
  documentId: string;
  docName: string;
  sourcePath: string;
  chunkCount: number;
  totalSizeBytes: number;
  createdUtc: string;
}

export interface SourceDetail extends Source {
  chunks: Array<{
    chunkIndex: number;
    snippet: string;
    textLength: number;
  }>;
}

export interface IngestResponse {
  documentId: string;
  docName: string;
  chunkCount: number;
  createdUtc: string;
}

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  ask(request: AskRequest): Observable<AskResponse> {
    return this.http.post<AskResponse>(`${this.apiUrl}/api/ask`, request);
  }

  ingestText(docName: string, text: string): Observable<IngestResponse> {
    return this.http.post<IngestResponse>(`${this.apiUrl}/api/ingest/text`, {
      docName,
      text
    });
  }

  ingestFiles(files: FileList): Observable<{ results: IngestResponse[] }> {
    const formData = new FormData();
    for (let i = 0; i < files.length; i++) {
      formData.append('files', files[i]);
    }
    return this.http.post<{ results: IngestResponse[] }>(`${this.apiUrl}/api/ingest/files`, formData);
  }

  ingestSamples(folderPath?: string): Observable<{ ingested: number; documents: IngestResponse[] }> {
    return this.http.post<{ ingested: number; documents: IngestResponse[] }>(
      `${this.apiUrl}/api/ingest/samples`,
      { folderPath: folderPath || 'samples/runbooks' }
    );
  }

  getSources(): Observable<Source[]> {
    return this.http.get<Source[]>(`${this.apiUrl}/api/sources`);
  }

  getSource(documentId: string): Observable<SourceDetail> {
    return this.http.get<SourceDetail>(`${this.apiUrl}/api/sources/${documentId}`);
  }
}
