using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MagMLParser
{
	public class FileExtentionManager
	{
		private const string jpegExtention = ".jpg";
		private const string pngExtention = ".png";
		private const string gifExtention = ".gif";
		private static bool testExtention(string filename, string extention)
		{
			return filename.ToLower().EndsWith(extention);
		}

		public static bool IsBitmap(string filename)
		{
			return testExtention(filename, jpegExtention)
				|| testExtention(filename, pngExtention)
				|| testExtention(filename, gifExtention);
		}

#if false
		public static System.Drawing.Imaging.ImageFormat FileNameToImageFormat(string filename)
		{
			if (testExtention(filename, jpegExtention))
			{
				return System.Drawing.Imaging.ImageFormat.Jpeg;
			}
			else if (testExtention(filename, pngExtention))
			{
				return System.Drawing.Imaging.ImageFormat.Png;
			}
			else if (testExtention(filename, gifExtention))
			{
				return System.Drawing.Imaging.ImageFormat.Gif;
			}
			throw new InvalidOperationException(filename + "の拡張子は画像として使用できないものです。.jpg, .png, .gifのいずれかでなければなりません。");
		}
#endif

		public static string FileNameToMimeType(string filename)
		{
			if (testExtention(filename, jpegExtention))
			{
				return "image/jpeg";
			}
			else if (testExtention(filename, pngExtention))
			{
				return "image/png";
			}
			else if (testExtention(filename, gifExtention))
			{
				return "image/gif";
			}
			return "application/octet-stream";
		}
	}
}
