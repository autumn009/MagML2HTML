using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Drawing;
using System.Xml;
using System.Diagnostics;

namespace MagMLParser
{
	public class IdAlreadyExistException : ApplicationException
	{
		public IdAlreadyExistException()
		{
		}
		public IdAlreadyExistException(string message) : base(message)
		{
		}
		protected IdAlreadyExistException(System.Runtime.Serialization.SerializationInfo info,
			System.Runtime.Serialization.StreamingContext context)
			: base(info, context)
		{
		}
		public IdAlreadyExistException(string message, Exception innerException)
			: base(message, innerException)
		{
		}
	}
}
