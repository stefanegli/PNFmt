// This header stays above the imports.
using Zebra;
using System.Text;
using Alpha;

namespace Sample;
using Zeta;
using Beta;

public class Example(int count){
public int[] Values {get;} = [3,2,1];
public void Write(){
if(count>0){
Console.WriteLine("value: {0}",count);
}
}
}

public static class Extensions{
extension(string value){
public bool HasText => value.Length>0;
}
}
