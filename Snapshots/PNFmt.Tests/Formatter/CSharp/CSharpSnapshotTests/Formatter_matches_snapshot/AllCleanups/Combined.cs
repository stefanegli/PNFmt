using System;
using Alpha;
using Zeta;
public static class Helpers
{
  private static readonly string Message = "keep  literal spacing";
  public static void Alpha() { }

  public static void Zebra() { Console.WriteLine(Message); }
// pnfmt: off
static public void   KeepZ( ){    }
static public void   KeepA( ){    }
// pnfmt: on
  public static void Delta() { }
  public static void Beta() { }
}
