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
		public static string CreateAttachedFileURI(ItemID itemID, string? filename, int width)
		{
			throw new NotImplementedException("CreateAttachedFileURI");
		}
		public static int SuggestedPictureIndex(int width, int[] resizeWidths)
		{
			int index = 0;
			while (true)
			{
				if (index >= resizeWidths.Length) break;
				if (resizeWidths[index] >= width) break;
				if (index == 2) break;
				index++;
			}
			return index;
		}
		public static void CreatePictureMenuItem(NhInline parent, ItemID itemID,
			Item.AttachedFile attachedFile)
		{
			throw new NotImplementedException("CreateAttachedFileURI");
		}
		public static void CreateNormalMenuItem(NhInline parent, ItemID itemID,
	Item.AttachedFile attachedFile)
		{
			throw new NotImplementedException("CreateNormalMenuItem");
		}

        internal static string BuildSubject(string subject, string additionalTitle)
        {
            throw new NotImplementedException();
        }
    }
	public class UtilXmlWriter
	{
		public static void WriteNewline(XmlWriter writer)
		{
			writer.WriteWhitespace("\r\n");
		}
		public static void WriteEndElement(XmlWriter writer)
		{
			writer.WriteEndElement();
			WriteNewline(writer);
		}
		//writer.WriteElementString(elementName,XmlNamespaces.Item,val);
		//XmlNamespaces.TrackBack

		public static void WriteElementString(XmlWriter writer, string elementName, string namespaceURI, string val)
		{
			writer.WriteElementString(elementName, namespaceURI, val);
			UtilXmlWriter.WriteNewline(writer);
		}
	}

}