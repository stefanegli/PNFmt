#region Imports
using Zeta; using System; using Alpha;
#endregion
static public class Helpers{
#region Members
static private readonly string Message = "keep  literal spacing";
static public void Zebra(){ Console.WriteLine(Message); }



static public void Alpha(){ }
// pnfmt: off
static public void   KeepZ( ){    }
static public void   KeepA( ){    }
// pnfmt: on
static public void Delta(){ }
static public void Beta(){ }
#endregion
}
