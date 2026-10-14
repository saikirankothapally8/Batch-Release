import { inject } from '@angular/core';
import { CanActivateFn, Router, Routes } from '@angular/router';
import { AuthService } from './core/auth.service';
import { P } from './core/models';
import { ShellComponent } from './shell.component';
import { LoginComponent } from './pages/login.component';

const authed: CanActivateFn = () => inject(AuthService).isLoggedIn() || inject(Router).createUrlTree(['/login']);
const can = (...perms: string[]): CanActivateFn => () => inject(AuthService).has(...perms) || inject(Router).createUrlTree(['/']);
const home: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.has(P.queue)) return router.createUrlTree(['/workbench']);
  if (auth.has(P.dashboard)) return router.createUrlTree(['/dashboard']);
  return router.createUrlTree(['/batches']);
};

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  {
    path: '', component: ShellComponent, canActivate: [authed], children: [
      { path: '', pathMatch: 'full', canActivate: [home], children: [] },
      { path: 'workbench', canActivate: [can(P.queue)], loadComponent: () => import('./pages/workbench.component').then(m => m.WorkbenchComponent), title: 'QA workbench' },
      { path: 'batches', canActivate: [can(P.batchView)], loadComponent: () => import('./pages/batches.component').then(m => m.BatchesComponent), title: 'Batches' },
      { path: 'batches/new', canActivate: [can(P.batchCreate)], loadComponent: () => import('./pages/batch-create.component').then(m => m.BatchCreateComponent), title: 'New batch' },
      { path: 'batches/:id', canActivate: [can(P.batchView)], loadComponent: () => import('./pages/batch360.component').then(m => m.Batch360Component), title: 'Batch 360' },
      { path: 'dashboard', canActivate: [can(P.dashboard)], loadComponent: () => import('./pages/dashboard.component').then(m => m.DashboardComponent), title: 'Management dashboard' },
      { path: 'approvals', canActivate: [can(P.batchView)], loadComponent: () => import('./pages/approvals.component').then(m => m.ApprovalsComponent), title: 'Approvals' },
      { path: 'admin/templates', canActivate: [can(P.batchView)], loadComponent: () => import('./pages/admin/templates.component').then(m => m.TemplatesComponent), title: 'Checklist templates' },
      { path: 'admin/roles', canActivate: [can(P.roles)], loadComponent: () => import('./pages/admin/roles.component').then(m => m.RolesComponent), title: 'Roles & activities' },
      { path: 'admin/users', canActivate: [can(P.users)], loadComponent: () => import('./pages/admin/users.component').then(m => m.UsersComponent), title: 'Users' },
      { path: 'admin/master-data', canActivate: [can(P.batchView)], loadComponent: () => import('./pages/admin/master-data.component').then(m => m.MasterDataComponent), title: 'Master data' },
      { path: 'admin/settings', canActivate: [can(P.queue, P.settings)], loadComponent: () => import('./pages/admin/settings.component').then(m => m.SettingsComponent), title: 'Prioritisation settings' },
      { path: 'admin/audit', canActivate: [can(P.audit)], loadComponent: () => import('./pages/admin/audit.component').then(m => m.AuditComponent), title: 'Audit trail' },
    ]
  },
  { path: '**', redirectTo: '' }
];
