class Example{
void Zebra(){ }
/// <summary>This documentation belongs to Alpha.</summary>
[System.Obsolete("Keep with Alpha")]
void Alpha(){ } // Alpha's trailing comment
static readonly int Storage = Create();
void Delta(){ }
void Beta(){ }
int Value { get; set; }
// This standalone comment separates the next sorting run.
void Zulu(){ }
void Echo(){ }
static int Create() => 1;
}
