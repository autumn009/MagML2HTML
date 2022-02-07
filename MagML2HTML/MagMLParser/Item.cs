using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Drawing;
using System.Xml;
using System.Diagnostics;
using SimpleCharTestLib;

namespace MagMLParser
{
	public delegate string GetItemFileName(ItemID contentItemID, FullKeyword keyword);

	public enum EBodyType
	{
		XhtmlFragment,  // ItemのXML文書内に、XHTMLフラグメントを埋め込む (互換用)
		PlaneText,      // プレーンテキストとして埋め込む
		XhtmlByText,    // XHTMLフラグメントのテキスト表現をテキストとして埋め込む
		MagML           // MagMLソースをプレーンテキストとして埋め込む
	}

	public class Item
	{
		public class AttachedFile : ICloneable
		{
			public int Number;
			public string FileName;
			public string Name;
			public Size PixelSize;
			public bool DisabledInMenu;
			public AttachedFile(int number, string fileName, string name, Size size, bool disabledInMenu)
			{
				Number = number;
				FileName = fileName;
				Name = name;
				PixelSize = size;
				DisabledInMenu = disabledInMenu;
			}
			public override string ToString()
			{
				System.Text.StringBuilder sb = new System.Text.StringBuilder();
				sb.Append("No.");
				sb.Append(Number.ToString());
				sb.Append(' ');
				sb.Append(Name);
				sb.Append(" (");
				sb.Append(FileName);
				sb.Append(")");
				return sb.ToString();
			}

			public static int ParseNumber(string s)
			{
				if (!s.StartsWith("No."))
				{
					throw new InvalidOperationException(s + "は解析できないAttachedFile文字列です。");
				}
				int from = 3;
				int p = from;
				while (true)
				{
					if (p >= s.Length) break;
					if (!SimpleCharTest.IsDigit(s[p])) break;
					p++;
				}
				string t = s.Substring(from, p - from);
				if (t.Length == 0)
				{
					throw new InvalidOperationException(s + "は解析できないAttachedFile文字列です。");
				}
				try
				{
					return int.Parse(t);
				}
				catch (FormatException e)
				{
					throw new InvalidOperationException(s + "は解析できないAttachedFile文字列です。", e);
				}
			}

			public object Clone()
			{
				AttachedFile newItem = new AttachedFile(this.Number, this.FileName, this.Name, this.PixelSize, this.DisabledInMenu);
				return newItem;
			}
		}

		public delegate void ConfirmNotNoDataItem();

		public class ChileCollection : IEnumerable
		{
			private ArrayList collection = new ArrayList();

			public IEnumerator GetEnumerator()
			{
				return collection.GetEnumerator();
			}

			public void Add(string asin)
			{
				confirmNotNoDataItem();
				collection.Add(asin);
			}
			public void Clear()
			{
				confirmNotNoDataItem();
				collection.Clear();
			}
			public string this[int index]
			{
				get { return (string)collection[index]; }
				set
				{
					confirmNotNoDataItem();
					collection[index] = value;
				}
			}
			public int Count
			{
				get { return collection.Count; }
			}
			private ConfirmNotNoDataItem confirmNotNoDataItem;
			public ChileCollection(ConfirmNotNoDataItem confirmNotNoDataItem)
			{
				this.confirmNotNoDataItem = confirmNotNoDataItem;
			}
			public static void alwaysNoDataItem()
			{
				throw new System.InvalidOperationException("存在しないアイテムに書き込もうとしました。");
			}
			public static ChileCollection Empty
			{
				get
				{
					return new ChileCollection(new ConfirmNotNoDataItem(alwaysNoDataItem));
				}
			}
		}

		private const string DateTimeFormat = "yyyyMMddHHmmss";

		private GetItemFileName getItemFileName;
		private string lastLoadedFileName = "";
		private FullKeyword lastKeyword = FullKeyword.NoKeyword;
		public readonly ItemID ItemID;
		public readonly DateTime Date;

		private void confirmNotNoDataItem()
		{
			if (ItemID.IsNoItem())
			{
				throw new System.InvalidOperationException("存在しないアイテムに書き込もうとしました。");
			}
		}

		public ChileCollection ChileDirectIDs;
		public ChileCollection ChileEmbeddedIDs;
		public ChileCollection ChileRepresentISs;

		public ChileCollection AllChileIDs
		{
			get
			{
				var t = new ChileCollection(this.confirmNotNoDataItem);
				foreach (string s in ChileRepresentISs) t.Add(s);
				foreach (string s in ChileEmbeddedIDs) t.Add(s);
				foreach (string s in ChileDirectIDs) t.Add(s);
				return t;
			}
		}

		private string rendererName = "";
		public string RendererName
		{
			get { return rendererName; }
			set
			{
				confirmNotNoDataItem();
				rendererName = value;
			}
		}

		private DateTime renderedDate = DateTime.MinValue;
		public DateTime RenderedDate
		{
			get { return renderedDate; }
			set
			{
				confirmNotNoDataItem();
				renderedDate = value;
			}
		}

		private string renderedBody = "";
		public string RenderedBody
		{
			get { return renderedBody; }
			set
			{
				confirmNotNoDataItem();
				renderedBody = value;
			}
		}

		private string subject = "";
		public string Subject
		{
			get { return subject; }
			set
			{
				confirmNotNoDataItem();
				subject = value;
			}
		}

		public string SubjectFull
		{
			get
			{
				return WriteLib.BuildSubject(subject, AdditionalTitle);
			}
		}

		private FullKeyword keyword = FullKeyword.NoKeyword;
		public FullKeyword Keyword
		{
			get { return keyword; }
			set
			{
				confirmNotNoDataItem();
				keyword = value;
			}
		}

		private string body = "";
		public string Body
		{
			get { return body; }
			set
			{
				confirmNotNoDataItem();
				body = value;
			}
		}

		private EBodyType bodyType = EBodyType.XhtmlByText;
		public EBodyType BodyType
		{
			get { return bodyType; }
			set
			{
				confirmNotNoDataItem();
				bodyType = value;
			}
		}

		private AttachedFile[] attachedFiles = { };
		public AttachedFile[] AttachedFiles
		{
			get
			{
				lock (this)
				{
					return (AttachedFile[])attachedFiles.Clone();
				}
			}
		}

		public void AddAttahedFile(int newNumber, string fileName, string name, Size size, bool disabledInMenu)
		{
			confirmNotNoDataItem();
			lock (this)
			{
				AttachedFile[] temp = new AttachedFile[attachedFiles.Length + 1];
				for (int i = 0; i < attachedFiles.Length; i++)
				{
					temp[i] = attachedFiles[i];
				}
				temp[attachedFiles.Length] = new AttachedFile(newNumber, fileName, name, size, disabledInMenu);
				attachedFiles = temp;
			}
		}

		public void ClearAttachedFiles()
		{
			confirmNotNoDataItem();
			attachedFiles = new Item.AttachedFile[0];
		}

#if false
		public void AddAttahedFile( string fileName, string name )
		{
			lock( this )
			{
				int newNumber = 1;
				foreach( AttachedFile file in attachedFiles )
				{
					if( file.Number >= newNumber )
					{
						newNumber = file.Number+1;
					}
				}
				AddAttahedFile( newNumber, fileName, name );
			}
		}
#endif

		private Item.AttachedFile searchAttachedFile(int number)
		{
			foreach (Item.AttachedFile attachedFile in this.attachedFiles)
			{
				if (attachedFile.Number == number)
				{
					return attachedFile;
				}
			}
			throw new InvalidOperationException("指定された番号のファイルが見つかりません。");
		}

		public void RemoveAttahedFile(int number)
		{
			lock (this)
			{
				Item.AttachedFile targetAttachedFile = searchAttachedFile(number);
				AttachedFile[] temp = new AttachedFile[attachedFiles.Length - 1];
				int p = 0;
				foreach (Item.AttachedFile attachedFile in this.attachedFiles)
				{
					if (!object.ReferenceEquals(attachedFile, targetAttachedFile))
					{
						temp[p++] = attachedFile;
					}
				}
				attachedFiles = temp;
			}
		}

		public void RenameNameOfAttahedFile(int number, string newName)
		{
			lock (this)
			{
				searchAttachedFile(number).Name = newName;
			}
		}

		private ArrayList alreadySentTrackBackUrls = new ArrayList();
		public ArrayList GetAlreadySentTrackBackUrls()
		{
			lock (this)
			{
				return (ArrayList)alreadySentTrackBackUrls.Clone();
			}
		}

		private ArrayList waitingTrackBackUrls = new ArrayList();
		public ArrayList GetWaitingTrackBackUrls()
		{
			lock (this)
			{
				return (ArrayList)waitingTrackBackUrls.Clone();
			}
		}

		private ArrayList pendingTrackBackUrls = new ArrayList();
		public ArrayList GetPendingTrackBackUrls()
		{
			lock (this)
			{
				return (ArrayList)pendingTrackBackUrls.Clone();
			}
		}

		private ArrayList processingTrackBackUrls = new ArrayList();
		public ArrayList GetProcessingTrackBackUrls()
		{
			lock (this)
			{
				return (ArrayList)processingTrackBackUrls.Clone();
			}
		}

		public void AddWaitingTrackBackUrl(string url)
		{
			lock (this)
			{
				if (!waitingTrackBackUrls.Contains(url))
				{
					waitingTrackBackUrls.Add(url);
				}
			}
		}

		public void ClearWaitingTrackBackUrls()
		{
			lock (this)
			{
				waitingTrackBackUrls.Clear();
			}
		}

		public void MoveWaitingTrackBackUrlToProcessing(string url)
		{
			lock (this)
			{
				if (!waitingTrackBackUrls.Contains(url))
				{
					throw new ApplicationException("waitingTrackBackUrlsに登録されていない" + url + "は移動できません。");
				}
				waitingTrackBackUrls.Remove(url);
				processingTrackBackUrls.Add(url);
			}
		}

		public void MoveProcessingTrackBackUrlToAlreadySent(string url)
		{
			lock (this)
			{
				if (!processingTrackBackUrls.Contains(url))
				{
					throw new ApplicationException("processingTrackBackUrlsに登録されていない" + url + "は移動できません。");
				}
				processingTrackBackUrls.Remove(url);
				alreadySentTrackBackUrls.Add(url);
			}
		}

		public void MoveProcessingTrackBackUrlToPending(string url)
		{
			lock (this)
			{
				if (!processingTrackBackUrls.Contains(url))
				{
					throw new ApplicationException("processingTrackBackUrlsに登録されていない" + url + "は移動できません。");
				}
				processingTrackBackUrls.Remove(url);
				pendingTrackBackUrls.Add(url);
			}
		}

		public void MovePendingTrackBackUrlToWaiting(string url)
		{
			lock (this)
			{
				if (!pendingTrackBackUrls.Contains(url))
				{
					throw new ApplicationException("pendingTrackBackUrlsに登録されていない" + url + "は移動できません。");
				}
				pendingTrackBackUrls.Remove(url);
				waitingTrackBackUrls.Add(url);
			}
		}

		public void RemoveWaitingTrackBackUrl(string url)
		{
			lock (this)
			{
				waitingTrackBackUrls.Remove(url);
			}
		}

		public void RemoveProcessingTrackBackUrl(string url)
		{
			lock (this)
			{
				processingTrackBackUrls.Remove(url);
			}
		}

		public void RemovePendingTrackBackUrl(string url)
		{
			lock (this)
			{
				pendingTrackBackUrls.Remove(url);
			}
		}

		private ArrayList categoryNames = new ArrayList();
		public ArrayList GetCategoryNames()
		{
			lock (this)
			{
				return (ArrayList)categoryNames.Clone();
			}
		}

		public void AddCategoryName(string categoryName)
		{
			lock (this)
			{
				if (!categoryNames.Contains(categoryName))
				{
					categoryNames.Add(categoryName);
				}
			}
		}

		public void SetCategoryNames(ArrayList newCategoryNameList)
		{
			lock (this)
			{
				categoryNames = (ArrayList)newCategoryNameList.Clone();
			}
		}

		public void RemoveCategoryName(string categoryName)
		{
			lock (this)
			{
				categoryNames.Remove(categoryName);
			}
		}




		// Todo:最適化の余地大
		public string GetDigestText()
		{
			string target = Body;
			if (BodyType != EBodyType.PlaneText)
			{
				XmlDocument doc = new XmlDocument();
				try
				{
					doc.LoadXml("<div>" + (RenderedBody.Length > 0 ? RenderedBody : Body) + "</div>");
					target = doc.InnerText;
				}
				catch (XmlException)
				{
					// 何もしない。XML文書扱いしないでスルーする
				}
			}

			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			//sb.Append("本文の先頭: ");
			int p = 0;
			while (true)
			{
				if (p >= target.Length) break;
				if (sb.Length >= 100) break;
				if (target[p] >= ' ')
				{
					sb.Append(target[p]);
				}
				p++;
			}
			return sb.ToString();
		}

		private string additionalTitle = "";

		public string AdditionalTitle
		{
			get { return additionalTitle; }
			set { additionalTitle = value; }
		}

		private void loadStringIntoArrayList(XmlDocument document, string elementName, ArrayList targetToLoad)
		{
			targetToLoad.Clear();
			XmlNodeList list = document.GetElementsByTagName(elementName, XmlNamespaces.Item);
			foreach (XmlNode node in list)
			{
				targetToLoad.Add(node.InnerText);
			}
		}

		private void load()
		{
			string fullpath = getItemFileName(ItemID, Keyword);
			XmlDocument document = new XmlDocument();

			try
			{
				// 注意: ここはdocument.Load(fullpath);としてはならない。なぜなら、
				// Loadメソッドの引数はファイル名ではなくURLであり、URLではファイル名
				// で使用できる文字の一部が使用できない文字になり、エラーになるため。
				// それは、URLならではの方法でエンコードする必要がある。
				// 例: 空白が%20など
				// しかし、そんな面倒なことをする必要はない。このコードで十分。
				using (FileStream stream = SafeFileStream.CreateFileStream(fullpath,
						   FileMode.Open, FileAccess.Read, FileShare.Read))
				{
					document.Load(stream);
				}
			}
			catch (FileNotFoundException)
			{
				return;
			}
			catch (IOException e)
			{
				throw;
			}

			loadSub(fullpath, document);
		}

		private void loadSub(string fullpath, XmlDocument document)
		{
			XmlNodeList subjectList = document.GetElementsByTagName("subject", XmlNamespaces.Item);
			Debug.Assert(subjectList.Count > 0);
			Subject = subjectList[0].InnerText;

			XmlNodeList additionalTitleList = document.GetElementsByTagName("additionalTitle", XmlNamespaces.Item);
			AdditionalTitle = additionalTitleList.Count > 0 ? additionalTitleList[0].InnerText : "";

			XmlNodeList rendererNameList = document.GetElementsByTagName("rendererName", XmlNamespaces.Item);
			rendererName = rendererNameList.Count > 0 ? rendererNameList[0].InnerText : "";

			XmlNodeList renderedDateList = document.GetElementsByTagName("renderedDate", XmlNamespaces.Item);
			renderedDate = rendererNameList.Count > 0 ? DateTime.ParseExact(renderedDateList[0].InnerText, DateTimeFormat, null) : DateTime.MinValue;

			XmlNodeList renderedBodyList = document.GetElementsByTagName("renderedBody", XmlNamespaces.Item);
			renderedBody = renderedBodyList.Count > 0 ? renderedBodyList[0].InnerText : "";

			XmlNodeList bodyTypeList = document.GetElementsByTagName("bodyType", XmlNamespaces.Item);
			if (bodyTypeList.Count == 0)
			{
				BodyType = EBodyType.XhtmlFragment;
			}
			else
			{
				BodyType = (EBodyType)Enum.Parse(typeof(EBodyType), bodyTypeList[0].InnerText);
			}

			XmlNodeList bodyList = document.GetElementsByTagName("body", XmlNamespaces.Item);
			Debug.Assert(bodyList.Count > 0);
			if (BodyType == EBodyType.XhtmlFragment)
			{
				System.Text.StringBuilder sb = new System.Text.StringBuilder();
				foreach (XmlNode child in bodyList[0].ChildNodes)
				{
					HtmlLib.XhmltFragmentToString(sb, child);
				}
				Body = sb.ToString();
			}
			else
			{
				Body = bodyList[0].InnerText;
			}

			XmlNodeList attachedFileList = document.GetElementsByTagName("attachedFile", XmlNamespaces.Item);
			foreach (XmlNode attachedFileNode in attachedFileList)
			{
				int number = -1;
				string fileName = "No_File_Name";
				string name = "タイトル無し";
				int pixelWidth = 0, pixelHeight = 0;
				bool disabledInMenu = false;
				foreach (XmlNode node in attachedFileNode.ChildNodes)
				{
					if (node.NodeType == XmlNodeType.Element
						&& node.NamespaceURI == XmlNamespaces.Item)
					{
						switch (node.LocalName)
						{
							case "number":
								number = int.Parse(node.InnerText);
								break;
							case "fileName":
								fileName = node.InnerText;
								break;
							case "name":
								name = node.InnerText;
								break;
							case "pixelWidth":
								pixelWidth = int.Parse(node.InnerText);
								break;
							case "pixelHeight":
								pixelHeight = int.Parse(node.InnerText);
								break;
							case "disabledInMenu":
								disabledInMenu = bool.Parse(node.InnerText);
								break;
						}
					}
				}
				// Todo: 不正なXML文書であった場合の対処が不十分かもしれない
				if (number > 0)
				{
					this.AddAttahedFile(number, fileName, name, new Size(pixelWidth, pixelHeight), disabledInMenu);
				}
			}

			XmlNodeList peruAsinList = document.GetElementsByTagName(SpecialResource.PeruAsin, XmlNamespaces.Item);
			foreach (XmlNode peruAsinNode in peruAsinList)
			{
				this.ChileDirectIDs.Add(peruAsinNode.InnerText);
			}
			XmlNodeList peruEmbeddedAsinList = document.GetElementsByTagName(SpecialResource.PeruEmbeddedAsin, XmlNamespaces.Item);
			foreach (XmlNode peruAsinNode in peruEmbeddedAsinList)
			{
				this.ChileEmbeddedIDs.Add(peruAsinNode.InnerText);
			}
			XmlNodeList peruRepresentAsinList = document.GetElementsByTagName(SpecialResource.PeruRepresentAsin, XmlNamespaces.Item);
			foreach (XmlNode peruAsinNode in peruRepresentAsinList)
			{
				this.ChileRepresentISs.Add(peruAsinNode.InnerText);
			}

			loadStringIntoArrayList(document, "waitingTrackBackUrl", this.waitingTrackBackUrls);
			loadStringIntoArrayList(document, "processingTrackBackUrl", this.processingTrackBackUrls);
			loadStringIntoArrayList(document, "pendingTrackBackUrl", this.pendingTrackBackUrls);
			loadStringIntoArrayList(document, "alreadySentTrackBackUrl", this.alreadySentTrackBackUrls);
			loadStringIntoArrayList(document, "categoryName", this.categoryNames);

			lastLoadedFileName = fullpath;
			lastKeyword = Keyword;

			if (BodyType == EBodyType.XhtmlFragment)
			{
				BodyType = EBodyType.XhtmlByText;
			}
		}

		private void writeElementStringOnNotEmpty(XmlWriter writer, string elementName, string val)
		{
			if (val.Length > 0)
			{
				UtilXmlWriter.WriteElementString(writer, elementName, XmlNamespaces.Item, val);
			}
		}

		private void writeStringCollection(XmlWriter writer, string elementName, IEnumerable targetToSave)
		{
			foreach (string s in targetToSave)
			{
				UtilXmlWriter.WriteElementString(writer, elementName, XmlNamespaces.Item, s);
			}
		}

		private void save()
		{
			// 新規コンテンツをEBodyType.XhtmlFragmentで書き出すことは2度とあり得ないとする
			Trace.Assert(this.BodyType != EBodyType.XhtmlFragment);

			string fullpath = getItemFileName(ItemID, Keyword);
			string tempFullPath = Path.ChangeExtension(fullpath, ".$$$");
			FullKeyword oldKeyword = lastKeyword;
			if (lastLoadedFileName == "")
			{
				lastLoadedFileName = fullpath;
				lastKeyword = Keyword;
			}
			string bakFullPath = Path.ChangeExtension(lastLoadedFileName, ".bak");

			FileStream stream = new FileStream(tempFullPath, FileMode.Create);
			XmlTextWriter writer = new XmlTextWriter(stream, System.Text.Encoding.UTF8);
			try
			{
				writer.WriteStartDocument();
				UtilXmlWriter.WriteNewline(writer);

				writer.WriteStartElement("vt", "column", XmlNamespaces.Item);
				writer.WriteAttributeString("xmlns", null, XmlNamespaces.Xhtml);
				UtilXmlWriter.WriteNewline(writer);

				UtilXmlWriter.WriteElementString(writer, "date", XmlNamespaces.Item, ItemID.ID);
				UtilXmlWriter.WriteElementString(writer, "subject", XmlNamespaces.Item, Subject);
				UtilXmlWriter.WriteElementString(writer, "additionalTitle", XmlNamespaces.Item, AdditionalTitle);
				writeStringCollection(writer, "keyword", this.Keyword.Keywords);
				writeStringCollection(writer, SpecialResource.PeruAsin, this.ChileDirectIDs);
				writeStringCollection(writer, SpecialResource.PeruEmbeddedAsin, this.ChileEmbeddedIDs);
				writeStringCollection(writer, SpecialResource.PeruRepresentAsin, this.ChileRepresentISs);
				UtilXmlWriter.WriteElementString(writer, "bodyType", XmlNamespaces.Item, this.BodyType.ToString());

				foreach (AttachedFile attach in this.attachedFiles)
				{
					writer.WriteStartElement("attachedFile", XmlNamespaces.Item);
					UtilXmlWriter.WriteNewline(writer);

					UtilXmlWriter.WriteElementString(writer, "number", XmlNamespaces.Item, attach.Number.ToString());
					UtilXmlWriter.WriteElementString(writer, "fileName", XmlNamespaces.Item, attach.FileName);
					UtilXmlWriter.WriteElementString(writer, "name", XmlNamespaces.Item, attach.Name);
					if (!attach.PixelSize.IsEmpty)
					{
						UtilXmlWriter.WriteElementString(writer, "pixelWidth", XmlNamespaces.Item, attach.PixelSize.Width.ToString());
						UtilXmlWriter.WriteElementString(writer, "pixelHeight", XmlNamespaces.Item, attach.PixelSize.Height.ToString());
					}
					if (attach.DisabledInMenu)
					{
						UtilXmlWriter.WriteElementString(writer, "disabledInMenu", XmlNamespaces.Item, attach.DisabledInMenu.ToString());
					}
					UtilXmlWriter.WriteEndElement(writer);
				}

				UtilXmlWriter.WriteElementString(writer, "body", XmlNamespaces.Item, Body);
				writeElementStringOnNotEmpty(writer, "rendererName", rendererName);
				if (renderedDate != DateTime.MinValue)
				{
					UtilXmlWriter.WriteElementString(writer, "renderedDate", XmlNamespaces.Item, renderedDate.ToString(DateTimeFormat));
				}
				writeElementStringOnNotEmpty(writer, "renderedBody", renderedBody);
				writeStringCollection(writer, "waitingTrackBackUrl", this.waitingTrackBackUrls);
				writeStringCollection(writer, "processingTrackBackUrl", this.processingTrackBackUrls);
				writeStringCollection(writer, "pendingTrackBackUrl", this.pendingTrackBackUrls);
				writeStringCollection(writer, "alreadySentTrackBackUrl", this.alreadySentTrackBackUrls);
				writeStringCollection(writer, "categoryName", this.categoryNames);

				UtilXmlWriter.WriteEndElement(writer);
				writer.WriteEndDocument();
			}
			finally
			{
				writer.Close();
				stream.Close();
			}
			if (File.Exists(lastLoadedFileName))
			{
				File.Delete(bakFullPath);
				File.Move(lastLoadedFileName, bakFullPath);
			}
			File.Delete(fullpath);
			File.Move(tempFullPath, fullpath);
			ContentFileAccessLayer.ClearCache();
		}

		public void Update()
		{
			confirmNotNoDataItem();
			lock (this)
			{
				save();
			}
		}
		public Item(ItemID itemID, FullKeyword keyword)
		{
			ChileDirectIDs = new ChileCollection(new ConfirmNotNoDataItem(confirmNotNoDataItem));
			ChileEmbeddedIDs = new ChileCollection(new ConfirmNotNoDataItem(confirmNotNoDataItem));
			ChileRepresentISs = new ChileCollection(new ConfirmNotNoDataItem(confirmNotNoDataItem));
			ItemID = itemID;
			Date = itemID.GetDateTime();
			this.getItemFileName = FileLayout.GetContentFileName;
			Keyword = keyword;
			load();
		}
		public Item(FullKeyword keyword, ItemID itemID, XmlDocument doc, GetItemFileName getItemFileName)
		{
			ChileDirectIDs = new ChileCollection(new ConfirmNotNoDataItem(confirmNotNoDataItem));
			ChileEmbeddedIDs = new ChileCollection(new ConfirmNotNoDataItem(confirmNotNoDataItem));
			ChileRepresentISs = new ChileCollection(new ConfirmNotNoDataItem(confirmNotNoDataItem));
			this.getItemFileName = getItemFileName;
			Keyword = keyword;
			ItemID = itemID;
			loadSub("", doc);
			Date = itemID.GetDateTime();
		}
		private Item(ItemID itemID, DateTime datetime)
		{
			ChileDirectIDs = new ChileCollection(new ConfirmNotNoDataItem(confirmNotNoDataItem));
			ChileEmbeddedIDs = new ChileCollection(new ConfirmNotNoDataItem(confirmNotNoDataItem));
			ChileRepresentISs = new ChileCollection(new ConfirmNotNoDataItem(confirmNotNoDataItem));
			ItemID = itemID;
			Date = datetime;
		}
		public Item(GetItemFileName getItemFileName, ItemID itemID, FullKeyword keyword)
			: this(itemID, DateTime.ParseExact(itemID.ID, DateTimeFormat, null))
		{
			this.getItemFileName = getItemFileName;
			Keyword = keyword;
			load();
		}
		public void CopyFrom(Item duplicateFrom)
		{
			lock (this)
			{
				lock (duplicateFrom)
				{
					this.keyword = duplicateFrom.keyword;
					this.subject = duplicateFrom.subject;
					this.AdditionalTitle = duplicateFrom.AdditionalTitle;
					this.bodyType = duplicateFrom.bodyType;
					this.body = duplicateFrom.body;
					this.rendererName = duplicateFrom.rendererName;
					this.renderedDate = duplicateFrom.renderedDate;
					this.renderedBody = duplicateFrom.renderedBody;

					this.ChileDirectIDs.Clear();
					for (int i = 0; i < duplicateFrom.ChileDirectIDs.Count; i++)
					{
						this.ChileDirectIDs.Add(duplicateFrom.ChileDirectIDs[i]);
					}
					this.ChileEmbeddedIDs.Clear();
					for (int i = 0; i < duplicateFrom.ChileEmbeddedIDs.Count; i++)
					{
						this.ChileEmbeddedIDs.Add(duplicateFrom.ChileEmbeddedIDs[i]);
					}
					this.ChileRepresentISs.Clear();
					for (int i = 0; i < duplicateFrom.ChileRepresentISs.Count; i++)
					{
						this.ChileRepresentISs.Add(duplicateFrom.ChileRepresentISs[i]);
					}

					this.ClearAttachedFiles();
					foreach (Item.AttachedFile attachedFile in duplicateFrom.AttachedFiles)
					{
						this.AddAttahedFile(attachedFile.Number, attachedFile.FileName,
							attachedFile.Name, attachedFile.PixelSize, attachedFile.DisabledInMenu);
					}

					this.waitingTrackBackUrls = (ArrayList)duplicateFrom.waitingTrackBackUrls.Clone();
					this.processingTrackBackUrls = (ArrayList)duplicateFrom.processingTrackBackUrls.Clone();
					this.pendingTrackBackUrls = (ArrayList)duplicateFrom.pendingTrackBackUrls.Clone();
					this.alreadySentTrackBackUrls = (ArrayList)duplicateFrom.alreadySentTrackBackUrls.Clone();
					this.categoryNames = (ArrayList)duplicateFrom.categoryNames.Clone();
				}
			}
		}

		public Item(ItemID itemID, Item duplicateFrom, GetItemFileName getItemFileName) : this(itemID, DateTime.ParseExact(itemID.ID, DateTimeFormat, null))
		{
			this.getItemFileName = getItemFileName;
			this.CopyFrom(duplicateFrom);
		}

		public static readonly Item DummyItem;
		public bool IsDummyItem()
		{
			return this == DummyItem;
		}
		private static string GetDummyFileName(ItemID contentItemID, FullKeyword keyword)
		{
			return "";
		}
		private static void dummyNotifyUpdate(ItemID itemID, FullKeyword oldKeyword, FullKeyword newKeyword)
		{
		}
		static Item()
		{
			DummyItem = new Item(ItemID.NoItem, DateTime.MinValue);
			DummyItem.getItemFileName = new GetItemFileName(GetDummyFileName);
			DummyItem.keyword = FullKeyword.NoKeyword;
			DummyItem.body = "このメッセージは発見できません。削除された可能性があります。";
			DummyItem.bodyType = EBodyType.PlaneText;
			DummyItem.subject = "削除された可能性のあるメッセージ";
		}
	}

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
