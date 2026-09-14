class Example
{

  /// <summary>This documentation belongs to Alpha.</summary>
  [System.Obsolete("Keep with Alpha")]
  void Alpha() { } // Alpha's trailing comment
  void Zebra() { }
  static readonly int Storage = Create();
  void Beta() { }
  void Delta() { }
  int Value { get; set; }
  // This standalone comment separates the next sorting run.
  void Zulu() { }
  static int Create() => 1;
  void Echo() { }
}
