import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ProjectBudget, Contract } from '../models/project.models';

/**
 * Service for managing project budgets and contracts.
 * Provided at root level — auth headers are added automatically by the HTTP interceptor.
 */
@Injectable({ providedIn: 'root' })
export class BudgetService {
  private readonly http = inject(HttpClient);
  private readonly budgetUrl = `${environment.apiUrl}/budgets`;
  private readonly contractUrl = `${environment.apiUrl}/contracts`;

  /**
   * Fetches the budget for a given project.
   * @param projectId The project to fetch the budget for.
   * @returns Observable of the project budget, or null if none exists.
   */
  getByProject(projectId: string): Observable<ProjectBudget | null> {
    return this.http.get<ProjectBudget | null>(`${this.budgetUrl}/project/${projectId}`);
  }

  /**
   * Creates or updates a project budget.
   * @param data Budget payload (include projectId for creation).
   * @returns Observable of the saved project budget.
   */
  createOrUpdate(data: Partial<ProjectBudget>): Observable<ProjectBudget> {
    return this.http.post<ProjectBudget>(this.budgetUrl, data);
  }

  /**
   * Fetches all contracts for a given project.
   * @param projectId The project to fetch contracts for.
   * @returns Observable of contracts.
   */
  getContracts(projectId: string): Observable<Contract[]> {
    return this.http.get<Contract[]>(`${this.contractUrl}/project/${projectId}`);
  }

  /**
   * Creates a new contract.
   * @param data Contract creation payload.
   * @returns Observable of the created contract.
   */
  createContract(data: Partial<Contract>): Observable<Contract> {
    return this.http.post<Contract>(this.contractUrl, data);
  }

  /**
   * Updates an existing contract.
   * @param id Primary key of the contract to update.
   * @param data Contract update payload.
   * @returns Observable of the updated contract.
   */
  updateContract(id: string, data: Partial<Contract>): Observable<Contract> {
    return this.http.put<Contract>(`${this.contractUrl}/${id}`, data);
  }

  /**
   * Deletes a contract.
   * @param id Primary key of the contract to delete.
   */
  deleteContract(id: string): Observable<void> {
    return this.http.delete<void>(`${this.contractUrl}/${id}`);
  }
}
