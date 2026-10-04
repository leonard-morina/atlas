using MediatR;

namespace Atlas.Onboarding.Application.Abstractions;

/// <summary>
/// A request that only reads. Commands change state and are logged at Information; queries can be frequent (a
/// submission polls for its decision) and are logged at Debug.
/// </summary>
public interface IQuery<out TResponse> : IRequest<TResponse>;
