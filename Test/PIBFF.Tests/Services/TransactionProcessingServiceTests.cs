using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PIBFF.Application.Common;
using PIBFF.Application.DTOs;
using PIBFF.Application.Interfaces;
using PIBFF.Application.Services;
using PIBFF.Application.Validators;
using PIBFF.Domain.Entities;

namespace PIBFF.Tests.Services;

public class TransactionProcessingServiceTests
{
    private readonly Mock<IPartnerVerificationService> _verificationServiceMock = new();
    private readonly Mock<IMessagePublisher> _messagePublisherMock = new();
    private readonly TransactionProcessingService _sut;

    public TransactionProcessingServiceTests()
    {
        // Default: verification succeeds, so existing validation-focused tests
        // don't need to know about the verification stage unless they override it.
        _verificationServiceMock
            .Setup(x => x.VerifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PartnerVerificationOutcome.Verified());


        _sut = new TransactionProcessingService(
            new TransactionRequestValidator(),
            _verificationServiceMock.Object,
            _messagePublisherMock.Object,
            NullLogger<TransactionProcessingService>.Instance);
    }

    /// <summary>
    /// ValidRequest
    /// </summary>
    /// <returns></returns>
    private static TransactionRequest ValidRequest() => new()
    {
        PartnerId = "P-1001",
        TransactionReference = "TXN-99823",
        Amount = 250.00m,
        Currency = "USD",
        Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1)
    };

    /// <summary>
    /// ProcessAsync_Should_Return_Success_For_A_Valid_Payload
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task ProcessAsync_Should_Return_Success_For_A_Valid_Payload()
    {
        var result = await _sut.ProcessAsync(ValidRequest());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.PartnerId.Should().Be("P-1001");
        result.Value.TransactionReference.Should().Be("TXN-99823");
        result.Value.Status.Should().Be("Verified");
        result.Value.CorrelationId.Should().NotBeEmpty();

        _verificationServiceMock.Verify(
            x => x.VerifyAsync("P-1001", It.IsAny<CancellationToken>()), Times.Once);
        _messagePublisherMock.Verify(
            x => x.PublishAsync(It.Is<PartnerTransaction>(t =>
                t.PartnerId == "P-1001" && t.TransactionReference == "TXN-99823" && t.Currency == "USD"),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// ProcessAsync Should Return ValidationFailed For An Invalid Payload
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task ProcessAsync_Should_Return_ValidationFailed_For_An_Invalid_Payload()
    {
        var invalidRequest = ValidRequest() with { Amount = -5m };
        var result = await _sut.ProcessAsync(invalidRequest);

        result.IsSuccess.Should().BeFalse();
        result.FailureReason.Should().Be(ProcessingFailureReason.ValidationFailed);
        result.Errors.Should().ContainSingle(e => e.Contains("amount"));

        // Fail-fast: an invalid payload should never trigger a (costly, external) verification call.
        _verificationServiceMock.Verify(
            x => x.VerifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// ProcessAsync Should Normalize Currency To Uppercase
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task ProcessAsync_Should_Aggregate_Multiple_Validation_Errors()
    {
        var invalidRequest = new TransactionRequest { Amount = -1m };
        var result = await _sut.ProcessAsync(invalidRequest);
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public async Task ProcessAsync_Should_Normalize_Currency_To_Uppercase()
    {
        var request = ValidRequest() with { Currency = "usd" };
        var result = await _sut.ProcessAsync(request);
        result.IsSuccess.Should().BeTrue();
    }

    // TODO (requirement 2): verify transaction.PartnerId against the
    // Partner Verification API with a resilience strategy before proceeding.
    /// <summary>
    /// ProcessAsync Should Return PartnerVerificationFailed When Partner Is Rejected
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task ProcessAsync_Should_Return_PartnerVerificationFailed_When_Partner_Is_Rejected()
    {
        _verificationServiceMock
            .Setup(x => x.VerifyAsync("P-1001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PartnerVerificationOutcome.Rejected("partnerId 'P-1001' is not a recognized partner."));

        var result = await _sut.ProcessAsync(ValidRequest());
        result.IsSuccess.Should().BeFalse();
        result.FailureReason.Should().Be(ProcessingFailureReason.PartnerVerificationFailed);
        result.Errors.Should().ContainSingle(e => e.Contains("not a recognized partner"));
    }

    /// <summary>
    /// ProcessAsync Should Return PartnerVerificationUnavailable When Upstream Keeps Failing
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task ProcessAsync_Should_Return_PartnerVerificationUnavailable_When_Upstream_Keeps_Failing()
    {
        _verificationServiceMock
            .Setup(x => x.VerifyAsync("P-1001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PartnerVerificationOutcome.Unavailable("Verification service timed out after retries."));

        var result = await _sut.ProcessAsync(ValidRequest());
        result.IsSuccess.Should().BeFalse();
        result.FailureReason.Should().Be(ProcessingFailureReason.PartnerVerificationUnavailable);
        result.Errors.Should().ContainSingle(e => e.Contains("timed out"));
    }

    // TODO (requirement 3): verify transaction.PartnerId against the
    // Partner Verification API with a resilience strategy before proceeding.
    /// <summary>
    /// ProcessAsync Should Return PublishFailed When Message Broker Fails
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task ProcessAsync_Should_Return_PublishFailed_When_Message_Broker_Fails()
    {
        _messagePublisherMock
            .Setup(x => x.PublishAsync(It.IsAny<PartnerTransaction>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("RabbitMQ unavailable"));

        var result = await _sut.ProcessAsync(ValidRequest());
        result.IsSuccess.Should().BeFalse();
        result.FailureReason.Should().Be(ProcessingFailureReason.PublishFailed);
        result.Errors.Should().ContainSingle(e => e.Contains("Message broker"));
    }

    /// <summary>
    /// ProcessAsync Should Not Publish When Partner Is Rejected
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task ProcessAsync_Should_Not_Publish_When_Partner_Is_Rejected()
    {
        _verificationServiceMock
            .Setup(x => x.VerifyAsync("P-1001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PartnerVerificationOutcome.Rejected("not recognized"));

        var result = await _sut.ProcessAsync(ValidRequest());
        result.IsSuccess.Should().BeFalse();
        _messagePublisherMock.Verify(
            x => x.PublishAsync(It.IsAny<PartnerTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
