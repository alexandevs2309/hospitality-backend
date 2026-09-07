using FluentAssertions;
using Hospitality.Domain.Entities;
using Xunit;

namespace Hospitality.UnitTests.Domain;

public class InvoiceTests
{
    [Fact]
    public void Issue_FromDraft_SetsIssuedStateAndDueDate()
    {
        var invoice = new Invoice();
        var before = DateTime.UtcNow;

        invoice.Issue();

        invoice.Status.Should().Be("Issued");
        invoice.IssueDate.Should().BeOnOrAfter(before.AddSeconds(-1));
        invoice.DueDate.Should().BeCloseTo(invoice.IssueDate.AddDays(30), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Issue_WhenAlreadyIssued_DoesNotOverwriteNumberOrDate()
    {
        var invoice = new Invoice();
        invoice.Issue();
        var originalNumber = invoice.InvoiceNumber;
        var originalIssueDate = invoice.IssueDate;

        invoice.Issue();

        invoice.InvoiceNumber.Should().Be(originalNumber);
        invoice.IssueDate.Should().Be(originalIssueDate);
    }

    [Fact]
    public void Issue_WithEmptyId_GeneratesInvoiceNumberWithoutThrowing()
    {
        var invoice = new Invoice();

        var act = () => invoice.Issue();

        act.Should().NotThrow();
        invoice.InvoiceNumber.Should().NotBeNullOrEmpty();
        invoice.InvoiceNumber.Should().StartWith("INV-");
        invoice.InvoiceNumber.Should().Contain(DateTime.UtcNow.ToString("yyyyMMdd"));
    }

    [Fact]
    public void AddLineItemAndCalculateTotals_Applies16PercentTax()
    {
        var invoice = new Invoice();

        invoice.AddLineItem("Habitación doble", 100m, 2);
        invoice.AddLineItem("Desayuno", 10m, 2);

        invoice.Subtotal.Should().Be(220m);
        invoice.TaxAmount.Should().Be(35.2m);
        invoice.TotalAmount.Should().Be(255.2m);
    }

    [Fact]
    public void CalculateTotals_UsesConfiguredTaxRate()
    {
        var invoice = new Invoice { TaxRate = 10m };

        invoice.AddLineItem("Habitación doble", 100m, 2);

        invoice.Subtotal.Should().Be(200m);
        invoice.TaxAmount.Should().Be(20m);
        invoice.TotalAmount.Should().Be(220m);
    }

    [Fact]
    public void MarkAsPaid_SetsPaidAndTracksPayment()
    {
        var invoice = new Invoice();
        invoice.AddLineItem("Habitación", 100m, 1);
        invoice.Issue();
        invoice.Send();

        invoice.MarkAsPaid("Recepcionista", DateTime.UtcNow);

        invoice.Status.Should().Be("Paid");
        invoice.PaidBy.Should().Be("Recepcionista");
        invoice.AmountPaid.Should().Be(invoice.TotalAmount);
        invoice.BalanceDue.Should().Be(0);
    }

    [Fact]
    public void MarkAsPaid_DoesNothing_WhenNotIssuedSentOverdue()
    {
        var invoice = new Invoice();
        invoice.AddLineItem("Habitación", 100m, 1);

        invoice.MarkAsPaid("X", DateTime.UtcNow);

        invoice.Status.Should().Be("Draft");
        invoice.PaidBy.Should().BeNull();
    }

    [Fact]
    public void Cancel_StoresReasonInNotes()
    {
        var invoice = new Invoice();
        invoice.AddLineItem("Habitación", 100m, 1);
        invoice.Issue();

        invoice.Cancel("Cliente no se presentó");

        invoice.Status.Should().Be("Cancelled");
        invoice.Notes.Should().Contain("Cliente no se presentó");
    }

    [Fact]
    public void BalanceDue_EqualsTotalMinusPaid()
    {
        var invoice = new Invoice();
        invoice.AddLineItem("Habitación", 100m, 1);
        invoice.AddPayment(50m, new Payment { PaymentDate = DateTime.UtcNow, ProcessedBy = "Recepcionista" });

        invoice.AmountPaid.Should().Be(50m);
        invoice.BalanceDue.Should().Be(invoice.TotalAmount - 50m);
    }
}