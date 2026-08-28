import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface EntryType {
  id: number;
  projectId: number;
  name: string;
  isContractType: boolean;
  isActive: boolean;
}

export interface CreateEntryTypeRequest {
  name: string;
  isContractType: boolean;
}

export interface UpdateEntryTypeRequest {
  name: string;
  isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class EntryTypesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  listByProject(projectId: number, includeInactive = true): Observable<EntryType[]> {
    return this.http.get<EntryType[]>(`${this.baseUrl}/projects/${projectId}/entry-types`, { params: { includeInactive } });
  }

  create(projectId: number, request: CreateEntryTypeRequest): Observable<EntryType> {
    return this.http.post<EntryType>(`${this.baseUrl}/projects/${projectId}/entry-types`, request);
  }

  update(id: number, request: UpdateEntryTypeRequest): Observable<EntryType> {
    return this.http.put<EntryType>(`${this.baseUrl}/entry-types/${id}`, request);
  }
}
