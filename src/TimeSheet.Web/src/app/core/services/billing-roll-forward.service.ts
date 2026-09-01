import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface BillingRollForwardResult {
  clientsDue: number;
  invoicesGenerated: number;
  failed: number;
}

@Injectable({ providedIn: 'root' })
export class BillingRollForwardService {
  private readonly http = inject(HttpClient);

  runNow(): Observable<BillingRollForwardResult> {
    return this.http.post<BillingRollForwardResult>(`${environment.apiBaseUrl}/billing-roll-forward/run-now`, {});
  }
}
