import { HttpInterceptorFn } from '@angular/common/http';

export const correlationInterceptor: HttpInterceptorFn = (req, next) => {
  const correlationId = sessionStorage.getItem('correlationId') || generateCorrelationId();
  sessionStorage.setItem('correlationId', correlationId);
  
  const cloned = req.clone({
    setHeaders: {
      'X-Correlation-ID': correlationId
    }
  });
  
  return next(cloned);
};

function generateCorrelationId(): string {
  return `${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
}
