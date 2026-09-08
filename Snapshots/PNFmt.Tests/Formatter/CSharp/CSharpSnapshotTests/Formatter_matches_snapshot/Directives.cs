using Alpha;
using Zeta;
#if FEATURE
using Zebra;
using Beta;
#else
using Other.Alpha;
using Other.Zeta;
#endif

// Application imports stay in this section.
using App.Alpha;
using App.Zeta; // Keep this explanation with Zeta.

class Example
{
  private string json = """
    {
      "message": "Keep literal indentation"
    }
    """;
  #region Methods
  public void M()
  {
    Console.WriteLine(json);
  }
  #endregion
}
