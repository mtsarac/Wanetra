namespace Wanetra.Api.Contracts;

/// <summary>
/// The single error shape every endpoint returns. <paramref name="Code"/> is a
/// stable identifier the frontend can branch on; <paramref name="Message"/> is
/// for humans and never carries exception detail.
/// </summary>
public sealed record ApiError(string Code, string Message);
