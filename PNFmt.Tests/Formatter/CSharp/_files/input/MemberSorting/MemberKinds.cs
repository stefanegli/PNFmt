class Example{
class Nested { void Z(){ } void A(){ } }
private void Zebra(){ }
public void Beta(){ }
public void Alpha(int value){ }
public void Alpha(){ }
private int Hidden => 1;
public int Visible => 2;
Example(){ }
const int Z = 2, A = 1;
~Example(){ }
public event System.Action Changed { add { } remove { } }
public int this[int index] => index;
public static Example operator +(Example left, Example right) => left;
}
