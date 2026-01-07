import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';

function shouldRedirect(url: string): boolean {
  // Only enforce auth redirects for privileged areas.
  // Participant APIs have their own flows (OIDC or anonymous).
  return url.startsWith('/api/host') || url.startsWith('/api/admin');
}

function buildLoginUrl(): string {
  const returnUrl = `${window.location.pathname}${window.location.search}${window.location.hash}`;
  return `/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`;
}

export const authRedirectInterceptor: HttpInterceptorFn = (req, next) => {
  return next(req).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse) {
        if ((err.status === 401 || err.status === 403) && shouldRedirect(req.url)) {
          window.location.href = buildLoginUrl();
        }
      }

      return throwError(() => err);
    })
  );
};
