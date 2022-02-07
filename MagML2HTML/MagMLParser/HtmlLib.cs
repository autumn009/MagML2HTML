using System.Xml;

namespace MagMLParser
{
    /// <summary>
    /// HtmlLib の概要の説明です。
    /// </summary>
    public class HtmlLib
	{
		private static bool isEmptyElement(XmlNode node)
		{
			string[] emptyElementNames =
				{
					"hr",
					"br",
					"area",
					"link",
					"img",
					"param",
					"input",
					"col",
					"base",
					"meta"
				};

			if (node.NamespaceURI != XmlNamespaces.Xhtml) return false;
			foreach (string s in emptyElementNames)
			{
				if (node.LocalName == s) return true;
			}
			return false;
		}
		private static void dumpSub(XmlNode node, TextWriter writer)
		{
			switch (node.NodeType)
			{
				case XmlNodeType.Element:
					writer.Write("<");
					writer.Write(node.Name);
					if (node.Attributes.Count > 0)
					{
						writer.Write(" ");
						foreach (System.Xml.XmlAttribute att in node.Attributes)
						{
							writer.Write(att.Name);
							writer.Write("=\"");
							writer.Write(NestedHtmlWriter.NhUtil.QuoteText(att.Value));
							writer.Write("\" ");
						}
					}
					if (isEmptyElement(node))
					{
						writer.Write(" /");
					}
					writer.Write(">");
					break;
				case XmlNodeType.CDATA:
					writer.Write(NestedHtmlWriter.NhUtil.QuoteText(node.Value));
					break;
				case XmlNodeType.Text:
					writer.Write(NestedHtmlWriter.NhUtil.QuoteText(node.Value));
					break;
					//case XmlNodeType.Comment:
					//	writer.Write( "<!--" );
					//	break;
			}

			if (node.NamespaceURI == XmlNamespaces.Xhtml)
			{
				if (node.NodeType == XmlNodeType.Element)
				{
					if (node.LocalName == "head")
					{
						writer.Write("<link rel=\"stylesheet\" type=\"text/css\" href=\"blue001.css\" />");
					}
				}
			}

			foreach (XmlNode child in node.ChildNodes)
			{
				dumpSub(child, writer);
			}
			switch (node.NodeType)
			{
				case XmlNodeType.Element:
					if (!isEmptyElement(node))
					{
						writer.Write("</");
						writer.Write(node.Name);
						writer.Write(">");
					}
					break;
					//case XmlNodeType.Comment:
					//	writer.Write( "-->" );
					//	break;
			}
		}
		public static void XhmltFragmentToString(System.Text.StringBuilder sb, XmlNode node)
		{
			using (StringWriter writer = new StringWriter(sb))
			{
				dumpSub(node, writer);
			}
		}
	}
}
