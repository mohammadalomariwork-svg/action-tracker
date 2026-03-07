import { Component, input, output, signal, computed } from '@angular/core';

export interface ColumnDef {
  key: string;
  label: string;
  sortable?: boolean;
  width?: string;
  /** Optional cell renderer: return HTML string or plain value */
  render?: (row: any) => string;
}

@Component({
  selector: 'app-data-table',
  standalone: true,
  templateUrl: './data-table.component.html',
  styleUrl: './data-table.component.scss',
})
export class DataTableComponent {
  readonly columns = input.required<ColumnDef[]>();
  readonly data = input.required<any[]>();
  readonly loading = input<boolean>(false);
  readonly emptyMessage = input<string>('No data available.');

  readonly sortKey = signal<string | null>(null);
  readonly sortDesc = signal(false);

  readonly sortChanged = output<{ key: string; desc: boolean }>();

  readonly skeletonRows = Array.from({ length: 5 });

  toggleSort(col: ColumnDef): void {
    if (!col.sortable) return;
    if (this.sortKey() === col.key) {
      this.sortDesc.update(d => !d);
    } else {
      this.sortKey.set(col.key);
      this.sortDesc.set(false);
    }
    this.sortChanged.emit({ key: this.sortKey()!, desc: this.sortDesc() });
  }

  cellValue(row: any, col: ColumnDef): string {
    if (col.render) return col.render(row);
    const val = row[col.key];
    return val == null ? '—' : String(val);
  }
}
