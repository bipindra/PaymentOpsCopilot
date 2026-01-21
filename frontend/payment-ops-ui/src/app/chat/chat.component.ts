import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService, AskResponse, Citation } from '../services/api.service';
import { firstValueFrom } from 'rxjs';
import { marked } from 'marked';

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './chat.component.html',
  styleUrls: ['./chat.component.css']
})
export class ChatComponent {
  question = '';
  loading = false;
  response: AskResponse | null = null;
  error: string | null = null;
  expandedCitations = new Set<string>();

  constructor(private apiService: ApiService) {}

  async ask() {
    if (!this.question.trim() || this.loading) return;

    this.loading = true;
    this.error = null;
    this.response = null;

    try {
      this.response = await firstValueFrom(this.apiService.ask({
        question: this.question,
        topK: 5
      }));
      
      // Check if the response contains an error message
      if (this.response?.answerMarkdown?.includes('An error occurred while processing')) {
        this.error = this.response.answerMarkdown;
        this.response = null;
      }
    } catch (err: any) {
      this.error = err.error?.error || err.message || 'Failed to get answer';
      this.response = null;
    } finally {
      this.loading = false;
    }
  }

  getRenderedAnswer(): string {
    if (!this.response?.answerMarkdown) return '';
    const result = marked.parse(this.response.answerMarkdown);
    return typeof result === 'string' ? result : '';
  }

  onEnterKey(event: Event) {
    const keyboardEvent = event as KeyboardEvent;
    if (!keyboardEvent.shiftKey) {
      keyboardEvent.preventDefault();
      this.ask();
    }
  }

  toggleCitation(citation: Citation) {
    const key = `${citation.documentName}:${citation.chunkIndex}`;
    if (this.expandedCitations.has(key)) {
      this.expandedCitations.delete(key);
    } else {
      this.expandedCitations.add(key);
    }
  }

  isExpanded(citation: Citation): boolean {
    const key = `${citation.documentName}:${citation.chunkIndex}`;
    return this.expandedCitations.has(key);
  }
}
