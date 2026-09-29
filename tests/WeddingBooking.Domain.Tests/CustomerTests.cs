using WeddingBooking.Domain.Common;
using WeddingBooking.Domain.Customers;

namespace WeddingBooking.Domain.Tests;

public sealed class CustomerTests
{
    [Fact]
    public void The_couple_is_two_people_and_the_name_follows_from_them()
    {
        var customer = Customer.Create(new("Tobias", "Hansen", "tobias@example.dk"), new("Cecilie", "Berg"));

        Assert.Equal("Tobias og Cecilie", customer.Name);
        Assert.Equal("Tobias Hansen", customer.Payer!.FullName);

        customer.UpdatePartner(customer.Partners.Last().Id, new("Cecilia", "Berg"));
        Assert.Equal("Tobias og Cecilia", customer.Name);
    }

    [Fact]
    public void Switching_the_payer_leaves_exactly_one_and_keeps_the_names_in_order()
    {
        var customer = Customer.Create(new("Tobias"), new("Cecilie"));
        customer.MakePayer(customer.Partners.Last().Id);

        Assert.Equal("Cecilie", Assert.Single(customer.Contacts, c => c.IsPayer).FirstName);
        Assert.Equal("Tobias og Cecilie", customer.Name);
    }

    [Fact]
    public void A_third_person_cannot_be_added() =>
        Assert.Throws<DomainException>(() => Customer.Create(new("Tobias"), new("Cecilie")).AddMissingPartner(new("Ida")));

    [Fact]
    public void Both_need_a_first_name() =>
        Assert.Throws<DomainException>(() => Customer.Create(new("Tobias"), new(" ")));

    [Theory]
    [InlineData("Tobias og Cecilie", "Tobias", "Cecilie")]
    [InlineData("Anna & Mikkel", "Anna", "Mikkel")]
    [InlineData("Emma+Oliver", "Emma", "Oliver")]
    [InlineData("Ida OG Frederik", "Ida", "Frederik")]
    public void A_couple_written_together_splits_in_two(string name, string first, string second) =>
        Assert.Equal((first, second), Customer.SplitCouple(name));

    [Theory]
    [InlineData("Bryllup 80 pax")]
    [InlineData("Tobias")]
    [InlineData("A og B og C")]
    public void A_name_that_is_not_two_people_does_not_split(string name) => Assert.Null(Customer.SplitCouple(name));
}
