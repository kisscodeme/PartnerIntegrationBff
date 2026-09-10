using PIBFF.Domain.Entities;

namespace PIBFF.Application.Interfaces;

/// <summary>Publishes verified transactions to the asynchronous processing queue.</summary>
public interface IMessagePublisher
{
    Task PublishAsync(PartnerTransaction transaction, CancellationToken cancellationToken = default);
}
