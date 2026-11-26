using MilkWala.Localization;
using MilkWala.Models;
using MilkWala.Services.Data;
using Xunit;

namespace MilkWala.Tests;

/// <summary>Rule-check errors and the billing summary are produced in the language the user is using.</summary>
[Collection("Language")]
public class LocalizedMessagesTests : IDisposable
{
    public LocalizedMessagesTests() => AppLanguage.Set("en");

    public void Dispose() => AppLanguage.Set("en");

    private static bool HasDevanagari(string text) => text.Any(c => c >= 'ऀ' && c <= 'ॿ');

    [Theory]
    [InlineData("en")]
    [InlineData("hi")]
    [InlineData("mr")]
    public void A_missing_name_is_reported_in_the_current_language(string language)
    {
        AppLanguage.Set(language);
        using var db = new TestDb();

        var ex = Assert.Throws<BusinessRuleException>(() => db.InTransaction(c =>
            CustomerOnboarding.Add(c, new NewCustomerRequest("", "9822334455", "", MilkType.Cow, 1, 0, DateTime.Today))));

        Assert.Equal(Translations.Get(language, "ErrEnterCustomerName"), ex.Message);
        Assert.Equal(language != "en", HasDevanagari(ex.Message));
    }

    [Fact]
    public void An_overpayment_message_keeps_the_amount_and_name_inside_the_translated_sentence()
    {
        AppLanguage.Set("hi");
        using var db = new TestDb();
        var farm = db.AddSupplier(outstandingPayout: 400m);
        farm.Name = "Ramesh Farm";
        db.Connection.Update(farm);

        var ex = Assert.Throws<BusinessRuleException>(() => db.InTransaction(c => SupplierLedger.Pay(c, farm.Id, 500m)));

        Assert.Equal("Ramesh Farm को केवल ₹400.00 देना बाकी है।", ex.Message);
    }

    [Fact]
    public void Status_words_inside_a_message_are_translated_too()
    {
        AppLanguage.Set("hi");
        using var db = new TestDb();
        var complaint = new Complaint { Status = ComplaintStatus.Closed };
        db.Connection.Insert(complaint);

        var ex = Assert.Throws<BusinessRuleException>(() => db.InTransaction(c =>
            ComplaintLedger.Update(c, complaint.Id, ComplaintStatus.Open, "x")));

        Assert.Equal("बंद शिकायत को खुली में नहीं बदला जा सकता।", ex.Message);
    }

    [Fact]
    public void A_milk_type_inside_a_message_follows_the_language()
    {
        AppLanguage.Set("mr");
        using var db = new TestDb();
        var customer = db.AddCustomer(0m);
        var tomorrow = DateTime.Today.AddDays(1);
        db.InTransaction(c => SpecialOrderLedger.Request(c, customer.Id, MilkType.Cow, 1, tomorrow, null, DateTime.Today));

        var ex = Assert.Throws<BusinessRuleException>(() => db.InTransaction(c =>
            SpecialOrderLedger.Request(c, customer.Id, MilkType.Cow, 1, tomorrow, null, DateTime.Today)));

        Assert.Contains("गायीचे दूध", ex.Message);
    }

    [Fact]
    public void The_billing_summary_is_translated_including_the_amounts()
    {
        var delivery = new Delivery { CustomerName = "Rahul", Status = DeliveryStatus.Delivered };
        var summary = new DeliveryChange(delivery, 30m, 63m, 0, 0);

        Assert.Equal("Delivered to Rahul. ₹30.00 charged to wallet. ₹63.00 added to dues.", summary.Summary());

        AppLanguage.Set("hi");
        Assert.Equal("Rahul को डिलीवर किया गया। वॉलेट से ₹30.00 काटे गए। ₹63.00 बकाया में जोड़े गए।", summary.Summary());

        AppLanguage.Set("mr");
        Assert.Equal("Rahul यांना पोहोचवले. वॉलेटमधून ₹30.00 वजा केले. ₹63.00 थकबाकीत जोडले.", summary.Summary());
    }

    [Fact]
    public void A_status_other_than_delivered_or_skipped_uses_the_translated_status_word()
    {
        AppLanguage.Set("hi");
        var delivery = new Delivery { CustomerName = "Rahul", Status = DeliveryStatus.Holiday };

        Assert.Equal("Rahul को छुट्टी चिह्नित किया गया।", new DeliveryChange(delivery, 0, 0, 0, 0).Summary());
    }

    [Fact]
    public void A_backup_error_is_translated()
    {
        AppLanguage.Set("hi");

        var ex = Assert.Throws<BusinessRuleException>(() => SqliteBackup.Validate("/no/such/backup.db"));

        Assert.Equal("बैकअप फ़ाइल नहीं मिली।", ex.Message);
    }
}
