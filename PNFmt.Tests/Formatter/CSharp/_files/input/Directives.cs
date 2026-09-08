using Zeta;
using Alpha;
#if FEATURE
using Zebra;
using Beta;
#else
using Other.Zeta;
using Other.Alpha;
#endif

// Application imports stay in this section.
using App.Zeta; // Keep this explanation with Zeta.
using App.Alpha;

class Example{
private string json = """
    {
      "message": "Keep literal indentation"
    }
    """;
#region Methods
public void M(){
Console.WriteLine(json);
}
#endregion
}
