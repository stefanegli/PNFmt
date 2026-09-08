using Alpha;
using Zeta;

public static partial class Helpers
{
  private static readonly string Message = "value";

  public static void Write()
  {
    Console.WriteLine(Message);
  }
// pnfmt: off
static public void Aligned( ){    }


static public void Kept( ){    }
// pnfmt: on
  public static void After() { }
}
