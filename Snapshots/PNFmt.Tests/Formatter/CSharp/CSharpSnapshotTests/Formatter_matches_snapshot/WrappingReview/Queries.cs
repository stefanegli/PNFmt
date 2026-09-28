class Example
{
    object M() => from customer in customerAccounts where customer.IsEligibleForDiscount orderby customer.DisplayName select customer.ReferenceIdentifier;
}
