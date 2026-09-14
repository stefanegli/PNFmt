class Example
{
  const int Z = 2, A = 1;
  Example() { }
  ~Example() { }
  public int Visible => 2;
  private int Hidden => 1;
  public int this[int index] => index;
  public event System.Action Changed { add { } remove { } }
  public void Alpha(int value) { }
  public void Alpha() { }
  public void Beta() { }
  private void Zebra() { }
  public static Example operator +(Example left, Example right) => left;
  class Nested
  {
    void A() { }
    void Z() { }
  }
}
