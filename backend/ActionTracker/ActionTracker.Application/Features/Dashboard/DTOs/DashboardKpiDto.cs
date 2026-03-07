namespace ActionTracker.Application.Features.Dashboard.DTOs;

public class DashboardKpiDto
{
    public int     TotalActions       { get; set; }
    public decimal CompletionRate     { get; set; }  // 0-100 %
    public decimal OnTimeDeliveryRate { get; set; }  // 0-100 %
    public int     ActiveEscalations  { get; set; }
    public int     TeamVelocity       { get; set; }  // count of Done items
    public int     CriticalHighCount  { get; set; }  // non-Done Critical or High
    public int     InProgressCount    { get; set; }
    public int     OverdueCount       { get; set; }
}
