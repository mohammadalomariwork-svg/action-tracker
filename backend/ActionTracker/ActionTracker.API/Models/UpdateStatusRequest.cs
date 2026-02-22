using ActionTracker.Domain.Enums;

namespace ActionTracker.API.Models;

/// <summary>Body for PATCH api/action-items/{id}/status</summary>
public record UpdateStatusRequest(ActionStatus Status);
