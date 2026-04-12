import { Component, AfterViewInit, ElementRef, ViewChild, Renderer2 } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { HeaderComponent } from './header/header.component';
import { FooterComponent } from './footer/footer.component';
import { LoadingBarComponent } from './loading-bar/loading-bar.component';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [RouterOutlet, HeaderComponent, FooterComponent, LoadingBarComponent],
  templateUrl: './layout.component.html',
  styleUrl: './layout.component.scss',
})
export class LayoutComponent implements AfterViewInit {
  @ViewChild('particleHost', { static: false }) particleHost!: ElementRef<HTMLDivElement>;

  constructor(private renderer: Renderer2) {}

  ngAfterViewInit(): void {
    this.spawnParticles();
  }

  private spawnParticles(): void {
    if (!this.particleHost) return;
    const host = this.particleHost.nativeElement;
    const colors = [
      { c: '#00E5FF', w: 40 },
      { c: '#7C5CFC', w: 20 },
      { c: '#FF6B9D', w: 12 },
      { c: '#22c55e', w: 10 },
      { c: '#f59e0b', w: 10 },
      { c: '#3b82f6', w: 8 },
    ];
    const total = 90;

    const pickColor = (): string => {
      const r = Math.random() * 100;
      let acc = 0;
      for (const { c, w } of colors) {
        acc += w;
        if (r < acc) return c;
      }
      return colors[0].c;
    };

    const pickSize = (): number => {
      const r = Math.random() * 100;
      if (r < 55) return 1 + Math.random() * 1.5;       // small: 1–2.5px (55%)
      if (r < 85) return 2.5 + Math.random() * 2;       // medium: 2.5–4.5px (30%)
      return 5 + Math.random() * 3.5;                    // large: 5–8.5px (15%)
    };

    for (let i = 0; i < total; i++) {
      const el = this.renderer.createElement('span');
      const size = pickSize();
      const color = pickColor();
      const duration = 12 + Math.random() * 20;
      const dx = (Math.random() - 0.5) * 120;
      const dy = (Math.random() - 0.5) * 120;

      this.renderer.addClass(el, 'dot');
      this.renderer.setStyle(el, 'width', `${size}px`);
      this.renderer.setStyle(el, 'height', `${size}px`);
      this.renderer.setStyle(el, 'background', color);
      this.renderer.setStyle(el, 'boxShadow', `0 0 ${size * 2}px ${color}`);
      this.renderer.setStyle(el, 'left', `${Math.random() * 100}%`);
      this.renderer.setStyle(el, 'top', `${Math.random() * 100}%`);
      this.renderer.setStyle(el, 'animationDuration', `${duration}s`);
      this.renderer.setStyle(el, 'animationDelay', `${-Math.random() * duration}s`);
      this.renderer.setStyle(el, '--dx', `${dx}px`);
      this.renderer.setStyle(el, '--dy', `${dy}px`);
      this.renderer.appendChild(host, el);
    }
  }
}
