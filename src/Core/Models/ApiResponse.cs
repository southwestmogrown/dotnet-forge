namespace Core.Models;

public record ApiResponse<T>(bool Success, T Data, string? Message = null);
