namespace EasytierUptime.DTOs;

public record LoginRequest(string Username, string Password, string CaptchaId, string CaptchaCode);
public record RegisterRequest(string Username, string Password);
public record RegisterEmailRequest(string Username, string Password, string Email);
public record ResendVerificationRequest(string Username);
public record VisibilityDto(bool IsPublic);

// New: node update request DTO (backend admin editing)
public record NodeUpdateRequest(
    string Name,
    string Protocol,
    string Host,
    int Port,
    string NetworkName,
    string NetworkSecret,
    int MaxConnections,
    bool AllowRelay,
    bool IsPublic,
    string Description
);

// User management DTOs (extended with Email)
public record CreateUserRequest(string Username, string Password, string Role, string? Email);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record AdminSetPasswordRequest(string Password);
public record ChangeRoleRequest(string Role);
public record UserDto(int Id, string Username, string Role, System.DateTime CreatedAt, string? Email);

// Public-facing DTO for exposing node info safely (no secrets)
public record PublicNodeDto(
    int Id,
    string Name,
    string Host,
    int Port,
    string Protocol,
    string Version,
    int CurrentConnections,
    string Description,
    string? LastStatus,
    System.DateTime? LastCheckedAt
);

// Email code registration
public record SendCodeRequest(string Email, string CaptchaId, string CaptchaCode);
public record RegisterWithCodeRequest(string Username, string Password, string Email, string Code);

// Captcha response
public record CaptchaCreateResponse(string CaptchaId, string ImageBase64);
