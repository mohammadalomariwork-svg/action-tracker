using ActionTracker.Application.Features.Auth.DTOs;

namespace ActionTracker.Application.Features.Auth.Interfaces;

public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(LoginRequestDto dto, string ipAddress, CancellationToken ct);
    Task<LoginResponseDto> RefreshTokenAsync(string refreshToken, string ipAddress, CancellationToken ct);
    Task RegisterAsync(RegisterRequestDto dto, CancellationToken ct);
    Task RevokeTokenAsync(string refreshToken, CancellationToken ct);
}
