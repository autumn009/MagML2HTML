using NestedHtmlWriter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace MagMLParser
{
	internal class WriteLib
	{
		// see RFC 3986:
		// unreserved    = ALPHA / DIGIT / "-" / "." / "_" / "~"
		public static bool IsUnreserved(byte b)
		{
			if (b >= 'A' && b <= 'Z') return true;
			if (b >= 'a' && b <= 'z') return true;
			if (b >= '0' && b <= '9') return true;
			if (b == '-') return true;
			if (b == '.') return true;
			if (b == '_') return true;
			if (b == '~') return true;
			return false;
		}
    }
	public class UtilXmlWriter
	{
		public static void WriteNewline(XmlWriter writer)
		{
			writer.WriteWhitespace("\r\n");
		}
	}

}