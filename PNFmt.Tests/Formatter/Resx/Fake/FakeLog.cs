namespace PNFmt.Tests.Formatter.Resx.Fake
{
    using PNFmt;

    using System;

    internal sealed class FakeLog : IFormatterLog
    {
        public void Write(Exception exception)
        {
        }

        public void WriteLine(string message)
        {
        }
    }
}
