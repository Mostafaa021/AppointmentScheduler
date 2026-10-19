import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

/** AC-004-15: sign in; 400/401/423 stay here and the error interceptor shows the server's `message`. */
@Component({
  selector: 'app-login-page',
  imports: [FormsModule, MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule],
  template: `
    <mat-card appearance="outlined" class="login">
      <mat-card-header><mat-card-title>Sign in</mat-card-title></mat-card-header>
      <mat-card-content>
        <form (ngSubmit)="submit()">
          <mat-form-field appearance="outline">
            <mat-label>E-mail</mat-label>
            <input matInput type="email" name="email" autocomplete="username" required [(ngModel)]="email" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Password</mat-label>
            <input matInput type="password" name="password" autocomplete="current-password" required [(ngModel)]="password" />
          </mat-form-field>
          <button mat-flat-button type="submit" [disabled]="pending()">Sign in</button>
        </form>
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    .login { max-width: 400px; margin: 48px auto; }
    form { display: flex; flex-direction: column; gap: 8px; padding-top: 16px; }
  `,
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected email = '';
  protected password = '';
  protected readonly pending = signal(false);

  protected submit(): void {
    this.pending.set(true);
    this.auth.login(this.email, this.password).subscribe({
      next: () => void this.router.navigateByUrl('/schedule'),
      error: () => this.pending.set(false),
    });
  }
}
