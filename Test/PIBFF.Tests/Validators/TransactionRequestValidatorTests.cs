using FluentValidation.TestHelper;
using PIBFF.Application.DTOs;
using PIBFF.Application.Validators;

namespace PIBFF.Tests.Validators;

public class TransactionRequestValidatorTests
{
    private readonly TransactionRequestValidator _validator = new();

    private static TransactionRequest ValidRequest() => new()
    {
        PartnerId = "P-1001",
        TransactionReference = "TXN-99823",
        Amount = 250.00m,
        Currency = "USD",
        Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1)
    };

    /// <summary>
    /// Should PassFor A Fully Valid Payload
    /// </summary>
    [Fact]
    public void Should_Pass_For_A_Fully_Valid_Payload()
    {
        var result = _validator.TestValidate(ValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>
    /// Should Fail When PartnerId Is Missing
    /// </summary>
    /// <param name="partnerId"></param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_Fail_When_PartnerId_Is_Missing(string? partnerId)
    {
        var request = ValidRequest() with { PartnerId = partnerId };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PartnerId);
    }

    /// <summary>
    /// Should Fail When TransactionReference Is Missing
    /// </summary>
    /// <param name="reference"></param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_Fail_When_TransactionReference_Is_Missing(string? reference)
    {
        var request = ValidRequest() with { TransactionReference = reference };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.TransactionReference);
    }

    /// <summary>
    /// Should Fail When Amount Is Not Greater Than Zero
    /// </summary>
    /// <param name="amount"></param>
    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(-100)]
    public void Should_Fail_When_Amount_Is_Not_Greater_Than_Zero(decimal amount)
    {
        var request = ValidRequest() with { Amount = amount };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Amount)
            .WithErrorMessage("amount must be greater than 0.");
    }

    /// <summary>
    /// Should Pass When AmountIs Greater Than Zero
    /// </summary>
    /// <param name="amount"></param>
    [Theory]
    [InlineData(0.01)]
    [InlineData(1)]
    [InlineData(250.00)]
    [InlineData(999999.99)]
    public void Should_Pass_When_Amount_Is_Greater_Than_Zero(decimal amount)
    {
        var request = ValidRequest() with { Amount = amount };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Amount);
    }

    /// <summary>
    /// Should Fail For Missing Or Invalid Currency
    /// </summary>
    /// <param name="currency"></param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("XXX")]
    [InlineData("US")]
    [InlineData("usd1")]
    public void Should_Fail_For_Missing_Or_Invalid_Currency(string? currency)
    {
        var request = ValidRequest() with { Currency = currency };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Currency);
    }

    /// <summary>
    /// Should Pass For Supported Currency Codes
    /// </summary>
    /// <param name="currency"></param>
    [Theory]
    [InlineData("USD")]
    [InlineData("eur")] // case-insensitive
    [InlineData("VND")]
    [InlineData("JPY")]
    public void Should_Pass_For_Supported_Currency_Codes(string currency)
    {
        var request = ValidRequest() with { Currency = currency };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Currency);
    }

    /// <summary>
    /// Should Fail When Timestamp Is Missing
    /// </summary>
    [Fact]
    public void Should_Fail_When_Timestamp_Is_Missing()
    {
        var request = ValidRequest() with { Timestamp = null };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Timestamp)
            .WithErrorMessage("timestamp is required.");
    }

    /// <summary>
    /// Should Fail When Timestamp Is Far In The Future
    /// </summary>
    [Fact]
    public void Should_Fail_When_Timestamp_Is_Far_In_The_Future()
    {
        var request = ValidRequest() with { Timestamp = DateTimeOffset.UtcNow.AddHours(1) };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Timestamp);
    }

    /// <summary>
    /// Should Pass When Timestamp Is Within Small Clock Skew Allowance
    /// </summary>
    [Fact]
    public void Should_Pass_When_Timestamp_Is_Within_Small_Clock_Skew_Allowance()
    {
        var request = ValidRequest() with { Timestamp = DateTimeOffset.UtcNow.AddMinutes(2) };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Timestamp);
    }

    /// <summary>
    /// Should Report All Violations For A Fully Empty Payload
    /// </summary>
    [Fact]
    public void Should_Report_All_Violations_For_A_Fully_Empty_Payload()
    {
        var request = new TransactionRequest();
        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.PartnerId);
        result.ShouldHaveValidationErrorFor(x => x.TransactionReference);
        result.ShouldHaveValidationErrorFor(x => x.Currency);
        result.ShouldHaveValidationErrorFor(x => x.Timestamp);
        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }
}
