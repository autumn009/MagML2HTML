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
	public enum EBodyType
	{
		XhtmlFragment,  // ItemのXML文書内に、XHTMLフラグメントを埋め込む (互換用)
		PlaneText,      // プレーンテキストとして埋め込む
		XhtmlByText,    // XHTMLフラグメントのテキスト表現をテキストとして埋め込む
		MagML           // MagMLソースをプレーンテキストとして埋め込む
	}

	public class Item
	{
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

		private ArrayList alreadySentTrackBackUrls = new ArrayList();

		private ArrayList waitingTrackBackUrls = new ArrayList();

		private ArrayList pendingTrackBackUrls = new ArrayList();

		private ArrayList processingTrackBackUrls = new ArrayList();

		private ArrayList categoryNames = new ArrayList();

		private string additionalTitle = "";

		public string AdditionalTitle
		{
			get { return additionalTitle; }
			set { additionalTitle = value; }
		}


		private Item(ItemID itemID, DateTime datetime)
		{
			ChileDirectIDs = new ChileCollection(new ConfirmNotNoDataItem(confirmNotNoDataItem));
			ChileEmbeddedIDs = new ChileCollection(new ConfirmNotNoDataItem(confirmNotNoDataItem));
			ChileRepresentISs = new ChileCollection(new ConfirmNotNoDataItem(confirmNotNoDataItem));
			ItemID = itemID;
			Date = datetime;
		}
		public void CopyFrom(Item duplicateFrom)
		{
			lock (this)
			{
				lock (duplicateFrom)
				{
					this.AdditionalTitle = duplicateFrom.AdditionalTitle;

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

					this.waitingTrackBackUrls = (ArrayList)duplicateFrom.waitingTrackBackUrls.Clone();
					this.processingTrackBackUrls = (ArrayList)duplicateFrom.processingTrackBackUrls.Clone();
					this.pendingTrackBackUrls = (ArrayList)duplicateFrom.pendingTrackBackUrls.Clone();
					this.alreadySentTrackBackUrls = (ArrayList)duplicateFrom.alreadySentTrackBackUrls.Clone();
					this.categoryNames = (ArrayList)duplicateFrom.categoryNames.Clone();
				}
			}
		}

		public Item(ItemID itemID, Item duplicateFrom) : this(itemID, DateTime.ParseExact(itemID.ID, DateTimeFormat, null))
		{
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
