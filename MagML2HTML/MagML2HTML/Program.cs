using MagMLParser;
using System.Diagnostics;
using System.Web;
using System.Xml;

if(Environment.GetCommandLineArgs().Length < 2)
{
    Console.WriteLine("usage: MagML2HTML SourceFilesWithWildcard DstHTMLFile");
    return;
}

var srcPaths = Environment.GetCommandLineArgs()[1];
var dstFileName = Environment.GetCommandLineArgs()[2];

using (var dstFile = File.CreateText(dstFileName))
{
    foreach (var filename in Directory.EnumerateFiles(Path.GetDirectoryName(srcPaths), Path.GetFileName(srcPaths)))
    {
        XmlDocument doc = new XmlDocument();
        doc.Load(File.OpenRead(filename));
        string subject = "NO NAME";
        var subjectNode = doc.GetElementsByTagName("subject", XmlNamespaces.Item);
        if (subjectNode.Count > 0)
            subject = subjectNode[0].InnerText;

        var srcNode = doc.GetElementsByTagName("body", XmlNamespaces.Item);
        if (srcNode.Count > 0)
        {
            var src = srcNode[0].InnerText;

            var processor = new MagML();
            processor.Compile(src, null, false);
            //Console.WriteLine(processor.RenderedBody);
            dstFile.WriteLine($"<h1>{HttpUtility.HtmlEncode(subject)}</h1>");
            dstFile.WriteLine(processor.RenderedBody);
        }
    }
}
Process.Start("cmd.exe", " /c " + dstFileName);
