class Example
{
    string Select(bool preferred) => preferred ? "preferred" : backupAccountIsEligible ? "backup" : "standard";
}
