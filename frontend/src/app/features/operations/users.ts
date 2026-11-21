import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { Role, UserProfile } from '../../core/models';
import { ToastService } from '../../shared/toast.service';
import { apiError } from '../../shared/errors';
const roles: Role[] = ['ADMINISTRATOR', 'MANAGER', 'OPERATOR', 'DRIVER', 'CUSTOMER'];
@Component({
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div>
    <h1 class="page-title">User access</h1>
    <p class="page-subtitle">Administrator-only account roles and disabled state.</p>
    <div class="table-wrap mt-6">
      <table class="data-table">
        <thead>
          <tr>
            <th>User</th>
            <th>Role</th>
            <th>State</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          @for (u of users(); track u.id) {
            <tr>
              <td>
                <b>{{ u.firstName }} {{ u.lastName }}</b
                ><small class="block text-slate-400">{{ u.email }}</small>
              </td>
              <td>
                <select class="input min-w-48" [(ngModel)]="u.roles[0]">
                  <option value="ADMINISTRATOR">Administrator</option>
                  <option value="MANAGER">Manager</option>
                  <option value="OPERATOR">Operator</option>
                  <option value="DRIVER">Driver</option>
                  <option value="CUSTOMER">Customer</option>
                </select>
              </td>
              <td>
                <label class="flex items-center gap-2"
                  ><input type="checkbox" [(ngModel)]="u.isDisabled" /> Disabled</label
                >
              </td>
              <td><button class="btn-secondary" (click)="save(u)">Save</button></td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  </div>`,
})
export class UsersPage {
  private api = inject(ApiService);
  private toast = inject(ToastService);
  users = signal<UserProfile[]>([]);
  constructor() {
    this.api.get<UserProfile[]>('/api/users', { page: 1 }).subscribe((x) => this.users.set(x));
  }
  save(u: UserProfile) {
    const role = roles.indexOf(u.roles[0]);
    this.api
      .put(`/api/users/${u.id}/access`, { role, isDisabled: u.isDisabled })
      .subscribe({
        next: () => this.toast.show('User access updated'),
        error: (e) => this.toast.show(apiError(e), true),
      });
  }
}
