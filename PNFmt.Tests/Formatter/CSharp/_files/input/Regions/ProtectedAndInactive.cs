#region Removable wrapper
class Example{
// pnfmt: off
#region Protected pair
void   Keep( ){    }
// pnfmt: on
#endregion
#if NEVER
#region Inactive pair
this is deliberately inactive text
#endregion
#endif
#region Remove this pair
void FormatMe(){ }
#endregion
}
#endregion
