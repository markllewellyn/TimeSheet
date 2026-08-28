import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

// Mirrors TimeSheet.Contracts.AppSettingsDto / UpdateAppSettingsRequest (C#) 1:1.
export interface AppSettings {
  baseReportingCurrency: string;
  defaultInvoiceMonthEndDay: number;
  projectBudgetWarningThresholdPercent: number;
  projectBudgetAlertThresholdPercent: number;
}

export type UpdateAppSettingsRequest = AppSettings;

@Injectable({ providedIn: 'root' })
export class SettingsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/settings`;

  get(): Observable<AppSettings> {
    return this.http.get<AppSettings>(this.baseUrl);
  }

  update(request: UpdateAppSettingsRequest): Observable<AppSettings> {
    return this.http.put<AppSettings>(this.baseUrl, request);
  }
}
