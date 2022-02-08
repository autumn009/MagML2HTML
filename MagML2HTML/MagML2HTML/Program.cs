using MagMLParser;
using System.Xml;

//const string filename = @"C:\Users\autumn.PDTOKYO4\OneDrive\SelfShare\0pub\C#\C#PRIMER2\org\20211107102022_▲_川俣晶の縁側_ソフトウェア_new%5FC#入門・全キーワード明快解説!.xmlcol";
const string filename = @"C:\Users\autumn.PDTOKYO4\OneDrive\SelfShare\0pub\C#\C#PRIMER2\org\20211114105944_▲_川俣晶の縁側_ソフトウェア_new%5FC#入門・全キーワード明快解説!.xmlcol";


// DEBUG CODE
XmlDocument doc = new XmlDocument();
doc.Load( File.OpenRead(filename));



var srcNode = doc.GetElementsByTagName("body",XmlNamespaces.Item);
if (srcNode.Count > 0)
{
    var src = srcNode[0].InnerText;

    var processor = new MagML();
    processor.Compile(src, null, false);
    Console.WriteLine(processor.RenderedBody);
}
