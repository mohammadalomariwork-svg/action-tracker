import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BehaviorSubject, of } from 'rxjs';

import { CommentsSectionComponent } from './comments-section.component';
import { CommentService } from '../../../core/services/comment.service';
import { ToastService } from '../../../core/services/toast.service';
import { AuthService } from '../../../core/services/auth.service';
import { CommentInfo } from '../../../core/models/comment.model';

const mine: CommentInfo = {
  id: 'c-mine', relatedEntityType: 'Project', relatedEntityId: 'p1', content: 'Mine', authorUserId: 'U-ME',
  authorName: 'Mona Me', isHighImportance: false, createdAt: '2026-11-26T08:00:00Z', updatedAt: null,
};
const theirs: CommentInfo = {
  ...mine, id: 'c-theirs', content: 'Theirs', authorUserId: 'u-other', authorName: 'Omar Other',
};

// WO-2026-11 P-2b (B6): Edit / Delete only on the caller's own comments — the server
// refuses everyone else's — and none through a project role without the manage right.
describe('CommentsSectionComponent (WO-2026-11 P-2b)', () => {
  let fixture: ComponentFixture<CommentsSectionComponent>;

  async function render(opts: { canPost?: boolean; canEditOwn?: boolean } = {}): Promise<HTMLElement> {
    const comments = jasmine.createSpyObj<CommentService>('CommentService', ['getByEntity', 'add', 'update', 'delete']);
    comments.getByEntity.and.returnValue(of({ success: true, data: [mine, theirs] } as any));
    await TestBed.configureTestingModule({
      imports: [CommentsSectionComponent],
      providers: [
        { provide: CommentService, useValue: comments },
        { provide: ToastService, useValue: jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error']) },
        { provide: AuthService, useValue: { currentUser$: new BehaviorSubject({ userId: 'u-me' }) } },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(CommentsSectionComponent);
    fixture.componentRef.setInput('entityType', 'Project');
    fixture.componentRef.setInput('entityId', 'p1');
    if (opts.canPost !== undefined) fixture.componentRef.setInput('canPost', opts.canPost);
    if (opts.canEditOwn !== undefined) fixture.componentRef.setInput('canEditOwn', opts.canEditOwn);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  function actionsOf(el: HTMLElement, text: string): Element | null {
    const entry = Array.from(el.querySelectorAll('.comment-entry')).find(e => e.textContent?.includes(text));
    return entry?.querySelector('.comment-actions') ?? null;
  }

  it('offers Edit and Delete on the caller\'s own comment only (ids compared without case)', async () => {
    const el = await render();
    expect(actionsOf(el, 'Mine')).not.toBeNull();
    expect(actionsOf(el, 'Theirs')).toBeNull();
  });

  it('keeps the author\'s own Edit / Delete when they may no longer post', async () => {
    const el = await render({ canPost: false });
    expect(el.querySelector('.comment-readonly')).not.toBeNull();
    expect(actionsOf(el, 'Mine')).not.toBeNull();
  });

  it('offers no Edit / Delete through a project role without the manage right', async () => {
    const el = await render({ canPost: false, canEditOwn: false });
    expect(el.querySelectorAll('.comment-actions').length).toBe(0);
  });
});
