class Example
{
    object M() => from customer in customerAccounts where customer.Active group customer by customer.Region into region orderby region.Key select region;
}
