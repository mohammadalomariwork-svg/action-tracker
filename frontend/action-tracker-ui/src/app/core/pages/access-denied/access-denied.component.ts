import { Component, inject } from '@angular/core';
import { Location, AsyncPipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { map } from 'rxjs/operators';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-access-denied',
  standalone: true,
  imports: [RouterLink, AsyncPipe],
  templateUrl: './access-denied.component.html',
  styleUrl: './access-denied.component.scss',
})
export class AccessDeniedComponent {
  private readonly location = inject(Location);
  private readonly route    = inject(ActivatedRoute);
  private readonly authSvc  = inject(AuthService);

  readonly reason$ = this.route.queryParamMap.pipe(
    map(params => params.get('reason') ?? '')
  );

  readonly displayName$ = this.authSvc.currentUser$.pipe(
    map(user => user?.displayName ?? null)
  );

  goBack(): void {
    this.location.back();
  }
}
